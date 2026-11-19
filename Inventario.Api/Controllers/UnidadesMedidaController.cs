
using Inventario.Api.Data;
using Inventario.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UnidadesMedidaController : ControllerBase
    {
        private readonly InventarioDbContext _context;

        public UnidadesMedidaController(InventarioDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<UnidadMedida>>> ObtenerUnidades()
        {
            var unidades = await _context.UnidadesMedida
                .AsNoTracking()
                .OrderBy(u => u.Nombre)
                .ToListAsync();

            return Ok(unidades);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<UnidadMedida>> ObtenerUnidadPorId(int id)
        {
            var unidad = await _context.UnidadesMedida
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == id);

            if (unidad == null)
            {
                return NotFound("Unidad de medida no encontrada.");
            }

            return Ok(unidad);
        }

        [HttpPost]
        public async Task<ActionResult<UnidadMedida>> CrearUnidad(
            [FromBody] UnidadMedida nuevaUnidad)
        {
            if (string.IsNullOrWhiteSpace(nuevaUnidad.Nombre) ||
                string.IsNullOrWhiteSpace(nuevaUnidad.Abreviatura))
            {
                return BadRequest("El nombre y la abreviatura son obligatorios.");
            }

            var nombre = nuevaUnidad.Nombre.Trim();
            var abreviatura = nuevaUnidad.Abreviatura.Trim();

            var existe = await _context.UnidadesMedida
                .AnyAsync(u =>
                    u.Nombre.ToLower() == nombre.ToLower() ||
                    u.Abreviatura.ToLower() == abreviatura.ToLower());

            if (existe)
            {
                return Conflict("Ya existe una unidad de medida con ese nombre o abreviatura.");
            }

            var unidad = new UnidadMedida
            {
                Nombre = nombre,
                Abreviatura = abreviatura,
                PermiteFracciones = nuevaUnidad.PermiteFracciones
            };

            _context.UnidadesMedida.Add(unidad);

            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(ObtenerUnidadPorId),
                new { id = unidad.Id },
                unidad);
        }
    }
}
