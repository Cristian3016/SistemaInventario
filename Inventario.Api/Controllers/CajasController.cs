using Inventario.Api.Data;
using Inventario.Api.DTOs;
using Inventario.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Inventario.Api.Authorization;
using Inventario.Api.Services;

namespace Inventario.Api.Controllers
{
    [ApiController]
    [Route("api/cajas")]
    public class CajasController : ControllerBase
    {
        private readonly InventarioDbContext _context;
        private readonly IAuditoriaService _auditoriaService;

        public CajasController(
            InventarioDbContext context,
            IAuditoriaService auditoriaService)
        {
            _context = context;
            _auditoriaService = auditoriaService;
        }

        [Permiso(Permisos.CajaGestionar)]
        [HttpPost("abrir")]
        public async Task<IActionResult> AbrirCaja(
            [FromBody] AbrirCajaDto datos)
        {
            if (!int.TryParse(User.FindFirst("sub")?.Value, out var usuarioId) ||
                usuarioId <= 0)
            {
                return Unauthorized();
            }

            if (datos == null)
                return BadRequest("Debes proporcionar los datos de apertura.");

            if (datos.MontoInicial < 0)
                return BadRequest("El monto inicial no puede ser negativo.");

            if (decimal.Round(datos.MontoInicial, 2) != datos.MontoInicial)
                return BadRequest("El monto inicial no puede superar dos decimales.");

            const decimal limiteDinero = 10000000000000000m;

            if (datos.MontoInicial >= limiteDinero)
                return BadRequest("El monto inicial supera el límite permitido.");

            var observaciones = datos.Observaciones?.Trim();

            if (observaciones?.Length > 500)
                return BadRequest("Las observaciones no pueden superar los 500 caracteres.");

            try
            {
                await using var transaccion = await _context.Database
                    .BeginTransactionAsync(
                        System.Data.IsolationLevel.Serializable);

                var existeCajaAbierta = await _context.Cajas
                    .AnyAsync(c => c.Estado == "Abierta");

                if (existeCajaAbierta)
                    return Conflict("Ya existe una caja abierta.");

                var caja = new Caja
                {
                    FechaApertura = DateTime.UtcNow,
                    MontoInicial = datos.MontoInicial,
                    Estado = "Abierta",
                    Observaciones = string.IsNullOrWhiteSpace(observaciones)
                        ? null
                        : observaciones
                };

                _context.Cajas.Add(caja);

                await _context.SaveChangesAsync();

                _auditoriaService.Registrar(
                    usuarioId,
                    "ABRIR_CAJA",
                    "Caja",
                    caja.Id.ToString(),
                    $"Caja abierta correctamente. " +
                    $"Monto inicial: {caja.MontoInicial}.");

                await _context.SaveChangesAsync();

                await transaccion.CommitAsync();

                return StatusCode(201, new
                {
                    mensaje = "Caja abierta correctamente.",
                    caja.Id,
                    caja.FechaApertura,
                    caja.MontoInicial,
                    caja.Estado,
                    caja.Observaciones
                });
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is SqlException sqlEx &&
                (sqlEx.Number == 2601 || sqlEx.Number == 2627))
            {
                return Conflict("Ya existe una caja abierta.");
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is SqlException sqlEx &&
                sqlEx.Number == 1205)
            {
                return Conflict(
                    "Existe una operación simultánea. Intente nuevamente.");
            }
            catch (SqlException ex) when (ex.Number == 1205)
            {
                return Conflict(
                    "Existe una operación simultánea. Intente nuevamente.");
            }
        }

        [Permiso(Permisos.CajaGestionar)]
        [HttpGet("abierta")]
        public async Task<IActionResult> ObtenerCajaAbierta()
        {
            var caja = await _context.Cajas
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Estado == "Abierta");

            if (caja == null)
                return NotFound("No existe una caja abierta.");

            return Ok(caja);
        }

        [Permiso(Permisos.CajaGestionar)]
        [HttpGet]
        public async Task<IActionResult> ObtenerCajas(
            [FromQuery] string? estado)
        {
            string? estadoNormalizado = null;

            if (!string.IsNullOrWhiteSpace(estado))
            {
                estadoNormalizado = estado.Trim().ToLowerInvariant() switch
                {
                    "abierta" => "Abierta",
                    "cerrada" => "Cerrada",
                    _ => null
                };

                if (estadoNormalizado == null)
                    return BadRequest("El estado debe ser Abierta o Cerrada.");
            }

            var consulta = _context.Cajas.AsNoTracking();

            if (estadoNormalizado != null)
                consulta = consulta.Where(c => c.Estado == estadoNormalizado);

            var cajas = await consulta
                .OrderByDescending(c => c.FechaApertura)
                .ThenByDescending(c => c.Id)
                .ToListAsync();

            return Ok(cajas);
        }

        [Permiso(Permisos.CajaGestionar)]
        [HttpGet("{id:int}")]
        public async Task<IActionResult> ObtenerCajaPorId(int id)
        {
            if (id <= 0)
                return BadRequest("El ID de la caja debe ser mayor que cero.");

            var caja = await _context.Cajas
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id);

            if (caja == null)
                return NotFound("Caja no encontrada.");

            return Ok(caja);
        }

        [Permiso(Permisos.CajaGestionar)]
        [HttpGet("{id:int}/resumen")]
        public async Task<IActionResult> ObtenerResumenCaja(int id)
        {
            if (id <= 0)
                return BadRequest("El ID de la caja debe ser mayor que cero.");

            await using var transaccion =
                await _context.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.RepeatableRead);

            var caja = await _context.Cajas
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id);

            if (caja == null)
                return NotFound("Caja no encontrada.");

            var cobrosPorMetodo = await _context.PagosVenta
                .AsNoTracking()
                .Where(p => p.CajaId == id)
                .GroupBy(p => new
                {
                    p.MetodoPagoId,
                    p.MetodoPago.Nombre,
                    p.MetodoPago.EsEfectivo
                })
                .Select(g => new
                {
                    g.Key.MetodoPagoId,
                    metodoPago = g.Key.Nombre,
                    g.Key.EsEfectivo,
                    cantidadPagos = g.Count(),
                    total = g.Sum(p => p.Monto)
                })
                .OrderByDescending(p => p.EsEfectivo)
                .ThenBy(p => p.metodoPago)
                .ToListAsync();

            var totalCobros = cobrosPorMetodo.Sum(p => p.total);

            var cobrosEfectivo = cobrosPorMetodo
                .Where(p => p.EsEfectivo)
                .Sum(p => p.total);

            var cobrosElectronicos = cobrosPorMetodo
                .Where(p => !p.EsEfectivo)
                .Sum(p => p.total);

            var movimientos = await _context.MovimientosCaja
                .AsNoTracking()
                .Where(m => m.CajaId == id)
                .GroupBy(m => m.Tipo)
                .Select(g => new
                {
                    tipo = g.Key,
                    total = g.Sum(m => m.Monto)
                })
                .ToListAsync();

            var totalIngresos = movimientos
                .Where(m => m.tipo == TipoMovimientoCaja.Ingreso)
                .Sum(m => m.total);

            var totalEgresos = movimientos
                .Where(m => m.tipo == TipoMovimientoCaja.Egreso)
                .Sum(m => m.total);

            var efectivoCalculado =
                caja.MontoInicial +
                cobrosEfectivo +
                totalIngresos -
                totalEgresos;

            await transaccion.CommitAsync();

            return Ok(new
            {
                cajaId = caja.Id,
                caja.Estado,
                caja.FechaApertura,
                caja.FechaCierre,
                caja.MontoInicial,
                totalCobros,
                cobrosEfectivo,
                cobrosElectronicos,
                totalIngresos,
                totalEgresos,
                efectivoDisponible = caja.Estado == "Abierta"
                    ? (decimal?)efectivoCalculado
                    : null,
                montoEsperado = caja.Estado == "Cerrada"
                    ? caja.MontoEsperado
                    : efectivoCalculado,
                caja.MontoReal,
                caja.Diferencia,
                cobrosPorMetodo
            });
        }

        [Permiso(Permisos.CajaGestionar)]
        [HttpPatch("{id:int}/cerrar")]
        public async Task<IActionResult> CerrarCaja(
            int id,
            [FromBody] CerrarCajaDto datos)
        {
            const decimal limiteDinero = 10000000000000000m;

            if (!int.TryParse(User.FindFirst("sub")?.Value, out var usuarioId) ||
                usuarioId <= 0)
            {
                return Unauthorized();
            }

            if (id <= 0)
                return BadRequest(
                    "El ID de la caja debe ser mayor que cero.");

            if (datos == null)
                return BadRequest(
                    "Debes proporcionar los datos del cierre.");

            if (datos.MontoReal < 0)
                return BadRequest(
                    "El monto real no puede ser negativo.");

            if (decimal.Round(datos.MontoReal, 2) != datos.MontoReal)
                return BadRequest(
                    "El monto real no puede superar dos decimales.");

            if (datos.MontoReal >= limiteDinero)
                return BadRequest(
                    "El monto real supera el límite permitido.");

            var observaciones = datos.Observaciones?.Trim();

            if (observaciones?.Length > 500)
                return BadRequest(
                    "Las observaciones no pueden superar los 500 caracteres.");

            try
            {
                await using var transaccion = await _context.Database
                    .BeginTransactionAsync(
                        System.Data.IsolationLevel.Serializable);

                var cajaBloqueada = await _context.Cajas
                    .Where(c =>
                        c.Id == id &&
                        c.Estado == "Abierta")
                    .ExecuteUpdateAsync(cambios =>
                        cambios.SetProperty(
                            c => c.Estado,
                            c => c.Estado));

                if (cajaBloqueada == 0)
                {
                    var existeCaja = await _context.Cajas
                        .AnyAsync(c => c.Id == id);

                    if (!existeCaja)
                        return NotFound("Caja no encontrada.");

                    return Conflict(
                        "Solo se pueden cerrar cajas abiertas.");
                }

                var caja = await _context.Cajas
                    .SingleAsync(c => c.Id == id);

                var cobrosEfectivo = await _context.PagosVenta
                    .Where(p =>
                        p.CajaId == id &&
                        p.MetodoPago.EsEfectivo)
                    .SumAsync(p => (decimal?)p.Monto) ?? 0m;

                var totalIngresos = await _context.MovimientosCaja
                    .Where(m =>
                        m.CajaId == id &&
                        m.Tipo == TipoMovimientoCaja.Ingreso)
                    .SumAsync(m => (decimal?)m.Monto) ?? 0m;

                var totalEgresos = await _context.MovimientosCaja
                    .Where(m =>
                        m.CajaId == id &&
                        m.Tipo == TipoMovimientoCaja.Egreso)
                    .SumAsync(m => (decimal?)m.Monto) ?? 0m;

                var montoEsperado = checked(
                    caja.MontoInicial +
                    cobrosEfectivo +
                    totalIngresos -
                    totalEgresos);

                if (montoEsperado < 0 ||
                    montoEsperado >= limiteDinero)
                {
                    return Conflict(
                        "El efectivo esperado se encuentra fuera de los límites permitidos.");
                }

                var diferencia = checked(
                    datos.MontoReal - montoEsperado);

                caja.MontoEsperado = montoEsperado;
                caja.MontoReal = datos.MontoReal;
                caja.Diferencia = diferencia;
                caja.FechaCierre = DateTime.UtcNow;
                caja.Estado = "Cerrada";

                if (!string.IsNullOrWhiteSpace(observaciones))
                    caja.Observaciones = observaciones;

                var resultadoArqueo = diferencia switch
                {
                    > 0 => "Sobrante",
                    < 0 => "Faltante",
                    _ => "Caja cuadrada"
                };

                _auditoriaService.Registrar(
                    usuarioId,
                    "CERRAR_CAJA",
                    "Caja",
                    caja.Id.ToString(),
                    $"Caja cerrada correctamente. " +
                    $"Monto inicial: {caja.MontoInicial}. " +
                    $"Cobros en efectivo: {cobrosEfectivo}. " +
                    $"Total ingresos: {totalIngresos}. " +
                    $"Total egresos: {totalEgresos}. " +
                    $"Monto esperado: {montoEsperado}. " +
                    $"Monto real: {datos.MontoReal}. " +
                    $"Diferencia: {diferencia}. " +
                    $"Resultado: {resultadoArqueo}.");

                await _context.SaveChangesAsync();

                await transaccion.CommitAsync();

                return Ok(new
                {
                    mensaje = "Caja cerrada correctamente.",
                    caja.Id,
                    caja.FechaApertura,
                    caja.FechaCierre,
                    caja.MontoInicial,
                    cobrosEfectivo,
                    totalIngresos,
                    totalEgresos,
                    caja.MontoEsperado,
                    caja.MontoReal,
                    caja.Diferencia,
                    caja.Estado,
                    caja.Observaciones,
                    resultadoArqueo
                });
            }
            catch (OverflowException)
            {
                return Conflict(
                    "Los importes de la caja superan los límites permitidos.");
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(
                    "La caja fue modificada por otra operación.");
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is SqlException sqlEx &&
                sqlEx.Number == 1205)
            {
                return Conflict(
                    "Existe una operación simultánea. Intente nuevamente.");
            }
            catch (SqlException ex) when (ex.Number == 1205)
            {
                return Conflict(
                    "Existe una operación simultánea. Intente nuevamente.");
            }
        }
    }
}