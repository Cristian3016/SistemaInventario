
using Inventario.Api.Data;
using Inventario.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MarcasController : ControllerBase
    {
        private readonly InventarioDbContext _context;

        public MarcasController(InventarioDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Marca>>> ObtenerMarcas()
        {
            var marcas = await _context.Marcas
                .AsNoTracking()
                .OrderBy(m => m.Nombre)
                .ToListAsync();

            return Ok(marcas);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<Marca>> ObtenerMarcaPorId(int id)
        {
            var marca = await _context.Marcas
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == id);

            if (marca == null)
            {
                return NotFound("Marca no encontrada.");
            }

            return Ok(marca);
        }

        [HttpPost]
        public async Task<ActionResult<Marca>> CrearMarca(
            [FromBody] Marca nuevaMarca)
        {
            if (string.IsNullOrWhiteSpace(nuevaMarca.Nombre))
            {
                return BadRequest("El nombre de la marca es obligatorio.");
            }

            var nombre = nuevaMarca.Nombre.Trim();

            var existeMarca = await _context.Marcas
                .AnyAsync(m => m.Nombre.ToLower() == nombre.ToLower());

            if (existeMarca)
            {
                return Conflict("Ya existe una marca con ese nombre.");
            }

            var marca = new Marca
            {
                Nombre = nombre,
                Descripcion = nuevaMarca.Descripcion?.Trim() ?? string.Empty
            };

            _context.Marcas.Add(marca);

            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(ObtenerMarcaPorId),
                new { id = marca.Id },
                marca);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> ActualizarMarca(
            int id, [FromBody] Marca marcaActualizada)
        {
            var marca = await _context.Marcas.FindAsync(id);

            if (marca == null)
            {
                return NotFound("Marca no encontrada.");
            }

            if (string.IsNullOrWhiteSpace(marcaActualizada.Nombre))
            {
                return BadRequest("El nombre de la marca es obligatorio.");
            }

            var nombre = marcaActualizada.Nombre.Trim();

            var existeMarca = await _context.Marcas
                .AnyAsync(m => m.Id != id &&
                               m.Nombre.ToLower() == nombre.ToLower());

            if (existeMarca)
            {
                return Conflict("Ya existe otra marca con ese nombre.");
            }

            marca.Nombre = nombre;
            marca.Descripcion = marcaActualizada.Descripcion?.Trim()
                                ?? string.Empty;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> EliminarMarca(int id)
        {
            var marca = await _context.Marcas.FindAsync(id);

            if (marca == null)
            {
                return NotFound("Marca no encontrada.");
            }

            var tieneProductos = await _context.Productos
                .AnyAsync(p => p.MarcaId == id);

            if (tieneProductos)
            {
                return Conflict(
                    "No se puede eliminar una marca que tiene productos asociados.");
            }

            _context.Marcas.Remove(marca);

            await _context.SaveChangesAsync();

            return NoContent();
        }

    }

}

