using Inventario.Api.Data;
using Inventario.Api.DTOs;
using Inventario.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Inventario.Api.Authorization;
using System.Security.Claims;

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

        [Permiso(Permisos.RolesGestionar)]
        [HttpPost]
        public async Task<IActionResult> CrearRol(
     [FromBody] CrearRolDto datos)
        {
            if (!_environment.IsDevelopment())
                return NotFound();

            if (datos == null)
                return BadRequest("Debes proporcionar los datos del rol.");

            var identificador = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub");

            if (!int.TryParse(identificador, out var usuarioAutenticadoId) ||
                usuarioAutenticadoId <= 0)
            {
                return Unauthorized();
            }

            try
            {
                await using var transaccion = await _context.Database
                    .BeginTransactionAsync(
                        System.Data.IsolationLevel.Serializable);

                var esAdministrador = await _context.UsuariosRoles
                    .AnyAsync(ur =>
                        ur.UsuarioId == usuarioAutenticadoId &&
                        ur.RolId == 1 &&
                        ur.Usuario.Activo &&
                        ur.Rol.Activo);

                if (!esAdministrador)
                    return Forbid();

                var nombre = datos.Nombre?.Trim();
                var descripcion = datos.Descripcion?.Trim();

                if (string.IsNullOrWhiteSpace(nombre))
                    return BadRequest("El nombre del rol es obligatorio.");

                if (nombre.Length > 100)
                    return BadRequest("El nombre no puede superar los 100 caracteres.");

                if (string.IsNullOrWhiteSpace(descripcion))
                    return BadRequest("La descripción es obligatoria.");

                if (descripcion.Length > 300)
                    return BadRequest("La descripción no puede superar los 300 caracteres.");

                var existeRol = await _context.Roles
                    .AnyAsync(r =>
                        r.Nombre.ToLower() == nombre.ToLower());

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

                await _context.SaveChangesAsync();

                await transaccion.CommitAsync();

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
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(
                    "El rol fue modificado por otra operación.");
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is SqlException sqlEx &&
                (sqlEx.Number == 2601 || sqlEx.Number == 2627))
            {
                return Conflict("Ya existe un rol con ese nombre.");
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is SqlException sqlEx &&
                sqlEx.Number == 1205)
            {
                return Conflict(
                    "Existe una operación simultánea. Intente nuevamente.");
            }
            catch (SqlException ex) when (ex.Number == 1205)
            {
                return Conflict(
                    "Existe una operación simultánea. Intente nuevamente.");
            }
        }

        [Permiso(Permisos.RolesGestionar)]
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

        [Permiso(Permisos.RolesGestionar)]
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

        [Permiso(Permisos.RolesGestionar)]
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

            var identificador = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub");

            if (!int.TryParse(identificador, out var usuarioAutenticadoId) ||
                usuarioAutenticadoId <= 0)
            {
                return Unauthorized();
            }

            try
            {
                await using var transaccion = await _context.Database
                    .BeginTransactionAsync(
                        System.Data.IsolationLevel.Serializable);

                var esAdministrador = await _context.UsuariosRoles
                    .AnyAsync(ur =>
                        ur.UsuarioId == usuarioAutenticadoId &&
                        ur.RolId == 1 &&
                        ur.Usuario.Activo &&
                        ur.Rol.Activo);

                if (!esAdministrador)
                    return Forbid();

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
                    return BadRequest("La descripción es obligatoria.");

                if (descripcion.Length > 300)
                    return BadRequest("La descripción no puede superar los 300 caracteres.");

                var existeRol = await _context.Roles
                    .AnyAsync(r =>
                        r.Nombre.ToLower() == nombre.ToLower() &&
                        r.Id != id);

                if (existeRol)
                    return Conflict("Ya existe un rol con ese nombre.");

                rol.Nombre = nombre;
                rol.Descripcion = descripcion;

                await _context.SaveChangesAsync();

                await transaccion.CommitAsync();

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
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(
                    "El rol fue modificado por otra operación.");
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is SqlException sqlEx &&
                (sqlEx.Number == 2601 || sqlEx.Number == 2627))
            {
                return Conflict("Ya existe un rol con ese nombre.");
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is SqlException sqlEx &&
                sqlEx.Number == 1205)
            {
                return Conflict(
                    "Existe una operación simultánea. Intente nuevamente.");
            }
            catch (SqlException ex) when (ex.Number == 1205)
            {
                return Conflict(
                    "Existe una operación simultánea. Intente nuevamente.");
            }
        }

        [Permiso(Permisos.RolesGestionar)]
        [HttpPatch("{id:int}/desactivar")]
        public async Task<IActionResult> DesactivarRol(int id)
        {
            if (!_environment.IsDevelopment())
                return NotFound();

            if (id <= 0)
                return BadRequest("El ID del rol debe ser mayor que cero.");

            var identificador = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub");

            if (!int.TryParse(identificador, out var usuarioAutenticadoId) ||
                usuarioAutenticadoId <= 0)
            {
                return Unauthorized();
            }

            try
            {
                await using var transaccion = await _context.Database
                    .BeginTransactionAsync(
                        System.Data.IsolationLevel.Serializable);

                var esAdministrador = await _context.UsuariosRoles
                    .AnyAsync(ur =>
                        ur.UsuarioId == usuarioAutenticadoId &&
                        ur.RolId == 1 &&
                        ur.Usuario.Activo &&
                        ur.Rol.Activo);

                if (!esAdministrador)
                    return Forbid();

                var rol = await _context.Roles.FindAsync(id);

                if (rol == null)
                    return NotFound("Rol no encontrado.");

                if (rol.Id == 1)
                    return Conflict("No se puede desactivar el rol Administrador.");

                if (!rol.Activo)
                    return Conflict("El rol ya se encuentra desactivado.");

                rol.Activo = false;

                await _context.SaveChangesAsync();

                await transaccion.CommitAsync();

                return Ok(new
                {
                    mensaje = "Rol desactivado correctamente.",
                    rol.Id,
                    rol.Nombre,
                    rol.Activo
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(
                    "El rol fue modificado por otra operación.");
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is SqlException sqlEx &&
                sqlEx.Number == 1205)
            {
                return Conflict(
                    "Existe una operación simultánea. Intente nuevamente.");
            }
            catch (SqlException ex) when (ex.Number == 1205)
            {
                return Conflict(
                    "Existe una operación simultánea. Intente nuevamente.");
            }
        }

        [Permiso(Permisos.RolesGestionar)]
        [HttpPatch("{id:int}/activar")]
        public async Task<IActionResult> ActivarRol(int id)
        {
            if (!_environment.IsDevelopment())
                return NotFound();

            if (id <= 0)
                return BadRequest("El ID del rol debe ser mayor que cero.");

            var identificador = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub");

            if (!int.TryParse(identificador, out var usuarioAutenticadoId) ||
                usuarioAutenticadoId <= 0)
            {
                return Unauthorized();
            }

            try
            {
                await using var transaccion = await _context.Database
                    .BeginTransactionAsync(
                        System.Data.IsolationLevel.Serializable);

                var esAdministrador = await _context.UsuariosRoles
                    .AnyAsync(ur =>
                        ur.UsuarioId == usuarioAutenticadoId &&
                        ur.RolId == 1 &&
                        ur.Usuario.Activo &&
                        ur.Rol.Activo);

                if (!esAdministrador)
                    return Forbid();

                var rol = await _context.Roles.FindAsync(id);

                if (rol == null)
                    return NotFound("Rol no encontrado.");

                if (rol.Activo)
                    return Conflict("El rol ya se encuentra activado.");

                rol.Activo = true;

                await _context.SaveChangesAsync();

                await transaccion.CommitAsync();

                return Ok(new
                {
                    mensaje = "Rol activado correctamente.",
                    rol.Id,
                    rol.Nombre,
                    rol.Activo
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(
                    "El rol fue modificado por otra operación.");
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is SqlException sqlEx &&
                sqlEx.Number == 1205)
            {
                return Conflict(
                    "Existe una operación simultánea. Intente nuevamente.");
            }
            catch (SqlException ex) when (ex.Number == 1205)
            {
                return Conflict(
                    "Existe una operación simultánea. Intente nuevamente.");
            }
        }

        [Permiso(Permisos.PermisosGestionar)]
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

        [Permiso(Permisos.PermisosGestionar)]
        [HttpPost("{id:int}/permisos/{permisoId:int}")]
        public async Task<IActionResult> AsignarPermiso(
            int id,
            int permisoId)
        {
            if (!_environment.IsDevelopment())
                return NotFound();

            if (id <= 0 || permisoId <= 0)
                return BadRequest("Los IDs deben ser mayores que cero.");

            var identificador = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub");

            if (!int.TryParse(identificador, out var usuarioAutenticadoId) ||
                usuarioAutenticadoId <= 0)
            {
                return Unauthorized();
            }

            try
            {
                await using var transaccion = await _context.Database
                    .BeginTransactionAsync(
                        System.Data.IsolationLevel.Serializable);

                var esAdministrador = await _context.UsuariosRoles
                    .AnyAsync(ur =>
                        ur.UsuarioId == usuarioAutenticadoId &&
                        ur.RolId == 1 &&
                        ur.Usuario.Activo &&
                        ur.Rol.Activo);

                if (!esAdministrador)
                    return Forbid();

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

                await _context.SaveChangesAsync();

                await transaccion.CommitAsync();

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
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(
                    "La asignación fue modificada por otra operación.");
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is SqlException sqlEx &&
                (sqlEx.Number == 2601 || sqlEx.Number == 2627))
            {
                return Conflict("El rol ya tiene asignado este permiso.");
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is SqlException sqlEx &&
                sqlEx.Number == 1205)
            {
                return Conflict(
                    "Existe una operación simultánea. Intente nuevamente.");
            }
            catch (SqlException ex) when (ex.Number == 1205)
            {
                return Conflict(
                    "Existe una operación simultánea. Intente nuevamente.");
            }
        }

        [Permiso(Permisos.PermisosGestionar)]
        [HttpDelete("{id:int}/permisos/{permisoId:int}")]
        public async Task<IActionResult> QuitarPermiso(
    int id,
    int permisoId)
        {
            if (!_environment.IsDevelopment())
                return NotFound();

            if (id <= 0 || permisoId <= 0)
                return BadRequest("Los IDs deben ser mayores que cero.");

            var identificador = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub");

            if (!int.TryParse(identificador, out var usuarioAutenticadoId) ||
                usuarioAutenticadoId <= 0)
            {
                return Unauthorized();
            }

            try
            {
                await using var transaccion = await _context.Database
                    .BeginTransactionAsync(
                        System.Data.IsolationLevel.Serializable);

                var esAdministrador = await _context.UsuariosRoles
                    .AnyAsync(ur =>
                        ur.UsuarioId == usuarioAutenticadoId &&
                        ur.RolId == 1 &&
                        ur.Usuario.Activo &&
                        ur.Rol.Activo);

                if (!esAdministrador)
                    return Forbid();

                var rol = await _context.Roles.FindAsync(id);

                if (rol == null)
                    return NotFound("Rol no encontrado.");

                if (rol.Id == 1)
                    return Conflict(
                        "No se pueden quitar permisos al rol Administrador.");

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

                await _context.SaveChangesAsync();

                await transaccion.CommitAsync();

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
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(
                    "La asignación fue modificada o eliminada por otra operación.");
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is SqlException sqlEx &&
                sqlEx.Number == 1205)
            {
                return Conflict(
                    "Existe una operación simultánea. Intente nuevamente.");
            }
            catch (SqlException ex) when (ex.Number == 1205)
            {
                return Conflict(
                    "Existe una operación simultánea. Intente nuevamente.");
            }
        }
    }
}