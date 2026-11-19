
using Inventario.Api.Data;
using Inventario.Api.DTOs;
using Inventario.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductosController : ControllerBase
    {
        private readonly InventarioDbContext _context;

        public ProductosController(InventarioDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Producto>>> ObtenerProductos()
        {
            var productos = await _context.Productos
                .AsNoTracking()
                .ToListAsync();

            return Ok(productos);
        }

        [HttpGet("stock-bajo")]
        public async Task<IActionResult> ObtenerProductosConStockBajo()
        {
            var productos = await _context.Productos
                .AsNoTracking()
                .Where(p => p.StockMinimo > 0 && p.Stock <= p.StockMinimo)
                .OrderBy(p => p.Stock)
                .ToListAsync();

            if (productos.Count == 0)
            {
                return Ok(new
                {
                    mensaje = "No hay productos con stock bajo.",
                    productos
                });
            }

            return Ok(new
            {
                mensaje = "¡Atención! Hay productos con stock bajo. Es necesario reponer mercadería.",
                cantidadProductos = productos.Count,
                productos
            });
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<Producto>> ObtenerProductoPorId(int id)
        {
            var producto = await _context.Productos
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id);

            if (producto == null)
            {
                return NotFound("Producto no encontrado.");
            }

            return Ok(producto);
        }


        [HttpPost]
        public async Task<ActionResult<Producto>> CrearProducto([FromBody] Producto nuevoProducto)
        {
            if (string.IsNullOrWhiteSpace(nuevoProducto.Nombre))
            {
                return BadRequest("El nombre es obligatorio.");
            }

            if (nuevoProducto.Precio < 0 ||
                nuevoProducto.Stock < 0 ||
                nuevoProducto.StockMinimo < 0)
            {
                return BadRequest("El precio, el stock y el stock mínimo no pueden ser negativos.");
            }

            if (nuevoProducto.CategoriaId.HasValue)
            {
                var existeCategoria = await _context.Categorias
                    .AnyAsync(c => c.Id == nuevoProducto.CategoriaId.Value);

                if (!existeCategoria)
                {
                    return BadRequest("La categoría indicada no existe.");
                }
            }

            var codigoSku = string.IsNullOrWhiteSpace(nuevoProducto.CodigoSku)
                ? null
                : nuevoProducto.CodigoSku.Trim();

            if (codigoSku != null)
            {
                if (codigoSku.Length > 50)
                {
                    return BadRequest("El código SKU no puede superar los 50 caracteres.");
                }

                var existeSku = await _context.Productos
                    .AnyAsync(p => p.CodigoSku == codigoSku);

                if (existeSku)
                {
                    return Conflict("Ya existe un producto con ese código SKU.");
                }
            }

            var producto = new Producto
            {
                Nombre = nuevoProducto.Nombre.Trim(),
                Descripcion = nuevoProducto.Descripcion ?? string.Empty,
                CodigoSku = codigoSku,
                Precio = nuevoProducto.Precio,
                Stock = nuevoProducto.Stock,
                StockMinimo = nuevoProducto.StockMinimo,
                CategoriaId = nuevoProducto.CategoriaId,
                FechaCreacion = DateTime.UtcNow
            };

            _context.Productos.Add(producto);

            if (nuevoProducto.Stock > 0)
            {
                var movimientoInicial = new MovimientoStock
                {
                    Producto = producto,
                    Tipo = TipoMovimiento.Entrada,
                    Cantidad = nuevoProducto.Stock,
                    Observacion = "Registro de stock inicial del producto.",
                    FechaMovimiento = DateTime.UtcNow
                };

                _context.MovimientosStock.Add(movimientoInicial);
            }

            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(ObtenerProductoPorId),
                new { id = producto.Id },
                producto);
        }


        [HttpPut("{id:int}")]
        public async Task<IActionResult> ActualizarProducto(
            int id, [FromBody] Producto productoActualizado)
        {
            var producto = await _context.Productos.FindAsync(id);

            if (producto == null)
            {
                return NotFound("Producto no encontrado.");
            }

            if (productoActualizado.Stock != producto.Stock)
            {
                return BadRequest("El stock solo puede modificarse mediante movimientos de inventario.");
            }

            if (string.IsNullOrWhiteSpace(productoActualizado.Nombre))
            {
                return BadRequest("El nombre es obligatorio.");
            }

            if (productoActualizado.Precio < 0 ||
                productoActualizado.StockMinimo < 0)
            {
                return BadRequest("El precio y el stock mínimo no pueden ser negativos.");
            }

            var codigoSku = string.IsNullOrWhiteSpace(productoActualizado.CodigoSku)
                ? null
                : productoActualizado.CodigoSku.Trim();

            if (codigoSku != null)
            {
                if (codigoSku.Length > 50)
                {
                    return BadRequest("El código SKU no puede superar los 50 caracteres.");
                }

                var existeSku = await _context.Productos
                    .AnyAsync(p => p.CodigoSku == codigoSku && p.Id != id);

                if (existeSku)
                {
                    return Conflict("Ya existe otro producto con ese código SKU.");
                }
            }

            if (productoActualizado.CategoriaId.HasValue)
            {
                var existeCategoria = await _context.Categorias
                    .AnyAsync(c => c.Id == productoActualizado.CategoriaId.Value);

                if (!existeCategoria)
                {
                    return BadRequest("La categoría indicada no existe.");
                }
            }

            producto.Nombre = productoActualizado.Nombre.Trim();
            producto.Descripcion = productoActualizado.Descripcion ?? string.Empty;
            producto.CodigoSku = codigoSku;
            producto.Precio = productoActualizado.Precio;
            producto.StockMinimo = productoActualizado.StockMinimo;
            producto.CategoriaId = productoActualizado.CategoriaId;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpPatch("{id:int}")]
        public async Task<IActionResult> ActualizarParcialProducto(
            int id, [FromBody] ProductoPatchDto datosPatch)
        {
            var producto = await _context.Productos.FindAsync(id);

            if (producto == null)
            {
                return NotFound("Producto no encontrado.");
            }

            if (datosPatch.Nombre == null &&
                datosPatch.Descripcion == null &&
                datosPatch.CodigoSku == null &&
                datosPatch.Precio == null &&
                datosPatch.Stock == null &&
                datosPatch.StockMinimo == null &&
                datosPatch.CategoriaId == null)
            {
                return BadRequest("Debes enviar al menos un campo para actualizar.");
            }

            if (datosPatch.Stock.HasValue)
            {
                return BadRequest("El stock solo puede modificarse mediante movimientos de inventario.");
            }

            if (datosPatch.Nombre != null &&
                string.IsNullOrWhiteSpace(datosPatch.Nombre))
            {
                return BadRequest("El nombre no puede estar vacío.");
            }

            if (datosPatch.Precio.HasValue &&
                datosPatch.Precio.Value < 0)
            {
                return BadRequest("El precio no puede ser negativo.");
            }

            if (datosPatch.StockMinimo.HasValue &&
                datosPatch.StockMinimo.Value < 0)
            {
                return BadRequest("El stock mínimo no puede ser negativo.");
            }

            if (datosPatch.CodigoSku != null)
            {
                var codigoSku = string.IsNullOrWhiteSpace(datosPatch.CodigoSku)
                    ? null
                    : datosPatch.CodigoSku.Trim();

                if (codigoSku != null)
                {
                    if (codigoSku.Length > 50)
                    {
                        return BadRequest("El código SKU no puede superar los 50 caracteres.");
                    }

                    var existeSku = await _context.Productos
                        .AnyAsync(p => p.CodigoSku == codigoSku && p.Id != id);

                    if (existeSku)
                    {
                        return Conflict("Ya existe otro producto con ese código SKU.");
                    }
                }

                producto.CodigoSku = codigoSku;
            }

            if (datosPatch.CategoriaId.HasValue)
            {
                var existeCategoria = await _context.Categorias
                    .AnyAsync(c => c.Id == datosPatch.CategoriaId.Value);

                if (!existeCategoria)
                {
                    return BadRequest("La categoría indicada no existe.");
                }
            }

            if (datosPatch.Nombre != null)
            {
                producto.Nombre = datosPatch.Nombre.Trim();
            }

            if (datosPatch.Descripcion != null)
            {
                producto.Descripcion = datosPatch.Descripcion;
            }

            if (datosPatch.Precio.HasValue)
            {
                producto.Precio = datosPatch.Precio.Value;
            }

            if (datosPatch.StockMinimo.HasValue)
            {
                producto.StockMinimo = datosPatch.StockMinimo.Value;
            }

            if (datosPatch.CategoriaId.HasValue)
            {
                producto.CategoriaId = datosPatch.CategoriaId.Value;
            }

            await _context.SaveChangesAsync();

            return Ok(producto);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> EliminarProducto(int id)
        {
            var producto = await _context.Productos.FindAsync(id);

            if (producto == null)
            {
                return NotFound("Producto no encontrado.");
            }

            var tieneMovimientos = await _context.MovimientosStock
                .AnyAsync(m => m.ProductoId == id);

            if (tieneMovimientos)
            {
                return Conflict("No se puede eliminar un producto que tiene movimientos de stock registrados.");
            }

            _context.Productos.Remove(producto);

            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}

