
using Inventario.Api.Data;
using Inventario.Api.DTOs;
using Inventario.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Api.Controllers
{
    [ApiController]
    [Route("api/ventas/{ventaId:int}/pagos")]
    public class PagosVentaController : ControllerBase
    {
        private readonly InventarioDbContext _context;

        public PagosVentaController(InventarioDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> RegistrarPago(
            int ventaId,
            [FromBody] RegistrarPagoVentaDto datos)
        {
            if (ventaId <= 0)
                return BadRequest("El ID de la venta debe ser mayor que cero.");

            if (datos == null)
                return BadRequest("Debes proporcionar los datos del pago.");

            if (datos.MetodoPagoId <= 0)
                return BadRequest("El ID del método de pago debe ser mayor que cero.");

            if (datos.Monto <= 0)
                return BadRequest("El monto debe ser mayor que cero.");

            if (decimal.Round(datos.Monto, 2) != datos.Monto)
                return BadRequest("El monto no puede superar dos decimales.");

            const decimal limiteDinero = 10000000000000000m;

            if (datos.Monto >= limiteDinero)
                return BadRequest("El monto supera el límite permitido.");

            var referencia = datos.ReferenciaOperacion?.Trim();

            if (referencia?.Length > 150)
                return BadRequest("La referencia no puede superar los 150 caracteres.");

            await using var transaccion =
                await _context.Database.BeginTransactionAsync();

            var caja = await _context.Cajas
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Estado == "Abierta");

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
                return Conflict("La caja ya no se encuentra abierta.");

            var venta = await _context.Ventas
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == ventaId);

            if (venta == null)
                return NotFound("Venta no encontrada.");

            if (venta.Estado != "Confirmada")
                return Conflict("Solo se pueden registrar pagos de ventas confirmadas.");

            var metodoPago = await _context.MetodosPago
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == datos.MetodoPagoId);

            if (metodoPago == null)
                return NotFound("Método de pago no encontrado.");

            if (!metodoPago.Activo)
                return Conflict("El método de pago está inactivo.");

            var totalPagado = await _context.PagosVenta
                .Where(p => p.VentaId == ventaId)
                .SumAsync(p => (decimal?)p.Monto) ?? 0m;

            var saldoPendiente = venta.Total - totalPagado;

            if (saldoPendiente <= 0)
                return Conflict("La venta ya se encuentra totalmente abonada.");

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

            var pago = new PagoVenta
            {
                VentaId = ventaId,
                MetodoPagoId = metodoPago.Id,
                CajaId = caja.Id,
                Monto = datos.Monto,
                ReferenciaOperacion = string.IsNullOrWhiteSpace(referencia)
                    ? null
                    : referencia,
                FechaPago = DateTime.UtcNow
            };

            _context.PagosVenta.Add(pago);

            await _context.SaveChangesAsync();

            totalPagado += pago.Monto;

            saldoPendiente = venta.Total - totalPagado;

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
                estadoPago = saldoPendiente == 0
                    ? "Pagada"
                    : "Pago parcial"
            });
        }

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
