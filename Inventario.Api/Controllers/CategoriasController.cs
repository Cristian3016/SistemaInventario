
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

            if (nuevaCategoria.CategoriaPadreId.HasValue)
            {
                if (nuevaCategoria.CategoriaPadreId.Value <= 0)
                {
                    return BadRequest("La categoría padre no es válida.");
                }

                var existePadre = await _context.Categorias
                    .AnyAsync(c => c.Id == nuevaCategoria.CategoriaPadreId.Value);

                if (!existePadre)
                {
                    return BadRequest("La categoría padre indicada no existe.");
                }
            }

            var existe = await _context.Categorias
                .AnyAsync(c =>
                    c.Nombre.ToLower() == nombre.ToLower() &&
                    c.CategoriaPadreId == nuevaCategoria.CategoriaPadreId);

            if (existe)
            {
                return Conflict("Ya existe una categoría con ese nombre en el mismo nivel.");
            }

            var categoria = new Categoria
            {
                Nombre = nombre,
                Descripcion = nuevaCategoria.Descripcion?.Trim() ?? string.Empty,
                CategoriaPadreId = nuevaCategoria.CategoriaPadreId
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

            if (categoriaActualizada.CategoriaPadreId.HasValue)
            {
                var padreId = categoriaActualizada.CategoriaPadreId.Value;

                if (padreId <= 0)
                {
                    return BadRequest("La categoría padre no es válida.");
                }

                if (padreId == id)
                {
                    return BadRequest("Una categoría no puede ser su propia categoría padre.");
                }

                var existePadre = await _context.Categorias
                    .AnyAsync(c => c.Id == padreId);

                if (!existePadre)
                {
                    return BadRequest("La categoría padre indicada no existe.");
                }

                int? ancestroId = padreId;
                var visitados = new HashSet<int>();

                while (ancestroId.HasValue)
                {
                    if (ancestroId.Value == id)
                    {
                        return BadRequest("No se puede crear una relación circular entre categorías.");
                    }

                    if (!visitados.Add(ancestroId.Value))
                    {
                        return BadRequest("Se detectó una relación circular entre categorías.");
                    }

                    var actual = ancestroId.Value;

                    ancestroId = await _context.Categorias
                        .Where(c => c.Id == actual)
                        .Select(c => c.CategoriaPadreId)
                        .FirstOrDefaultAsync();
                }
            }

            var existe = await _context.Categorias
                .AnyAsync(c =>
                    c.Id != id &&
                    c.Nombre.ToLower() == nombre.ToLower() &&
                    c.CategoriaPadreId == categoriaActualizada.CategoriaPadreId);

            if (existe)
            {
                return Conflict("Ya existe otra categoría con ese nombre en el mismo nivel.");
            }

            categoria.Nombre = nombre;
            categoria.Descripcion = categoriaActualizada.Descripcion?.Trim() ?? string.Empty;
            categoria.CategoriaPadreId = categoriaActualizada.CategoriaPadreId;

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

            var tieneSubcategorias = await _context.Categorias
                .AnyAsync(c => c.CategoriaPadreId == id);

            if (tieneSubcategorias)
            {
                return Conflict("No se puede eliminar una categoría que tiene subcategorías asociadas.");
            }

            _context.Categorias.Remove(categoria);

            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}

