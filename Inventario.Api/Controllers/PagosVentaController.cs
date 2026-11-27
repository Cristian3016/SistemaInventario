using Microsoft.Data.SqlClient;
using Inventario.Api.Data;
using Inventario.Api.DTOs;
using Inventario.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Inventario.Api.Authorization;
using Inventario.Api.Services;

namespace Inventario.Api.Controllers
{
    [ApiController]
    [Route("api/ventas/{ventaId:int}/pagos")]
    public class PagosVentaController : ControllerBase
    {
        private readonly InventarioDbContext _context;
        private readonly IAuditoriaService _auditoriaService;

        public PagosVentaController(
            InventarioDbContext context,
            IAuditoriaService auditoriaService)
        {
            _context = context;
            _auditoriaService = auditoriaService;
        }

        [Permiso(Permisos.CobrosRegistrar)]
        [HttpPost]
        public async Task<IActionResult> RegistrarPago(
            int ventaId,
            [FromBody] RegistrarPagoVentaDto datos)
        {
            const decimal limiteDinero = 10000000000000000m;

            if (!int.TryParse(User.FindFirst("sub")?.Value, out var usuarioId) ||
                usuarioId <= 0)
            {
                return Unauthorized();
            }

            if (ventaId <= 0)
                return BadRequest(
                    "El ID de la venta debe ser mayor que cero.");

            if (datos == null)
                return BadRequest(
                    "Debes proporcionar los datos del pago.");

            if (datos.MetodoPagoId <= 0)
                return BadRequest(
                    "El ID del método de pago debe ser mayor que cero.");

            if (datos.Monto <= 0)
                return BadRequest(
                    "El monto debe ser mayor que cero.");

            if (decimal.Round(datos.Monto, 2) != datos.Monto)
                return BadRequest(
                    "El monto no puede superar dos decimales.");

            if (datos.Monto >= limiteDinero)
                return BadRequest(
                    "El monto supera el límite permitido.");

            var referencia = datos.ReferenciaOperacion?.Trim();

            if (referencia?.Length > 150)
                return BadRequest(
                    "La referencia no puede superar los 150 caracteres.");

            try
            {
                await using var transaccion = await _context.Database
                    .BeginTransactionAsync(
                        System.Data.IsolationLevel.Serializable);

                var caja = await _context.Cajas
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c =>
                        c.Estado == "Abierta");

                if (caja == null)
                    return Conflict("No existe una caja abierta.");

                var cajaBloqueada = await _context.Cajas
                    .Where(c =>
                        c.Id == caja.Id &&
                        c.Estado == "Abierta")
                    .ExecuteUpdateAsync(cambios =>
                        cambios.SetProperty(
                            c => c.Estado,
                            c => c.Estado));

                if (cajaBloqueada == 0)
                    return Conflict(
                        "La caja ya no se encuentra abierta.");

                var venta = await _context.Ventas
                    .AsNoTracking()
                    .FirstOrDefaultAsync(v =>
                        v.Id == ventaId);

                if (venta == null)
                    return NotFound("Venta no encontrada.");

                if (venta.Estado != "Confirmada")
                    return Conflict(
                        "Solo se pueden registrar pagos de ventas confirmadas.");

                if (venta.Total <= 0 ||
                    venta.Total >= limiteDinero)
                {
                    return Conflict(
                        "El total de la venta no es válido.");
                }

                var metodoPago = await _context.MetodosPago
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m =>
                        m.Id == datos.MetodoPagoId);

                if (metodoPago == null)
                    return NotFound(
                        "Método de pago no encontrado.");

                if (!metodoPago.Activo)
                    return Conflict(
                        "El método de pago está inactivo.");

                var totalPagado = await _context.PagosVenta
                    .Where(p => p.VentaId == ventaId)
                    .SumAsync(p => (decimal?)p.Monto) ?? 0m;

                var saldoPendiente = venta.Total - totalPagado;

                if (totalPagado < 0 || saldoPendiente < 0)
                    return Conflict(
                        "Los importes registrados de la venta son inconsistentes.");

                if (saldoPendiente == 0)
                    return Conflict(
                        "La venta ya se encuentra totalmente abonada.");

                if (datos.Monto > saldoPendiente)
                {
                    return Conflict(new
                    {
                        mensaje = "El monto supera el saldo pendiente de la venta.",
                        totalVenta = venta.Total,
                        totalPagado,
                        saldoPendiente
                    });
                }

                if (metodoPago.EsEfectivo)
                {
                    var cobrosEfectivo = await _context.PagosVenta
                        .Where(p =>
                            p.CajaId == caja.Id &&
                            p.MetodoPago.EsEfectivo)
                        .SumAsync(p => (decimal?)p.Monto) ?? 0m;

                    var totalIngresos = await _context.MovimientosCaja
                        .Where(m =>
                            m.CajaId == caja.Id &&
                            m.Tipo == TipoMovimientoCaja.Ingreso)
                        .SumAsync(m => (decimal?)m.Monto) ?? 0m;

                    var totalEgresos = await _context.MovimientosCaja
                        .Where(m =>
                            m.CajaId == caja.Id &&
                            m.Tipo == TipoMovimientoCaja.Egreso)
                        .SumAsync(m => (decimal?)m.Monto) ?? 0m;

                    var efectivoProyectado = checked(
                        caja.MontoInicial +
                        cobrosEfectivo +
                        totalIngresos -
                        totalEgresos +
                        datos.Monto);

                    if (efectivoProyectado >= limiteDinero)
                        return Conflict(
                            "El pago supera el límite de efectivo permitido en la caja.");
                }

                var pago = new PagoVenta
                {
                    VentaId = ventaId,
                    MetodoPagoId = metodoPago.Id,
                    CajaId = caja.Id,
                    Monto = datos.Monto,
                    ReferenciaOperacion =
                        string.IsNullOrWhiteSpace(referencia)
                            ? null
                            : referencia,
                    FechaPago = DateTime.UtcNow
                };

                _context.PagosVenta.Add(pago);

                await _context.SaveChangesAsync();

                totalPagado += pago.Monto;

                saldoPendiente = venta.Total - totalPagado;

                var estadoPago = saldoPendiente == 0
                    ? "Pagada"
                    : "Pago parcial";

                _auditoriaService.Registrar(
                    usuarioId,
                    "REGISTRAR_PAGO",
                    "PagoVenta",
                    pago.Id.ToString(),
                    $"Venta ID: {pago.VentaId}. " +
                    $"Caja ID: {pago.CajaId}. " +
                    $"Método de pago ID: {pago.MetodoPagoId}. " +
                    $"Es efectivo: {metodoPago.EsEfectivo}. " +
                    $"Monto abonado: {pago.Monto}. " +
                    $"Total venta: {venta.Total}. " +
                    $"Total pagado: {totalPagado}. " +
                    $"Saldo pendiente: {saldoPendiente}. " +
                    $"Estado: {estadoPago}.");

                await _context.SaveChangesAsync();

                await transaccion.CommitAsync();

                return StatusCode(201, new
                {
                    mensaje = "Pago registrado correctamente.",
                    pago.Id,
                    pago.VentaId,
                    pago.CajaId,
                    pago.MetodoPagoId,
                    metodoPago = metodoPago.Nombre,
                    metodoPago.EsEfectivo,
                    pago.Monto,
                    pago.ReferenciaOperacion,
                    pago.FechaPago,
                    totalVenta = venta.Total,
                    totalPagado,
                    saldoPendiente,
                    estadoPago
                });
            }
            catch (OverflowException)
            {
                return Conflict(
                    "Los importes superan los límites permitidos.");
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(
                    "Los datos fueron modificados por otra operación.");
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is SqlException sqlEx &&
                sqlEx.Number == 547)
            {
                return Conflict(
                    "No se pudo registrar el pago debido a registros relacionados.");
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

        [Permiso(Permisos.VentasVer)]
        [HttpGet]
        public async Task<IActionResult> ObtenerPagosVenta(int ventaId)
        {
            if (ventaId <= 0)
                return BadRequest("El ID de la venta debe ser mayor que cero.");

            var venta = await _context.Ventas
                .AsNoTracking()
                .Where(v => v.Id == ventaId)
                .Select(v => new
                {
                    v.Id,
                    v.Estado,
                    v.Total
                })
                .FirstOrDefaultAsync();

            if (venta == null)
                return NotFound("Venta no encontrada.");

            var pagos = await _context.PagosVenta
                .AsNoTracking()
                .Where(p => p.VentaId == ventaId)
                .OrderBy(p => p.FechaPago)
                .ThenBy(p => p.Id)
                .Select(p => new
                {
                    p.Id,
                    p.CajaId,
                    p.MetodoPagoId,
                    MetodoPago = p.MetodoPago.Nombre,
                    p.MetodoPago.EsEfectivo,
                    p.Monto,
                    p.ReferenciaOperacion,
                    p.FechaPago
                })
                .ToListAsync();

            var totalPagado = pagos.Sum(p => p.Monto);

            var saldoPendiente = venta.Estado == "Confirmada"
                ? venta.Total - totalPagado
                : 0m;

            var estadoPago = venta.Estado switch
            {
                "Anulada" => "No corresponde",
                "Borrador" => "Sin confirmar",
                _ => saldoPendiente == 0
                    ? "Pagada"
                    : totalPagado == 0
                        ? "Pendiente"
                        : "Pago parcial"
            };

            return Ok(new
            {
                ventaId = venta.Id,
                estadoVenta = venta.Estado,
                totalVenta = venta.Total,
                totalPagado,
                saldoPendiente,
                estadoPago,
                cantidadPagos = pagos.Count,
                pagos
            });
        }
    }
}