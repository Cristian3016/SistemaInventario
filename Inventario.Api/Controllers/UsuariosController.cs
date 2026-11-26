using Inventario.Api.Data;
using Inventario.Api.DTOs;
using Inventario.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Net.Mail;
using Inventario.Api.Authorization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Inventario.Api.Controllers
{
    [ApiController]
    [Route("api/usuarios")]
    public class UsuariosController : ControllerBase
    {
        private readonly InventarioDbContext _context;
        private readonly IPasswordHasher<Usuario> _passwordHasher;
        private readonly IWebHostEnvironment _environment;

        public UsuariosController(
            InventarioDbContext context,
            IPasswordHasher<Usuario> passwordHasher,
            IWebHostEnvironment environment)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _environment = environment;
        }

        [Permiso(Permisos.UsuariosGestionar)]
        [HttpPost]
        public async Task<IActionResult> CrearUsuario(
            [FromBody] CrearUsuarioDto datos)
        {
            if (!_environment.IsDevelopment())
                return NotFound();

            if (datos == null)
                return BadRequest("Debes proporcionar los datos del usuario.");

            var nombre = datos.Nombre?.Trim();
            var apellido = datos.Apellido?.Trim();
            var nombreUsuario = datos.NombreUsuario?.Trim().ToLowerInvariant();
            var email = datos.Email?.Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(nombre))
                return BadRequest("El nombre es obligatorio.");

            if (nombre.Length > 100)
                return BadRequest("El nombre no puede superar los 100 caracteres.");

            if (string.IsNullOrWhiteSpace(apellido))
                return BadRequest("El apellido es obligatorio.");

            if (apellido.Length > 100)
                return BadRequest("El apellido no puede superar los 100 caracteres.");

            if (string.IsNullOrWhiteSpace(nombreUsuario))
                return BadRequest("El nombre de usuario es obligatorio.");

            if (nombreUsuario.Length < 4 || nombreUsuario.Length > 50)
                return BadRequest("El nombre de usuario debe tener entre 4 y 50 caracteres.");

            if (!nombreUsuario.All(c =>
                char.IsAsciiLetterOrDigit(c) ||
                c == '.' ||
                c == '_' ||
                c == '-'))
            {
                return BadRequest("El nombre de usuario contiene caracteres no permitidos.");
            }

            if (string.IsNullOrWhiteSpace(email))
                return BadRequest("El correo electrónico es obligatorio.");

            if (email.Length > 254)
                return BadRequest("El correo electrónico no puede superar los 254 caracteres.");

            if (!MailAddress.TryCreate(email, out var direccionEmail) ||
                direccionEmail.Address != email ||
                !direccionEmail.Host.Contains('.'))
            {
                return BadRequest("El correo electrónico no es válido.");
            }

            if (string.IsNullOrWhiteSpace(datos.Password))
                return BadRequest("La contraseña es obligatoria.");

            if (datos.Password.Length < 12 || datos.Password.Length > 128)
                return BadRequest("La contraseña debe tener entre 12 y 128 caracteres.");

            var existeNombreUsuario = await _context.Usuarios
                .AnyAsync(u => u.NombreUsuario == nombreUsuario);

            if (existeNombreUsuario)
                return Conflict("El nombre de usuario ya está registrado.");

            var existeEmail = await _context.Usuarios
                .AnyAsync(u => u.Email == email);

            if (existeEmail)
                return Conflict("El correo electrónico ya está registrado.");

            var usuario = new Usuario
            {
                Nombre = nombre,
                Apellido = apellido,
                NombreUsuario = nombreUsuario,
                Email = email,
                Activo = true,
                FechaCreacion = DateTime.UtcNow,
                UltimoAcceso = null
            };

            usuario.PasswordHash = _passwordHasher.HashPassword(
                usuario,
                datos.Password);

            _context.Usuarios.Add(usuario);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is SqlException sqlEx &&
                (sqlEx.Number == 2601 || sqlEx.Number == 2627))
            {
                return Conflict("El nombre de usuario o correo electrónico ya está registrado.");
            }

            return StatusCode(201, new
            {
                mensaje = "Usuario registrado correctamente.",
                usuario.Id,
                usuario.Nombre,
                usuario.Apellido,
                usuario.NombreUsuario,
                usuario.Email,
                usuario.Activo,
                usuario.FechaCreacion,
                usuario.UltimoAcceso
            });
        }

        [Permiso(Permisos.UsuariosGestionar)]
        [HttpGet]
        public async Task<IActionResult> ObtenerUsuarios(
            [FromQuery] bool? activo,
            [FromQuery] string? buscar)
        {
            if (!_environment.IsDevelopment())
                return NotFound();

            var consulta = _context.Usuarios.AsNoTracking();

            if (activo.HasValue)
                consulta = consulta.Where(u => u.Activo == activo.Value);

            if (!string.IsNullOrWhiteSpace(buscar))
            {
                var termino = buscar.Trim().ToLowerInvariant();

                consulta = consulta.Where(u =>
                    u.Nombre.ToLower().Contains(termino) ||
                    u.Apellido.ToLower().Contains(termino) ||
                    u.NombreUsuario.Contains(termino) ||
                    u.Email.Contains(termino));
            }

            var usuarios = await consulta
                .OrderBy(u => u.Apellido)
                .ThenBy(u => u.Nombre)
                .Select(u => new
                {
                    u.Id,
                    u.Nombre,
                    u.Apellido,
                    u.NombreUsuario,
                    u.Email,
                    u.Activo,
                    u.FechaCreacion,
                    u.UltimoAcceso
                })
                .ToListAsync();

            return Ok(usuarios);
        }

        [Permiso(Permisos.UsuariosGestionar)]
        [HttpGet("{id:int}")]
        public async Task<IActionResult> ObtenerUsuarioPorId(int id)
        {
            if (!_environment.IsDevelopment())
                return NotFound();

            if (id <= 0)
                return BadRequest("El ID del usuario debe ser mayor que cero.");

            var usuario = await _context.Usuarios
                .AsNoTracking()
                .Where(u => u.Id == id)
                .Select(u => new
                {
                    u.Id,
                    u.Nombre,
                    u.Apellido,
                    u.NombreUsuario,
                    u.Email,
                    u.Activo,
                    u.FechaCreacion,
                    u.UltimoAcceso
                })
                .FirstOrDefaultAsync();

            if (usuario == null)
                return NotFound("Usuario no encontrado.");

            return Ok(usuario);
        }

        [Permiso(Permisos.UsuariosGestionar)]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> ActualizarUsuario(
            int id,
            [FromBody] ActualizarUsuarioDto datos)
        {
            if (!_environment.IsDevelopment())
                return NotFound();

            if (id <= 0)
                return BadRequest("El ID del usuario debe ser mayor que cero.");

            if (datos == null)
                return BadRequest("Debes proporcionar los datos del usuario.");

            await using var transaccion = await _context.Database
                .BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable);

            try
            {
                var usuario = await _context.Usuarios.FindAsync(id);

                if (usuario == null)
                    return NotFound("Usuario no encontrado.");

                if (!usuario.Activo)
                    return Conflict("No se puede modificar un usuario inactivo.");

                var esAdministrador = await _context.UsuariosRoles
                    .AnyAsync(ur =>
                        ur.UsuarioId == id &&
                        ur.RolId == 1);

                if (esAdministrador)
                {
                    var identificador = User.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? User.FindFirstValue("sub");

                    if (!int.TryParse(identificador, out var usuarioAutenticadoId))
                        return Unauthorized();

                    var solicitanteEsAdministrador = await _context.UsuariosRoles
                        .AnyAsync(ur =>
                            ur.UsuarioId == usuarioAutenticadoId &&
                            ur.RolId == 1 &&
                            ur.Usuario.Activo &&
                            ur.Rol.Activo);

                    if (!solicitanteEsAdministrador)
                        return Forbid();
                }

                var nombre = datos.Nombre?.Trim();
                var apellido = datos.Apellido?.Trim();
                var nombreUsuario = datos.NombreUsuario?.Trim().ToLowerInvariant();
                var email = datos.Email?.Trim().ToLowerInvariant();

                if (string.IsNullOrWhiteSpace(nombre))
                    return BadRequest("El nombre es obligatorio.");

                if (nombre.Length > 100)
                    return BadRequest("El nombre no puede superar los 100 caracteres.");

                if (string.IsNullOrWhiteSpace(apellido))
                    return BadRequest("El apellido es obligatorio.");

                if (apellido.Length > 100)
                    return BadRequest("El apellido no puede superar los 100 caracteres.");

                if (string.IsNullOrWhiteSpace(nombreUsuario))
                    return BadRequest("El nombre de usuario es obligatorio.");

                if (nombreUsuario.Length < 4 || nombreUsuario.Length > 50)
                    return BadRequest("El nombre de usuario debe tener entre 4 y 50 caracteres.");

                if (!nombreUsuario.All(c =>
                    char.IsAsciiLetterOrDigit(c) ||
                    c == '.' ||
                    c == '_' ||
                    c == '-'))
                {
                    return BadRequest("El nombre de usuario contiene caracteres no permitidos.");
                }

                if (string.IsNullOrWhiteSpace(email))
                    return BadRequest("El correo electrónico es obligatorio.");

                if (email.Length > 254)
                    return BadRequest("El correo electrónico no puede superar los 254 caracteres.");

                if (!MailAddress.TryCreate(email, out var direccionEmail) ||
                    direccionEmail.Address != email ||
                    !direccionEmail.Host.Contains('.'))
                {
                    return BadRequest("El correo electrónico no es válido.");
                }

                var existeNombreUsuario = await _context.Usuarios
                    .AnyAsync(u =>
                        u.NombreUsuario == nombreUsuario &&
                        u.Id != id);

                if (existeNombreUsuario)
                    return Conflict("El nombre de usuario ya está registrado.");

                var existeEmail = await _context.Usuarios
                    .AnyAsync(u =>
                        u.Email == email &&
                        u.Id != id);

                if (existeEmail)
                    return Conflict("El correo electrónico ya está registrado.");

                usuario.Nombre = nombre;
                usuario.Apellido = apellido;
                usuario.NombreUsuario = nombreUsuario;
                usuario.Email = email;

                await _context.SaveChangesAsync();

                await transaccion.CommitAsync();

                return Ok(new
                {
                    mensaje = "Usuario actualizado correctamente.",
                    usuario.Id,
                    usuario.Nombre,
                    usuario.Apellido,
                    usuario.NombreUsuario,
                    usuario.Email,
                    usuario.Activo,
                    usuario.FechaCreacion,
                    usuario.UltimoAcceso
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(
                    "El usuario fue modificado por otra operacion.");
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is SqlException sqlEx &&
                (sqlEx.Number == 2601 || sqlEx.Number == 2627))
            {
                return Conflict(
                    "El nombre de usuario o correo electrónico ya está registrado.");
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is SqlException sqlEx &&
                sqlEx.Number == 1205)
            {
                return Conflict(
                    "Existe una operacion simultanea. Intente nuevamente.");
            }
            catch (SqlException ex) when (ex.Number == 1205)
            {
                return Conflict(
                    "Existe una operacion simultanea. Intente nuevamente.");
            }
        }

        [Permiso(Permisos.UsuariosGestionar)]
        [HttpPatch("{id:int}/desactivar")]
        public async Task<IActionResult> DesactivarUsuario(int id)
        {
            if (!_environment.IsDevelopment())
                return NotFound();

            if (id <= 0)
                return BadRequest("El ID del usuario debe ser mayor que cero.");

            try
            {
                await using var transaccion = await _context.Database
                    .BeginTransactionAsync(
                        System.Data.IsolationLevel.Serializable);

                var usuario = await _context.Usuarios.FindAsync(id);

                if (usuario == null)
                    return NotFound("Usuario no encontrado.");

                if (!usuario.Activo)
                    return Conflict("El usuario ya se encuentra desactivado.");

                var esAdministrador = await _context.UsuariosRoles
                    .AnyAsync(ur =>
                        ur.UsuarioId == id &&
                        ur.RolId == 1 &&
                        ur.Rol.Activo);

                if (esAdministrador)
                {
                    var identificador = User.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? User.FindFirstValue("sub");

                    if (!int.TryParse(identificador, out var usuarioAutenticadoId))
                        return Unauthorized();

                    var solicitanteEsAdministrador = await _context.UsuariosRoles
                        .AnyAsync(ur =>
                            ur.UsuarioId == usuarioAutenticadoId &&
                            ur.RolId == 1 &&
                            ur.Usuario.Activo &&
                            ur.Rol.Activo);

                    if (!solicitanteEsAdministrador)
                        return Forbid();

                    var existeOtroAdministrador = await _context.UsuariosRoles
                        .AnyAsync(ur =>
                            ur.UsuarioId != id &&
                            ur.RolId == 1 &&
                            ur.Usuario.Activo &&
                            ur.Rol.Activo);

                    if (!existeOtroAdministrador)
                    {
                        return Conflict(
                            "No se puede desactivar al ultimo administrador activo.");
                    }
                }

                usuario.Activo = false;

                await _context.SaveChangesAsync();

                var ahora = DateTime.UtcNow;

                const string motivo = "Desactivación de usuario.";

                await _context.SesionesUsuarios
                    .Where(s =>
                        s.UsuarioId == id &&
                        s.FechaRevocacion == null)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(
                            s => s.FechaRevocacion,
                            (DateTime?)ahora)
                        .SetProperty(
                            s => s.MotivoRevocacion,
                            motivo));

                await _context.RefreshTokens
                    .Where(rt =>
                        rt.SesionUsuario.UsuarioId == id &&
                        rt.FechaRevocacion == null)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(
                            rt => rt.FechaRevocacion,
                            (DateTime?)ahora)
                        .SetProperty(
                            rt => rt.MotivoRevocacion,
                            motivo));

                await transaccion.CommitAsync();

                return Ok(new
                {
                    mensaje = "Usuario desactivado correctamente.",
                    usuario.Id,
                    usuario.NombreUsuario,
                    usuario.Activo
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(
                    "El usuario fue modificado por otra operación.");
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

        [Permiso(Permisos.UsuariosGestionar)]
        [HttpPatch("{id:int}/activar")]
        public async Task<IActionResult> ActivarUsuario(int id)
        {
            if (!_environment.IsDevelopment())
                return NotFound();

            if (id <= 0)
                return BadRequest("El ID del usuario debe ser mayor que cero.");

            await using var transaccion = await _context.Database
                .BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable);

            try
            {
                var usuario = await _context.Usuarios.FindAsync(id);

                if (usuario == null)
                    return NotFound("Usuario no encontrado.");

                if (usuario.Activo)
                    return Conflict("El usuario ya se encuentra activado.");

                var esAdministrador = await _context.UsuariosRoles
                    .AnyAsync(ur =>
                        ur.UsuarioId == id &&
                        ur.RolId == 1);

                if (esAdministrador)
                {
                    var identificador = User.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? User.FindFirstValue("sub");

                    if (!int.TryParse(identificador, out var usuarioAutenticadoId))
                        return Unauthorized();

                    var solicitanteEsAdministrador = await _context.UsuariosRoles
                        .AnyAsync(ur =>
                            ur.UsuarioId == usuarioAutenticadoId &&
                            ur.RolId == 1 &&
                            ur.Usuario.Activo &&
                            ur.Rol.Activo);

                    if (!solicitanteEsAdministrador)
                        return Forbid();
                }

                usuario.Activo = true;

                await _context.SaveChangesAsync();

                await transaccion.CommitAsync();

                return Ok(new
                {
                    mensaje = "Usuario activado correctamente.",
                    usuario.Id,
                    usuario.NombreUsuario,
                    usuario.Activo
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(
                    "El usuario fue modificado por otra operacion.");
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is SqlException sqlEx &&
                sqlEx.Number == 1205)
            {
                return Conflict(
                    "Existe una operacion simultanea. Intente nuevamente.");
            }
            catch (SqlException ex) when (ex.Number == 1205)
            {
                return Conflict(
                    "Existe una operacion simultanea. Intente nuevamente.");
            }
        }

        [Authorize]
        [HttpPatch("{id:int}/cambiar-password")]
        public async Task<IActionResult> CambiarPassword(
    int id,
    [FromBody] CambiarPasswordDto datos)
        {
            if (!_environment.IsDevelopment())
                return NotFound();

            if (id <= 0)
                return BadRequest("El ID del usuario debe ser mayor que cero.");

            var identificador = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub");

            if (!int.TryParse(identificador, out var usuarioAutenticadoId) ||
                usuarioAutenticadoId <= 0)
            {
                return Unauthorized();
            }

            if (usuarioAutenticadoId != id)
                return Forbid();

            if (datos == null)
                return BadRequest("Debes proporcionar los datos de la contraseña.");

            if (string.IsNullOrWhiteSpace(datos.PasswordActual))
                return BadRequest("La contraseña actual es obligatoria.");

            if (string.IsNullOrWhiteSpace(datos.PasswordNueva))
                return BadRequest("La nueva contraseña es obligatoria.");

            if (string.IsNullOrWhiteSpace(datos.ConfirmarPassword))
                return BadRequest("Debes confirmar la nueva contraseña.");

            if (datos.PasswordNueva.Length < 12 ||
                datos.PasswordNueva.Length > 128)
            {
                return BadRequest(
                    "La nueva contraseña debe tener entre 12 y 128 caracteres.");
            }

            if (datos.PasswordNueva != datos.ConfirmarPassword)
                return BadRequest("Las nuevas contraseñas no coinciden.");

            try
            {
                await using var transaccion = await _context.Database
                    .BeginTransactionAsync(
                        System.Data.IsolationLevel.Serializable);

                var usuario = await _context.Usuarios.FindAsync(id);

                if (usuario == null)
                    return NotFound("Usuario no encontrado.");

                if (!usuario.Activo)
                    return Conflict("No se puede cambiar la contraseña de un usuario inactivo.");

                var resultado = _passwordHasher.VerifyHashedPassword(
                    usuario,
                    usuario.PasswordHash,
                    datos.PasswordActual);

                if (resultado == PasswordVerificationResult.Failed)
                    return BadRequest("La contraseña actual es incorrecta.");

                if (datos.PasswordActual == datos.PasswordNueva)
                    return BadRequest("La nueva contraseña debe ser diferente de la actual.");

                usuario.PasswordHash = _passwordHasher.HashPassword(
                    usuario,
                    datos.PasswordNueva);

                await _context.SaveChangesAsync();

                var ahora = DateTime.UtcNow;

                const string motivo = "Cambio de contraseña.";

                await _context.SesionesUsuarios
                    .Where(s =>
                        s.UsuarioId == id &&
                        s.FechaRevocacion == null)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(
                            s => s.FechaRevocacion,
                            (DateTime?)ahora)
                        .SetProperty(
                            s => s.MotivoRevocacion,
                            motivo));

                await _context.RefreshTokens
                    .Where(rt =>
                        rt.SesionUsuario.UsuarioId == id &&
                        rt.FechaRevocacion == null)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(
                            rt => rt.FechaRevocacion,
                            (DateTime?)ahora)
                        .SetProperty(
                            rt => rt.MotivoRevocacion,
                            motivo));

                await transaccion.CommitAsync();

                return Ok(new
                {
                    mensaje = "Contraseña actualizada correctamente. Inicie sesión nuevamente.",
                    usuario.Id,
                    usuario.NombreUsuario
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(
                    "El usuario fue modificado por otra operación.");
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
        [HttpPost("{id:int}/roles/{rolId:int}")]
        public async Task<IActionResult> AsignarRol(
    int id,
    int rolId)
        {
            if (!_environment.IsDevelopment())
                return NotFound();

            if (id <= 0 || rolId <= 0)
                return BadRequest("Los IDs deben ser mayores que cero.");

            var identificador = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub");

            if (!int.TryParse(identificador, out var usuarioAutenticadoId) ||
                usuarioAutenticadoId <= 0)
                return Unauthorized();

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

                var usuario = await _context.Usuarios.FindAsync(id);

                if (usuario == null)
                    return NotFound("Usuario no encontrado.");

                if (!usuario.Activo)
                    return Conflict("No se pueden asignar roles a un usuario inactivo.");

                var rol = await _context.Roles.FindAsync(rolId);

                if (rol == null)
                    return NotFound("Rol no encontrado.");

                if (!rol.Activo)
                    return Conflict("No se puede asignar un rol inactivo.");

                var existeAsignacion = await _context.UsuariosRoles
                    .AnyAsync(ur =>
                        ur.UsuarioId == id &&
                        ur.RolId == rolId);

                if (existeAsignacion)
                    return Conflict("El usuario ya tiene asignado este rol.");

                var usuarioRol = new UsuarioRol
                {
                    UsuarioId = id,
                    RolId = rolId,
                    FechaAsignacion = DateTime.UtcNow
                };

                _context.UsuariosRoles.Add(usuarioRol);

                await _context.SaveChangesAsync();

                await transaccion.CommitAsync();

                return StatusCode(201, new
                {
                    mensaje = "Rol asignado correctamente.",
                    usuarioId = usuario.Id,
                    usuario.NombreUsuario,
                    rolId = rol.Id,
                    rol = rol.Nombre,
                    usuarioRol.FechaAsignacion
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
                return Conflict("El usuario ya tiene asignado este rol.");
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
        [HttpDelete("{id:int}/roles/{rolId:int}")]
        public async Task<IActionResult> QuitarRol(
    int id,
    int rolId)
        {
            if (!_environment.IsDevelopment())
                return NotFound();

            if (id <= 0 || rolId <= 0)
                return BadRequest("Los IDs deben ser mayores que cero.");

            var identificador = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub");

            if (!int.TryParse(identificador, out var usuarioAutenticadoId) ||
                usuarioAutenticadoId <= 0)
                return Unauthorized();

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

                var usuario = await _context.Usuarios.FindAsync(id);

                if (usuario == null)
                    return NotFound("Usuario no encontrado.");

                var rol = await _context.Roles.FindAsync(rolId);

                if (rol == null)
                    return NotFound("Rol no encontrado.");

                var usuarioRol = await _context.UsuariosRoles
                    .FirstOrDefaultAsync(ur =>
                        ur.UsuarioId == id &&
                        ur.RolId == rolId);

                if (usuarioRol == null)
                    return NotFound("El usuario no tiene asignado este rol.");

                if (rolId == 1 && usuario.Activo)
                {
                    var existeOtroAdministrador = await _context.UsuariosRoles
                        .AnyAsync(ur =>
                            ur.RolId == 1 &&
                            ur.UsuarioId != id &&
                            ur.Usuario.Activo &&
                            ur.Rol.Activo);

                    if (!existeOtroAdministrador)
                    {
                        return Conflict(
                            "No se puede quitar el rol al último administrador activo.");
                    }
                }

                _context.UsuariosRoles.Remove(usuarioRol);

                await _context.SaveChangesAsync();

                await transaccion.CommitAsync();

                return Ok(new
                {
                    mensaje = "Rol quitado correctamente.",
                    usuarioId = usuario.Id,
                    usuario.NombreUsuario,
                    rolId = rol.Id,
                    rol = rol.Nombre
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

        [Permiso(Permisos.RolesGestionar)]
        [HttpGet("{id:int}/roles")]
        public async Task<IActionResult> ObtenerRolesDelUsuario(int id)
        {
            if (!_environment.IsDevelopment())
                return NotFound();

            if (id <= 0)
                return BadRequest("El ID del usuario debe ser mayor que cero.");

            var usuario = await _context.Usuarios
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == id);

            if (usuario == null)
                return NotFound("Usuario no encontrado.");

            var roles = await _context.UsuariosRoles
                .AsNoTracking()
                .Where(ur => ur.UsuarioId == id)
                .OrderBy(ur => ur.Rol.Nombre)
                .Select(ur => new
                {
                    ur.Rol.Id,
                    ur.Rol.Nombre,
                    ur.Rol.Descripcion,
                    ur.Rol.Activo,
                    ur.FechaAsignacion
                })
                .ToListAsync();

            return Ok(new
            {
                usuario.Id,
                usuario.Nombre,
                usuario.Apellido,
                usuario.NombreUsuario,
                usuario.Activo,
                cantidadRoles = roles.Count,
                roles
            });
        }

        [Permiso(Permisos.PermisosGestionar)]
        [HttpGet("{id:int}/permisos")]
        public async Task<IActionResult> ObtenerPermisosDelUsuario(int id)
        {
            if (!_environment.IsDevelopment())
                return NotFound();

            if (id <= 0)
                return BadRequest("El ID del usuario debe ser mayor que cero.");

            var usuario = await _context.Usuarios
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == id);

            if (usuario == null)
                return NotFound("Usuario no encontrado.");

            var permisos = await _context.RolesPermisos
                .AsNoTracking()
                .Where(rp =>
                    rp.Rol.Activo &&
                    rp.Permiso.Activo &&
                    _context.UsuariosRoles.Any(ur =>
                        ur.UsuarioId == id &&
                        ur.Usuario.Activo &&
                        ur.RolId == rp.RolId))
                .Select(rp => new
                {
                    rp.Permiso.Id,
                    rp.Permiso.Nombre,
                    rp.Permiso.Codigo,
                    rp.Permiso.Descripcion
                })
                .Distinct()
                .OrderBy(p => p.Nombre)
                .ToListAsync();

            return Ok(new
            {
                usuario.Id,
                usuario.Nombre,
                usuario.Apellido,
                usuario.NombreUsuario,
                usuario.Activo,
                cantidadPermisos = permisos.Count,
                permisos
            });
        }
    }
}