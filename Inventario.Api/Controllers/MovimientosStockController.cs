
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

            var existeProducto = await _context.Productos
                .AnyAsync(p => p.Id == datos.ProductoId);

            if (!existeProducto)
            {
                return NotFound("Producto no encontrado.");
            }

            await using var transaccion = await _context.Database.BeginTransactionAsync();

            int filasActualizadas;

            if (datos.Tipo == TipoMovimiento.Entrada)
            {
                filasActualizadas = await _context.Productos
                    .Where(p => p.Id == datos.ProductoId &&
                                p.Stock <= int.MaxValue - datos.Cantidad)
                    .ExecuteUpdateAsync(cambios =>
                        cambios.SetProperty(
                            p => p.Stock,
                            p => p.Stock + datos.Cantidad));
            }
            else
            {
                filasActualizadas = await _context.Productos
                    .Where(p => p.Id == datos.ProductoId &&
                                p.Stock >= datos.Cantidad)
                    .ExecuteUpdateAsync(cambios =>
                        cambios.SetProperty(
                            p => p.Stock,
                            p => p.Stock - datos.Cantidad));
            }

            if (filasActualizadas == 0)
            {
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

            return StatusCode(StatusCodes.Status201Created, new
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


