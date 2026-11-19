
using Inventario.Api.Data;
using Inventario.Api.DTOs;
using Inventario.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MovimientosStockController : ControllerBase
    {
        private readonly InventarioDbContext _context;

        public MovimientosStockController(InventarioDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerMovimientos()
        {
            var movimientos = await _context.MovimientosStock
                .AsNoTracking()
                .OrderByDescending(m => m.FechaMovimiento)
                .Select(m => new
                {
                    m.Id,
                    m.ProductoId,
                    NombreProducto = m.Producto.Nombre,
                    m.Tipo,
                    m.Cantidad,
                    m.Observacion,
                    m.FechaMovimiento
                })
                .ToListAsync();

            return Ok(movimientos);
        }

        [HttpPost]
        public async Task<IActionResult> RegistrarMovimiento(
            [FromBody] RegistrarMovimientoStockDto datos)
        {
            if (datos.ProductoId <= 0)
            {
                return BadRequest("El producto indicado no es válido.");
            }

            if (datos.Tipo != TipoMovimiento.Entrada &&
                datos.Tipo != TipoMovimiento.Salida)
            {
                return BadRequest("El tipo de movimiento no es válido.");
            }

            if (datos.Cantidad <= 0)
            {
                return BadRequest("La cantidad debe ser mayor que cero.");
            }

            if (decimal.Round(datos.Cantidad, 3) != datos.Cantidad)
            {
                return BadRequest("La cantidad no puede superar tres decimales.");
            }

            var producto = await _context.Productos
                .AsNoTracking()
                .Where(p => p.Id == datos.ProductoId)
                .Select(p => new
                {
                    p.Activo,
                    p.UnidadMedidaId,
                    PermiteFracciones = p.UnidadMedida != null &&
                                       p.UnidadMedida.PermiteFracciones
                })
                .FirstOrDefaultAsync();

            if (producto == null)
            {
                return NotFound("Producto no encontrado.");
            }

            if (!producto.Activo)
            {
                return Conflict(
                    "No se pueden registrar movimientos de stock en productos inactivos.");
            }

            if (datos.Cantidad != decimal.Truncate(datos.Cantidad))
            {
                if (!producto.UnidadMedidaId.HasValue)
                {
                    return BadRequest(
                        "El producto no tiene una unidad de medida asignada para registrar cantidades decimales.");
                }

                if (!producto.PermiteFracciones)
                {
                    return BadRequest(
                        "La unidad de medida de este producto no permite cantidades decimales.");
                }
            }

            await using var transaccion =
                await _context.Database.BeginTransactionAsync();

            int filasActualizadas;

            if (datos.Tipo == TipoMovimiento.Entrada)
            {
                const decimal stockMaximo = 999999999999999.999m;

                filasActualizadas = await _context.Productos
                    .Where(p => p.Id == datos.ProductoId &&
                                p.Activo &&
                                p.Stock <= stockMaximo - datos.Cantidad)
                    .ExecuteUpdateAsync(cambios =>
                        cambios.SetProperty(
                            p => p.Stock,
                            p => p.Stock + datos.Cantidad));
            }
            else
            {
                filasActualizadas = await _context.Productos
                    .Where(p => p.Id == datos.ProductoId &&
                                p.Activo &&
                                p.Stock >= datos.Cantidad)
                    .ExecuteUpdateAsync(cambios =>
                        cambios.SetProperty(
                            p => p.Stock,
                            p => p.Stock - datos.Cantidad));
            }

            if (filasActualizadas == 0)
            {
                var sigueActivo = await _context.Productos
                    .AsNoTracking()
                    .Where(p => p.Id == datos.ProductoId)
                    .Select(p => p.Activo)
                    .FirstAsync();

                if (!sigueActivo)
                {
                    return Conflict(
                        "No se pueden registrar movimientos de stock en productos inactivos.");
                }

                return Conflict(
                    datos.Tipo == TipoMovimiento.Salida
                        ? "Stock insuficiente para registrar la salida."
                        : "No se pudo registrar la entrada de mercadería.");
            }

            var movimiento = new MovimientoStock
            {
                ProductoId = datos.ProductoId,
                Tipo = datos.Tipo,
                Cantidad = datos.Cantidad,
                Observacion = datos.Observacion?.Trim() ?? string.Empty,
                FechaMovimiento = DateTime.UtcNow
            };

            _context.MovimientosStock.Add(movimiento);

            await _context.SaveChangesAsync();

            var stockActual = await _context.Productos
                .Where(p => p.Id == datos.ProductoId)
                .Select(p => p.Stock)
                .SingleAsync();

            await transaccion.CommitAsync();

            return StatusCode(
                StatusCodes.Status201Created,
                new
                {
                    mensaje = "Movimiento registrado correctamente.",
                    movimientoId = movimiento.Id,
                    productoId = movimiento.ProductoId,
                    tipo = movimiento.Tipo,
                    cantidad = movimiento.Cantidad,
                    stockActual
                });
        }
    }
}




