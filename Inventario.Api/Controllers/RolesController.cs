
using Inventario.Api.Data;
using Inventario.Api.DTOs;
using Inventario.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Api.Controllers
{
    [ApiController]
    [Route("api/roles")]
    public class RolesController : ControllerBase
    {
        private readonly InventarioDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public RolesController(
            InventarioDbContext context,
            IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        [HttpPost]
        public async Task<IActionResult> CrearRol(
            [FromBody] CrearRolDto datos)
        {
            if (!_environment.IsDevelopment())
                return NotFound();

            if (datos == null)
                return BadRequest("Debes proporcionar los datos del rol.");

            var nombre = datos.Nombre?.Trim();
            var descripcion = datos.Descripcion?.Trim();

            if (string.IsNullOrWhiteSpace(nombre))
                return BadRequest("El nombre del rol es obligatorio.");

            if (nombre.Length > 100)
                return BadRequest("El nombre no puede superar los 100 caracteres.");

            if (string.IsNullOrWhiteSpace(descripcion))
                return BadRequest("La descripcion es obligatoria.");

            if (descripcion.Length > 300)
                return BadRequest("La descripcion no puede superar los 300 caracteres.");

            var existeRol = await _context.Roles
                .AnyAsync(r => r.Nombre.ToLower() == nombre.ToLower());

            if (existeRol)
                return Conflict("Ya existe un rol con ese nombre.");

            var rol = new Rol
            {
                Nombre = nombre,
                Descripcion = descripcion,
                Activo = true,
                FechaCreacion = DateTime.UtcNow
            };

            _context.Roles.Add(rol);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is SqlException sqlEx &&
                (sqlEx.Number == 2601 || sqlEx.Number == 2627))
            {
                return Conflict("Ya existe un rol con ese nombre.");
            }

            return StatusCode(201, new
            {
                mensaje = "Rol registrado correctamente.",
                rol.Id,
                rol.Nombre,
                rol.Descripcion,
                rol.Activo,
                rol.FechaCreacion
            });
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerRoles(
            [FromQuery] bool? activo,
            [FromQuery] string? buscar)
        {
            if (!_environment.IsDevelopment())
                return NotFound();

            var consulta = _context.Roles.AsNoTracking();

            if (activo.HasValue)
                consulta = consulta.Where(r => r.Activo == activo.Value);

            if (!string.IsNullOrWhiteSpace(buscar))
            {
                var termino = buscar.Trim().ToLowerInvariant();

                consulta = consulta.Where(r =>
                    r.Nombre.ToLower().Contains(termino) ||
                    r.Descripcion.ToLower().Contains(termino));
            }

            var roles = await consulta
                .OrderBy(r => r.Nombre)
                .Select(r => new
                {
                    r.Id,
                    r.Nombre,
                    r.Descripcion,
                    r.Activo,
                    r.FechaCreacion
                })
                .ToListAsync();

            return Ok(roles);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> ObtenerRolPorId(int id)
        {
            if (!_environment.IsDevelopment())
                return NotFound();

            if (id <= 0)
                return BadRequest("El ID del rol debe ser mayor que cero.");

            var rol = await _context.Roles
                .AsNoTracking()
                .Where(r => r.Id == id)
                .Select(r => new
                {
                    r.Id,
                    r.Nombre,
                    r.Descripcion,
                    r.Activo,
                    r.FechaCreacion
                })
                .FirstOrDefaultAsync();

            if (rol == null)
                return NotFound("Rol no encontrado.");

            return Ok(rol);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> ActualizarRol(
            int id,
            [FromBody] ActualizarRolDto datos)
        {
            if (!_environment.IsDevelopment())
                return NotFound();

            if (id <= 0)
                return BadRequest("El ID del rol debe ser mayor que cero.");

            if (datos == null)
                return BadRequest("Debes proporcionar los datos del rol.");

            var rol = await _context.Roles.FindAsync(id);

            if (rol == null)
                return NotFound("Rol no encontrado.");

            if (rol.Id == 1)
                return Conflict("No se puede modificar el rol Administrador.");

            if (!rol.Activo)
                return Conflict("No se puede modificar un rol inactivo.");

            var nombre = datos.Nombre?.Trim();
            var descripcion = datos.Descripcion?.Trim();

            if (string.IsNullOrWhiteSpace(nombre))
                return BadRequest("El nombre del rol es obligatorio.");

            if (nombre.Length > 100)
                return BadRequest("El nombre no puede superar los 100 caracteres.");

            if (string.IsNullOrWhiteSpace(descripcion))
                return BadRequest("La descripcion es obligatoria.");

            if (descripcion.Length > 300)
                return BadRequest("La descripcion no puede superar los 300 caracteres.");

            var existeRol = await _context.Roles
                .AnyAsync(r =>
                    r.Nombre.ToLower() == nombre.ToLower() &&
                    r.Id != id);

            if (existeRol)
                return Conflict("Ya existe un rol con ese nombre.");

            rol.Nombre = nombre;
            rol.Descripcion = descripcion;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is SqlException sqlEx &&
                (sqlEx.Number == 2601 || sqlEx.Number == 2627))
            {
                return Conflict("Ya existe un rol con ese nombre.");
            }

            return Ok(new
            {
                mensaje = "Rol actualizado correctamente.",
                rol.Id,
                rol.Nombre,
                rol.Descripcion,
                rol.Activo,
                rol.FechaCreacion
            });
        }

        [HttpPatch("{id:int}/desactivar")]
        public async Task<IActionResult> DesactivarRol(int id)
        {
            if (!_environment.IsDevelopment())
                return NotFound();

            if (id <= 0)
                return BadRequest("El ID del rol debe ser mayor que cero.");

            var rol = await _context.Roles.FindAsync(id);

            if (rol == null)
                return NotFound("Rol no encontrado.");

            if (rol.Id == 1)
                return Conflict("No se puede desactivar el rol Administrador.");

            if (!rol.Activo)
                return Conflict("El rol ya se encuentra desactivado.");

            rol.Activo = false;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                mensaje = "Rol desactivado correctamente.",
                rol.Id,
                rol.Nombre,
                rol.Activo
            });
        }

        [HttpPatch("{id:int}/activar")]
        public async Task<IActionResult> ActivarRol(int id)
        {
            if (!_environment.IsDevelopment())
                return NotFound();

            if (id <= 0)
                return BadRequest("El ID del rol debe ser mayor que cero.");

            var rol = await _context.Roles.FindAsync(id);

            if (rol == null)
                return NotFound("Rol no encontrado.");

            if (rol.Activo)
                return Conflict("El rol ya se encuentra activado.");

            rol.Activo = true;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                mensaje = "Rol activado correctamente.",
                rol.Id,
                rol.Nombre,
                rol.Activo
            });
        }

        [HttpGet("{id:int}/permisos")]
        public async Task<IActionResult> ObtenerPermisosDelRol(int id)
        {
            if (!_environment.IsDevelopment())
                return NotFound();

            if (id <= 0)
                return BadRequest("El ID del rol debe ser mayor que cero.");

            var rol = await _context.Roles
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id);

            if (rol == null)
                return NotFound("Rol no encontrado.");

            var permisos = await _context.RolesPermisos
                .AsNoTracking()
                .Where(rp => rp.RolId == id)
                .OrderBy(rp => rp.Permiso.Nombre)
                .Select(rp => new
                {
                    rp.Permiso.Id,
                    rp.Permiso.Nombre,
                    rp.Permiso.Codigo,
                    rp.Permiso.Descripcion,
                    rp.Permiso.Activo,
                    rp.FechaAsignacion
                })
                .ToListAsync();

            return Ok(new
            {
                rol.Id,
                rol.Nombre,
                rol.Activo,
                cantidadPermisos = permisos.Count,
                permisos
            });
        }

        [HttpPost("{id:int}/permisos/{permisoId:int}")]
        public async Task<IActionResult> AsignarPermiso(
            int id,
            int permisoId)
        {
            if (!_environment.IsDevelopment())
                return NotFound();

            if (id <= 0 || permisoId <= 0)
                return BadRequest("Los IDs deben ser mayores que cero.");

            var rol = await _context.Roles.FindAsync(id);

            if (rol == null)
                return NotFound("Rol no encontrado.");

            if (!rol.Activo)
                return Conflict("No se pueden asignar permisos a un rol inactivo.");

            var permiso = await _context.Permisos.FindAsync(permisoId);

            if (permiso == null)
                return NotFound("Permiso no encontrado.");

            if (!permiso.Activo)
                return Conflict("No se puede asignar un permiso inactivo.");

            var existeAsignacion = await _context.RolesPermisos
                .AnyAsync(rp =>
                    rp.RolId == id &&
                    rp.PermisoId == permisoId);

            if (existeAsignacion)
                return Conflict("El rol ya tiene asignado este permiso.");

            var rolPermiso = new RolPermiso
            {
                RolId = id,
                PermisoId = permisoId,
                FechaAsignacion = DateTime.UtcNow
            };

            _context.RolesPermisos.Add(rolPermiso);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is SqlException sqlEx &&
                (sqlEx.Number == 2601 || sqlEx.Number == 2627))
            {
                return Conflict("El rol ya tiene asignado este permiso.");
            }

            return StatusCode(201, new
            {
                mensaje = "Permiso asignado correctamente.",
                rolId = rol.Id,
                rol = rol.Nombre,
                permisoId = permiso.Id,
                permiso = permiso.Nombre,
                permiso.Codigo,
                rolPermiso.FechaAsignacion
            });
        }

        [HttpDelete("{id:int}/permisos/{permisoId:int}")]
        public async Task<IActionResult> QuitarPermiso(
            int id,
            int permisoId)
        {
            if (!_environment.IsDevelopment())
                return NotFound();

            if (id <= 0 || permisoId <= 0)
                return BadRequest("Los IDs deben ser mayores que cero.");

            var rol = await _context.Roles.FindAsync(id);

            if (rol == null)
                return NotFound("Rol no encontrado.");

            if (rol.Id == 1)
                return Conflict("No se pueden quitar permisos al rol Administrador.");

            var permiso = await _context.Permisos.FindAsync(permisoId);

            if (permiso == null)
                return NotFound("Permiso no encontrado.");

            var rolPermiso = await _context.RolesPermisos
                .FirstOrDefaultAsync(rp =>
                    rp.RolId == id &&
                    rp.PermisoId == permisoId);

            if (rolPermiso == null)
                return NotFound("El rol no tiene asignado este permiso.");

            _context.RolesPermisos.Remove(rolPermiso);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict("La asignacion fue modificada o eliminada por otra operacion.");
            }

            return Ok(new
            {
                mensaje = "Permiso quitado correctamente.",
                rolId = rol.Id,
                rol = rol.Nombre,
                permisoId = permiso.Id,
                permiso = permiso.Nombre,
                permiso.Codigo
            });
        }

    }
}
