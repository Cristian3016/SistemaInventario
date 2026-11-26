using Inventario.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Inventario.Api.Authorization;

namespace Inventario.Api.Controllers
{
    [ApiController]
    [Route("api/permisos")]
    public class PermisosController : ControllerBase
    {
        private readonly InventarioDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public PermisosController(
            InventarioDbContext context,
            IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        [Permiso(Permisos.PermisosGestionar)]
        [HttpGet]
        public async Task<IActionResult> ObtenerPermisos(
            [FromQuery] bool? activo,
            [FromQuery] string? buscar)
        {
            if (!_environment.IsDevelopment())
                return NotFound();

            var consulta = _context.Permisos.AsNoTracking();

            if (activo.HasValue)
                consulta = consulta.Where(p =>
                    p.Activo == activo.Value);

            if (!string.IsNullOrWhiteSpace(buscar))
            {
                var termino = buscar.Trim().ToLowerInvariant();

                consulta = consulta.Where(p =>
                    p.Nombre.ToLower().Contains(termino) ||
                    p.Codigo.ToLower().Contains(termino) ||
                    p.Descripcion.ToLower().Contains(termino));
            }

            var permisos = await consulta
                .OrderBy(p => p.Nombre)
                .Select(p => new
                {
                    p.Id,
                    p.Nombre,
                    p.Codigo,
                    p.Descripcion,
                    p.Activo,
                    p.FechaCreacion
                })
                .ToListAsync();

            return Ok(permisos);
        }

        [Permiso(Permisos.PermisosGestionar)]
        [HttpGet("{id:int}")]
        public async Task<IActionResult> ObtenerPermisoPorId(int id)
        {
            if (!_environment.IsDevelopment())
                return NotFound();

            if (id <= 0)
                return BadRequest(
                    "El ID del permiso debe ser mayor que cero.");

            var permiso = await _context.Permisos
                .AsNoTracking()
                .Where(p => p.Id == id)
                .Select(p => new
                {
                    p.Id,
                    p.Nombre,
                    p.Codigo,
                    p.Descripcion,
                    p.Activo,
                    p.FechaCreacion
                })
                .FirstOrDefaultAsync();

            if (permiso == null)
                return NotFound("Permiso no encontrado.");

            return Ok(permiso);
        }
    }
}