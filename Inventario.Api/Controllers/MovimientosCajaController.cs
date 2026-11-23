
using Inventario.Api.Data;
using Inventario.Api.DTOs;
using Inventario.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Api.Controllers
{
    [ApiController]
    [Route("api/cajas/{cajaId:int}/movimientos")]
    public class MovimientosCajaController : ControllerBase
    {
        private readonly InventarioDbContext _context;

        public MovimientosCajaController(InventarioDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> RegistrarMovimiento(
            int cajaId,
            [FromBody] RegistrarMovimientoCajaDto datos)
        {
            if (cajaId <= 0)
                return BadRequest("El ID de la caja debe ser mayor que cero.");

            if (datos == null)
                return BadRequest("Debes proporcionar los datos del movimiento.");

            if (!Enum.IsDefined(typeof(TipoMovimientoCaja), datos.Tipo))
                return BadRequest("El tipo de movimiento no es válido.");

            if (datos.Monto <= 0)
                return BadRequest("El monto debe ser mayor que cero.");

            if (decimal.Round(datos.Monto, 2) != datos.Monto)
                return BadRequest("El monto no puede superar dos decimales.");

            const decimal limiteDinero = 10000000000000000m;

            if (datos.Monto >= limiteDinero)
                return BadRequest("El monto supera el límite permitido.");

            var concepto = datos.Concepto?.Trim();

            if (string.IsNullOrWhiteSpace(concepto))
                return BadRequest("El concepto es obligatorio.");

            if (concepto.Length > 200)
                return BadRequest("El concepto no puede superar los 200 caracteres.");

            var observacion = datos.Observacion?.Trim();

            if (observacion?.Length > 500)
                return BadRequest("La observación no puede superar los 500 caracteres.");

            await using var transaccion =
                await _context.Database.BeginTransactionAsync();

            var caja = await _context.Cajas
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == cajaId);

            if (caja == null)
                return NotFound("Caja no encontrada.");

            if (caja.Estado != "Abierta")
                return Conflict("Solo se pueden registrar movimientos en cajas abiertas.");

            var cajaBloqueada = await _context.Cajas
                .Where(c =>
                    c.Id == cajaId &&
                    c.Estado == "Abierta")
                .ExecuteUpdateAsync(cambios =>
                    cambios.SetProperty(
                        c => c.Estado,
                        c => c.Estado));

            if (cajaBloqueada == 0)
                return Conflict("La caja ya no se encuentra abierta.");

            var cobrosEfectivo = await _context.PagosVenta
                .Where(p =>
                    p.CajaId == cajaId &&
                    p.MetodoPago.EsEfectivo)
                .SumAsync(p => (decimal?)p.Monto) ?? 0m;

            var totalIngresos = await _context.MovimientosCaja
                .Where(m =>
                    m.CajaId == cajaId &&
                    m.Tipo == TipoMovimientoCaja.Ingreso)
                .SumAsync(m => (decimal?)m.Monto) ?? 0m;

            var totalEgresos = await _context.MovimientosCaja
                .Where(m =>
                    m.CajaId == cajaId &&
                    m.Tipo == TipoMovimientoCaja.Egreso)
                .SumAsync(m => (decimal?)m.Monto) ?? 0m;

            var efectivoDisponible =
                caja.MontoInicial +
                cobrosEfectivo +
                totalIngresos -
                totalEgresos;

            if (datos.Tipo == TipoMovimientoCaja.Egreso &&
                datos.Monto > efectivoDisponible)
            {
                return Conflict(new
                {
                    mensaje = "El egreso supera el efectivo disponible en caja.",
                    efectivoDisponible,
                    montoSolicitado = datos.Monto
                });
            }

            var nuevoEfectivo = datos.Tipo == TipoMovimientoCaja.Ingreso
                ? efectivoDisponible + datos.Monto
                : efectivoDisponible - datos.Monto;

            if (nuevoEfectivo >= limiteDinero)
                return Conflict("El efectivo disponible supera el límite permitido.");

            var movimiento = new MovimientoCaja
            {
                CajaId = cajaId,
                Tipo = datos.Tipo,
                Monto = datos.Monto,
                Concepto = concepto,
                Observacion = string.IsNullOrWhiteSpace(observacion)
                    ? null
                    : observacion,
                FechaMovimiento = DateTime.UtcNow
            };

            _context.MovimientosCaja.Add(movimiento);

            await _context.SaveChangesAsync();

            await transaccion.CommitAsync();

            return StatusCode(201, new
            {
                mensaje = "Movimiento de caja registrado correctamente.",
                movimiento.Id,
                movimiento.CajaId,
                movimiento.Tipo,
                tipoMovimiento = movimiento.Tipo.ToString(),
                movimiento.Monto,
                movimiento.Concepto,
                movimiento.Observacion,
                movimiento.FechaMovimiento,
                efectivoAnterior = efectivoDisponible,
                efectivoDisponible = nuevoEfectivo
            });
        }
        [HttpGet]
        public async Task<IActionResult> ObtenerMovimientosCaja(
            int cajaId,
            [FromQuery] TipoMovimientoCaja? tipo)
        {
            if (cajaId <= 0)
                return BadRequest("El ID de la caja debe ser mayor que cero.");

            if (tipo.HasValue &&
                !Enum.IsDefined(typeof(TipoMovimientoCaja), tipo.Value))
                return BadRequest("El tipo de movimiento no es válido.");

            var existeCaja = await _context.Cajas
                .AnyAsync(c => c.Id == cajaId);

            if (!existeCaja)
                return NotFound("Caja no encontrada.");

            var consulta = _context.MovimientosCaja
                .AsNoTracking()
                .Where(m => m.CajaId == cajaId);

            if (tipo.HasValue)
                consulta = consulta.Where(m => m.Tipo == tipo.Value);

            var movimientos = await consulta
                .OrderByDescending(m => m.FechaMovimiento)
                .ThenByDescending(m => m.Id)
                .Select(m => new
                {
                    m.Id,
                    m.CajaId,
                    m.Tipo,
                    m.Monto,
                    m.Concepto,
                    m.Observacion,
                    m.FechaMovimiento
                })
                .ToListAsync();

            return Ok(movimientos.Select(m => new
            {
                m.Id,
                m.CajaId,
                m.Tipo,
                tipoMovimiento = m.Tipo.ToString(),
                m.Monto,
                m.Concepto,
                m.Observacion,
                m.FechaMovimiento
            }));
        }

    }
}


