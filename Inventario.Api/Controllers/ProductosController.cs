
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

            if (nuevoProducto.Precio < 0 || nuevoProducto.Stock < 0 || nuevoProducto.StockMinimo < 0)
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

            var producto = new Producto
            {
                Nombre = nuevoProducto.Nombre.Trim(),
                Descripcion = nuevoProducto.Descripcion ?? string.Empty,
                CodigoSku = string.IsNullOrWhiteSpace(nuevoProducto.CodigoSku)
                    ? null
                    : nuevoProducto.CodigoSku.Trim(),
                Precio = nuevoProducto.Precio,
                Stock = nuevoProducto.Stock,
                StockMinimo = nuevoProducto.StockMinimo,
                CategoriaId = nuevoProducto.CategoriaId,
                FechaCreacion = DateTime.UtcNow
            };

            _context.Productos.Add(producto);

            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(ObtenerProductoPorId),
                new { id = producto.Id },
                producto);
        }




        [HttpPut("{id:int}")]
        public async Task<IActionResult> ActualizarProducto(
            int id,
            [FromBody] Producto productoActualizado)
        {
            var producto = await _context.Productos.FindAsync(id);

            if (producto == null)
            {
                return NotFound("Producto no encontrado.");
            }

            if (string.IsNullOrWhiteSpace(productoActualizado.Nombre))
            {
                return BadRequest("El nombre es obligatorio.");
            }

            if (productoActualizado.Precio < 0 ||
                productoActualizado.Stock < 0)
            {
                return BadRequest(
                    "El precio y el stock no pueden ser negativos.");
            }

            producto.Nombre = productoActualizado.Nombre.Trim();
            producto.Descripcion = productoActualizado.Descripcion
                ?? string.Empty;
            producto.Precio = productoActualizado.Precio;
            producto.Stock = productoActualizado.Stock;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpPatch("{id:int}")]
        public async Task<IActionResult> ActualizarParcialProducto(
            int id,
            [FromBody] ProductoPatchDto datosPatch)
        {
            var producto = await _context.Productos.FindAsync(id);

            if (producto == null)
            {
                return NotFound("Producto no encontrado.");
            }

            if (datosPatch.Nombre == null &&
                datosPatch.Descripcion == null &&
                datosPatch.Precio == null &&
                datosPatch.Stock == null)
            {
                return BadRequest(
                    "Debes enviar al menos un campo para actualizar.");
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

            if (datosPatch.Stock.HasValue &&
                datosPatch.Stock.Value < 0)
            {
                return BadRequest("El stock no puede ser negativo.");
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

            if (datosPatch.Stock.HasValue)
            {
                producto.Stock = datosPatch.Stock.Value;
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

            _context.Productos.Remove(producto);

            await _context.SaveChangesAsync();

            return NoContent();
        }

    }
}

