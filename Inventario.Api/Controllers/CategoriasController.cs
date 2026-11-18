using Inventario.Api.Data;
using Inventario.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CategoriasController : ControllerBase
    {
        private readonly InventarioDbContext _context;

        public CategoriasController(InventarioDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Categoria>>> ObtenerCategorias()
        {
            var categorias = await _context.Categorias
                .AsNoTracking()
                .ToListAsync();

            return Ok(categorias);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<Categoria>> ObtenerCategoriaPorId(int id)
        {
            var categoria = await _context.Categorias
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id);

            if (categoria == null)
            {
                return NotFound("Categoría no encontrada.");
            }

            return Ok(categoria);
        }

        [HttpPost]
        public async Task<ActionResult<Categoria>> CrearCategoria(
            [FromBody] Categoria nuevaCategoria)
        {
            if (string.IsNullOrWhiteSpace(nuevaCategoria.Nombre))
            {
                return BadRequest("El nombre de la categoría es obligatorio.");
            }

            var nombre = nuevaCategoria.Nombre.Trim();

            var existe = await _context.Categorias
                .AnyAsync(c => c.Nombre.ToLower() == nombre.ToLower());

            if (existe)
            {
                return Conflict("Ya existe una categoría con ese nombre.");
            }

            var categoria = new Categoria
            {
                Nombre = nombre,
                Descripcion = nuevaCategoria.Descripcion ?? string.Empty
            };

            _context.Categorias.Add(categoria);

            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(ObtenerCategoriaPorId),
                new { id = categoria.Id },
                categoria);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> ActualizarCategoria(
            int id, [FromBody] Categoria categoriaActualizada)
        {
            var categoria = await _context.Categorias.FindAsync(id);

            if (categoria == null)
            {
                return NotFound("Categoría no encontrada.");
            }

            if (string.IsNullOrWhiteSpace(categoriaActualizada.Nombre))
            {
                return BadRequest("El nombre de la categoría es obligatorio.");
            }

            var nombre = categoriaActualizada.Nombre.Trim();

            var existe = await _context.Categorias
                .AnyAsync(c => c.Id != id &&
                               c.Nombre.ToLower() == nombre.ToLower());

            if (existe)
            {
                return Conflict("Ya existe otra categoría con ese nombre.");
            }

            categoria.Nombre = nombre;
            categoria.Descripcion = categoriaActualizada.Descripcion ?? string.Empty;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> EliminarCategoria(int id)
        {
            var categoria = await _context.Categorias.FindAsync(id);

            if (categoria == null)
            {
                return NotFound("Categoría no encontrada.");
            }

            var tieneProductos = await _context.Productos
                .AnyAsync(p => p.CategoriaId == id);

            if (tieneProductos)
            {
                return Conflict("No se puede eliminar una categoría que tiene productos asociados.");
            }

            _context.Categorias.Remove(categoria);

            await _context.SaveChangesAsync();

            return NoContent();
        }


    }
}

