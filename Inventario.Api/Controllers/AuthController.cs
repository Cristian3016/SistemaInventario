using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;
using Inventario.Api.Data;
using Inventario.Api.DTOs;
using Inventario.Api.Models;
using Inventario.Api.Services;
using Inventario.Api.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Data;
using System.IdentityModel.Tokens.Jwt;

namespace Inventario.Api.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly InventarioDbContext _context;
        private readonly IPasswordHasher<Usuario> _passwordHasher;
        private readonly IJwtService _jwtService;
        private readonly JwtSettings _jwtSettings;
        private readonly IAuditoriaService _auditoriaService;

        private const string RefreshTokenCookieName =
            "inventario_refresh_token";

        public AuthController(
            InventarioDbContext context,
            IPasswordHasher<Usuario> passwordHasher,
            IJwtService jwtService,
            IOptions<JwtSettings> jwtOptions,
            IAuditoriaService auditoriaService)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _jwtService = jwtService;
            _jwtSettings = jwtOptions.Value;
            _auditoriaService = auditoriaService;
        }

        [AllowAnonymous]
        [EnableRateLimiting("login")]
        [HttpPost("login")]
        [Consumes("application/json")]
        [ResponseCache(
            NoStore = true,
            Location = ResponseCacheLocation.None)]
        public async Task<ActionResult<LoginRespuestaDto>> Login(
            [FromBody] LoginDto datos)
        {
            if (datos == null ||
                string.IsNullOrWhiteSpace(datos.NombreUsuario) ||
                string.IsNullOrWhiteSpace(datos.Password))
            {
                return BadRequest(new
                {
                    mensaje = "El usuario y la contraseña son obligatorios."
                });
            }

            var nombreUsuario = datos.NombreUsuario
                .Trim()
                .ToLowerInvariant();

            try
            {
                await using var transaccion =
                    await _context.Database.BeginTransactionAsync(
                        IsolationLevel.Serializable);

                var usuario = await _context.Usuarios
                    .FirstOrDefaultAsync(u =>
                        u.NombreUsuario == nombreUsuario);

                if (usuario == null || !usuario.Activo)
                {
                    _auditoriaService.Registrar(
                        null,
                        "LOGIN_FALLIDO",
                        "Usuario",
                        usuario?.Id.ToString(),
                        "Intento de inicio de sesión rechazado.");

                    await _context.SaveChangesAsync();

                    await transaccion.CommitAsync();

                    return Unauthorized(new
                    {
                        mensaje = "Usuario o contraseña incorrectos."
                    });
                }

                var resultado = _passwordHasher.VerifyHashedPassword(
                    usuario,
                    usuario.PasswordHash,
                    datos.Password);

                if (resultado == PasswordVerificationResult.Failed)
                {
                    _auditoriaService.Registrar(
                        null,
                        "LOGIN_FALLIDO",
                        "Usuario",
                        usuario.Id.ToString(),
                        "Intento de inicio de sesión rechazado.");

                    await _context.SaveChangesAsync();

                    await transaccion.CommitAsync();

                    return Unauthorized(new
                    {
                        mensaje = "Usuario o contraseña incorrectos."
                    });
                }

                if (resultado ==
                    PasswordVerificationResult.SuccessRehashNeeded)
                {
                    usuario.PasswordHash =
                        _passwordHasher.HashPassword(
                            usuario,
                            datos.Password);
                }

                var ahora = DateTime.UtcNow;

                var direccionIp = HttpContext.Connection
                    .RemoteIpAddress?.ToString();

                var userAgent = Request.Headers.UserAgent.ToString();

                if (userAgent.Length > 500)
                {
                    userAgent = userAgent[..500];
                }

                var sesion = new SesionUsuario
                {
                    UsuarioId = usuario.Id,
                    FechaCreacion = ahora,
                    UltimaActividad = ahora,
                    FechaExpiracion = ahora.AddHours(
                        _jwtSettings.SessionMaxDurationHours),
                    DireccionIp = direccionIp,
                    UserAgent = userAgent
                };

                var refreshTokenOriginal =
                    _jwtService.GenerarRefreshToken();

                var fechaExpiracionRefreshToken = ahora.AddHours(
                    _jwtSettings.RefreshTokenExpirationHours);

                if (fechaExpiracionRefreshToken > sesion.FechaExpiracion)
                {
                    fechaExpiracionRefreshToken =
                        sesion.FechaExpiracion;
                }

                var refreshToken = new RefreshToken
                {
                    SesionUsuarioId = sesion.Id,
                    TokenHash = _jwtService.CalcularHashRefreshToken(
                        refreshTokenOriginal),
                    FechaCreacion = ahora,
                    FechaExpiracion = fechaExpiracionRefreshToken
                };

                var accessToken = _jwtService.GenerarAccessToken(
                    usuario,
                    sesion);

                var jwt = new JwtSecurityTokenHandler()
                    .ReadJwtToken(accessToken);

                usuario.UltimoAcceso = ahora;

                _context.SesionesUsuarios.Add(sesion);

                _context.RefreshTokens.Add(refreshToken);

                _auditoriaService.Registrar(
                    usuario.Id,
                    "INICIAR_SESION",
                    "SesionUsuario",
                    sesion.Id.ToString(),
                    "Inicio de sesión exitoso.");

                await _context.SaveChangesAsync();

                await transaccion.CommitAsync();

                Response.Cookies.Append(
                    RefreshTokenCookieName,
                    refreshTokenOriginal,
                    new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.Strict,
                        Path = "/api/auth",
                        IsEssential = true,
                        Expires = new DateTimeOffset(
                            fechaExpiracionRefreshToken)
                    });

                var fechaExpiracionInactividad = ahora.AddMinutes(
                    _jwtSettings.SessionIdleTimeoutMinutes);

                if (fechaExpiracionInactividad > sesion.FechaExpiracion)
                {
                    fechaExpiracionInactividad =
                        sesion.FechaExpiracion;
                }

                var respuesta = new LoginRespuestaDto
                {
                    UsuarioId = usuario.Id,
                    Nombre = usuario.Nombre,
                    Apellido = usuario.Apellido,
                    NombreUsuario = usuario.NombreUsuario,
                    SesionId = sesion.Id,
                    AccessToken = accessToken,
                    TokenType = "Bearer",
                    FechaExpiracionAccessToken = jwt.ValidTo,
                    FechaExpiracionSesion = sesion.FechaExpiracion,
                    FechaExpiracionInactividad =
                        fechaExpiracionInactividad,
                    AdvertenciaInactividadSegundos =
                        _jwtSettings.SessionWarningSeconds
                };

                return Ok(respuesta);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new
                {
                    mensaje = "Existe una operación simultánea. Intente nuevamente."
                });
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is SqlException sqlEx &&
                sqlEx.Number == 1205)
            {
                return Conflict(new
                {
                    mensaje = "Conflicto durante el inicio de sesión."
                });
            }
            catch (SqlException ex) when (ex.Number == 1205)
            {
                return Conflict(new
                {
                    mensaje = "Conflicto durante el inicio de sesión."
                });
            }
        }

        [Authorize]
        [HttpGet("me")]
        [ResponseCache(
            NoStore = true,
            Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> ObtenerUsuarioAutenticado()
        {
            var usuarioIdClaim = User.FindFirst("sub")?.Value;

            var sesionIdClaim = User.FindFirst("sid")?.Value;

            if (!int.TryParse(usuarioIdClaim, out var usuarioId) ||
                !Guid.TryParse(sesionIdClaim, out var sesionId))
            {
                return Unauthorized(new
                {
                    mensaje = "La sesión no es válida."
                });
            }

            var ahora = DateTime.UtcNow;

            var limiteInactividad = ahora.AddMinutes(
                -_jwtSettings.SessionIdleTimeoutMinutes);

            var sesion = await _context.SesionesUsuarios
                .AsNoTracking()
                .Include(s => s.Usuario)
                .FirstOrDefaultAsync(s =>
                    s.Id == sesionId &&
                    s.UsuarioId == usuarioId &&
                    s.Usuario.Activo &&
                    s.FechaRevocacion == null &&
                    s.FechaExpiracion > ahora &&
                    s.UltimaActividad > limiteInactividad);

            if (sesion == null)
            {
                return Unauthorized(new
                {
                    mensaje = "La sesión no es válida o ha expirado."
                });
            }

            var usuario = sesion.Usuario;

            var roles = await _context.UsuariosRoles
                .AsNoTracking()
                .Where(ur =>
                    ur.UsuarioId == usuarioId &&
                    ur.Usuario.Activo &&
                    ur.Rol.Activo)
                .Select(ur => new
                {
                    ur.Rol.Id,
                    ur.Rol.Nombre
                })
                .OrderBy(r => r.Nombre)
                .ToListAsync();

            var permisos = await _context.RolesPermisos
                .AsNoTracking()
                .Where(rp =>
                    rp.Rol.Activo &&
                    rp.Permiso.Activo &&
                    _context.UsuariosRoles.Any(ur =>
                        ur.UsuarioId == usuarioId &&
                        ur.Usuario.Activo &&
                        ur.RolId == rp.RolId))
                .Select(rp => new
                {
                    rp.Permiso.Id,
                    rp.Permiso.Nombre,
                    rp.Permiso.Codigo
                })
                .Distinct()
                .OrderBy(p => p.Nombre)
                .ToListAsync();

            var fechaExpiracionInactividad =
                sesion.UltimaActividad.AddMinutes(
                    _jwtSettings.SessionIdleTimeoutMinutes);

            if (fechaExpiracionInactividad > sesion.FechaExpiracion)
            {
                fechaExpiracionInactividad =
                    sesion.FechaExpiracion;
            }

            return Ok(new
            {
                usuario.Id,
                usuario.Nombre,
                usuario.Apellido,
                usuario.NombreUsuario,
                usuario.Email,
                usuario.Activo,
                usuario.FechaCreacion,
                usuario.UltimoAcceso,

                CantidadRoles = roles.Count,
                Roles = roles,

                CantidadPermisos = permisos.Count,
                Permisos = permisos,

                Sesion = new
                {
                    sesion.Id,
                    sesion.FechaCreacion,
                    sesion.UltimaActividad,
                    sesion.FechaExpiracion,
                    FechaExpiracionInactividad =
                        fechaExpiracionInactividad,
                    AdvertenciaInactividadSegundos =
                        _jwtSettings.SessionWarningSeconds
                }
            });
        }

        [Authorize]
        [HttpPost("actividad")]
        [ResponseCache(
            NoStore = true,
            Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> RegistrarActividad()
        {
            var usuarioIdClaim = User.FindFirst("sub")?.Value;

            var sesionIdClaim = User.FindFirst("sid")?.Value;

            if (!int.TryParse(usuarioIdClaim, out var usuarioId) ||
                usuarioId <= 0 ||
                !Guid.TryParse(sesionIdClaim, out var sesionId) ||
                sesionId == Guid.Empty)
            {
                return Unauthorized(new
                {
                    mensaje = "La sesión no es válida."
                });
            }

            var ahora = DateTime.UtcNow;

            var limiteInactividad = ahora.AddMinutes(
                -_jwtSettings.SessionIdleTimeoutMinutes);

            var filasActualizadas = await _context.SesionesUsuarios
                .Where(s =>
                    s.Id == sesionId &&
                    s.UsuarioId == usuarioId &&
                    s.Usuario.Activo &&
                    s.FechaRevocacion == null &&
                    s.FechaExpiracion > ahora &&
                    s.UltimaActividad > limiteInactividad)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(
                        s => s.UltimaActividad,
                        s => s.UltimaActividad < ahora
                            ? ahora
                            : s.UltimaActividad),
                    HttpContext.RequestAborted);

            if (filasActualizadas == 0)
            {
                return Unauthorized(new
                {
                    mensaje = "La sesión no es válida o ha expirado."
                });
            }

            return NoContent();
        }

        [AllowAnonymous]
        [HttpPost("refresh")]
        [ResponseCache(
            NoStore = true,
            Location = ResponseCacheLocation.None)]
        public async Task<ActionResult<LoginRespuestaDto>> RenovarToken()
        {
            if (Request.Headers["X-CSRF-Protection"].ToString() != "1")
            {
                return StatusCode(403, new
                {
                    mensaje = "Solicitud no permitida."
                });
            }

            if (!Request.Cookies.TryGetValue(
                RefreshTokenCookieName,
                out var refreshTokenOriginal) ||
                string.IsNullOrWhiteSpace(refreshTokenOriginal) ||
                refreshTokenOriginal.Length != 86)
            {
                return Unauthorized(new
                {
                    mensaje = "La sesión no es válida o ha expirado."
                });
            }

            var tokenHash = _jwtService.CalcularHashRefreshToken(
                refreshTokenOriginal);

            try
            {
                await using var transaccion =
                    await _context.Database.BeginTransactionAsync(
                        IsolationLevel.Serializable);

                var refreshToken = await _context.RefreshTokens
                    .Include(rt => rt.SesionUsuario)
                    .ThenInclude(s => s.Usuario)
                    .FirstOrDefaultAsync(rt =>
                        rt.TokenHash == tokenHash);

                if (refreshToken == null)
                {
                    return Unauthorized(new
                    {
                        mensaje = "La sesión no es válida o ha expirado."
                    });
                }

                var sesion = refreshToken.SesionUsuario;

                var usuario = sesion.Usuario;

                var ahora = DateTime.UtcNow;

                var limiteInactividad = ahora.AddMinutes(
                    -_jwtSettings.SessionIdleTimeoutMinutes);

                if (!usuario.Activo ||
                    sesion.FechaRevocacion.HasValue ||
                    sesion.FechaExpiracion <= ahora ||
                    sesion.UltimaActividad <= limiteInactividad)
                {
                    return Unauthorized(new
                    {
                        mensaje = "La sesión no es válida o ha expirado."
                    });
                }

                if (refreshToken.FechaUtilizacion.HasValue)
                {
                    const string motivo =
                        "Reutilización de Refresh Token.";

                    sesion.FechaRevocacion = ahora;

                    sesion.MotivoRevocacion = motivo;

                    await _context.SaveChangesAsync();

                    var tokensRevocados = await _context.RefreshTokens
                        .Where(rt =>
                            rt.SesionUsuarioId == sesion.Id &&
                            rt.FechaRevocacion == null)
                        .ExecuteUpdateAsync(setters => setters
                            .SetProperty(
                                rt => rt.FechaRevocacion,
                                (DateTime?)ahora)
                            .SetProperty(
                                rt => rt.MotivoRevocacion,
                                motivo));

                    _auditoriaService.Registrar(
                        null,
                        "REUTILIZACION_REFRESH_TOKEN",
                        "SesionUsuario",
                        sesion.Id.ToString(),
                        $"Se detectó la reutilización de un Refresh Token. " +
                        $"Usuario afectado ID: {usuario.Id}. " +
                        $"Sesión invalidada. " +
                        $"Tokens revocados: {tokensRevocados}.");

                    await _context.SaveChangesAsync();

                    await transaccion.CommitAsync();

                    return Unauthorized(new
                    {
                        mensaje = "La sesión ha sido invalidada. " +
                                  "Inicie sesión nuevamente."
                    });
                }

                if (refreshToken.FechaRevocacion.HasValue ||
                    refreshToken.FechaExpiracion <= ahora)
                {
                    return Unauthorized(new
                    {
                        mensaje = "La sesión no es válida o ha expirado."
                    });
                }

                var fechaExpiracionSesion = DateTime.SpecifyKind(
                    sesion.FechaExpiracion,
                    DateTimeKind.Utc);

                refreshToken.FechaUtilizacion = ahora;

                await _context.SaveChangesAsync();

                var nuevoRefreshTokenOriginal =
                    _jwtService.GenerarRefreshToken();

                var fechaExpiracionRefreshToken = ahora.AddHours(
                    _jwtSettings.RefreshTokenExpirationHours);

                if (fechaExpiracionRefreshToken > fechaExpiracionSesion)
                {
                    fechaExpiracionRefreshToken =
                        fechaExpiracionSesion;
                }

                var nuevoRefreshToken = new RefreshToken
                {
                    SesionUsuarioId = sesion.Id,

                    TokenHash = _jwtService.CalcularHashRefreshToken(
                        nuevoRefreshTokenOriginal),

                    FechaCreacion = ahora,

                    FechaExpiracion = fechaExpiracionRefreshToken
                };

                _context.RefreshTokens.Add(nuevoRefreshToken);

                await _context.SaveChangesAsync();

                refreshToken.ReemplazadoPorId = nuevoRefreshToken.Id;

                await _context.SaveChangesAsync();

                var accessToken = _jwtService.GenerarAccessToken(
                    usuario,
                    sesion);

                var jwt = new JwtSecurityTokenHandler()
                    .ReadJwtToken(accessToken);

                var fechaExpiracionInactividad = DateTime.SpecifyKind(
                    sesion.UltimaActividad.AddMinutes(
                        _jwtSettings.SessionIdleTimeoutMinutes),
                    DateTimeKind.Utc);

                if (fechaExpiracionInactividad > fechaExpiracionSesion)
                {
                    fechaExpiracionInactividad =
                        fechaExpiracionSesion;
                }

                await transaccion.CommitAsync();

                Response.Cookies.Append(
                    RefreshTokenCookieName,
                    nuevoRefreshTokenOriginal,
                    new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.Strict,
                        Path = "/api/auth",
                        IsEssential = true,
                        Expires = new DateTimeOffset(
                            fechaExpiracionRefreshToken)
                    });

                var respuesta = new LoginRespuestaDto
                {
                    UsuarioId = usuario.Id,

                    Nombre = usuario.Nombre,

                    Apellido = usuario.Apellido,

                    NombreUsuario = usuario.NombreUsuario,

                    SesionId = sesion.Id,

                    AccessToken = accessToken,

                    TokenType = "Bearer",

                    FechaExpiracionAccessToken = jwt.ValidTo,

                    FechaExpiracionSesion = fechaExpiracionSesion,

                    FechaExpiracionInactividad =
                        fechaExpiracionInactividad,

                    AdvertenciaInactividadSegundos =
                        _jwtSettings.SessionWarningSeconds
                };

                return Ok(respuesta);
            }
            catch (SqlException ex) when (ex.Number == 1205)
            {
                return Conflict(new
                {
                    mensaje = "Conflicto durante la renovación de la sesión."
                });
            }
            catch (DbUpdateException ex)
                when (ex.InnerException is SqlException sqlException &&
                      sqlException.Number == 1205)
            {
                return Conflict(new
                {
                    mensaje = "Conflicto durante la renovación de la sesión."
                });
            }
        }

        [AllowAnonymous]
        [HttpPost("/api/auth/logout")]
        [ResponseCache(
            NoStore = true,
            Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> CerrarSesion()
        {
            if (Request.Headers["X-CSRF-Protection"].ToString() != "1")
            {
                return StatusCode(403, new
                {
                    mensaje = "Solicitud no permitida."
                });
            }

            try
            {
                await using var transaccion =
                    await _context.Database.BeginTransactionAsync(
                        IsolationLevel.Serializable);

                var ahora = DateTime.UtcNow;

                Guid? sesionId = null;

                if (User.Identity?.IsAuthenticated == true)
                {
                    var usuarioIdClaim = User.FindFirst("sub")?.Value;
                    var sesionIdClaim = User.FindFirst("sid")?.Value;

                    if (int.TryParse(usuarioIdClaim, out var usuarioId) &&
                        usuarioId > 0 &&
                        Guid.TryParse(sesionIdClaim, out var sesionAutenticadaId) &&
                        sesionAutenticadaId != Guid.Empty)
                    {
                        sesionId = await _context.SesionesUsuarios
                            .AsNoTracking()
                            .Where(s =>
                                s.Id == sesionAutenticadaId &&
                                s.UsuarioId == usuarioId &&
                                s.Usuario.Activo &&
                                s.FechaRevocacion == null)
                            .Select(s => (Guid?)s.Id)
                            .FirstOrDefaultAsync();
                    }
                }

                if (!sesionId.HasValue &&
                    Request.Cookies.TryGetValue(
                        RefreshTokenCookieName,
                        out var refreshTokenOriginal) &&
                    !string.IsNullOrWhiteSpace(refreshTokenOriginal) &&
                    refreshTokenOriginal.Length == 86)
                {
                    var tokenHash = _jwtService.CalcularHashRefreshToken(
                        refreshTokenOriginal);

                    sesionId = await _context.RefreshTokens
                        .AsNoTracking()
                        .Where(rt =>
                            rt.TokenHash == tokenHash &&
                            rt.FechaUtilizacion == null &&
                            rt.FechaRevocacion == null &&
                            rt.FechaExpiracion > ahora)
                        .Select(rt => (Guid?)rt.SesionUsuarioId)
                        .FirstOrDefaultAsync();
                }

                if (sesionId.HasValue)
                {
                    const string motivo =
                        "Cierre de sesión voluntario.";

                    var usuarioSesionId = await _context.SesionesUsuarios
                        .AsNoTracking()
                        .Where(s => s.Id == sesionId.Value)
                        .Select(s => (int?)s.UsuarioId)
                        .FirstOrDefaultAsync();

                    var sesionesRevocadas = await _context.SesionesUsuarios
                        .Where(s =>
                            s.Id == sesionId.Value &&
                            s.FechaRevocacion == null)
                        .ExecuteUpdateAsync(setters => setters
                            .SetProperty(
                                s => s.FechaRevocacion,
                                (DateTime?)ahora)
                            .SetProperty(
                                s => s.MotivoRevocacion,
                                motivo));

                    var tokensRevocados = await _context.RefreshTokens
                        .Where(rt =>
                            rt.SesionUsuarioId == sesionId.Value &&
                            rt.FechaRevocacion == null)
                        .ExecuteUpdateAsync(setters => setters
                            .SetProperty(
                                rt => rt.FechaRevocacion,
                                (DateTime?)ahora)
                            .SetProperty(
                                rt => rt.MotivoRevocacion,
                                motivo));

                    if (sesionesRevocadas > 0 || tokensRevocados > 0)
                    {
                        _auditoriaService.Registrar(
                            usuarioSesionId,
                            "CERRAR_SESION",
                            "SesionUsuario",
                            sesionId.Value.ToString(),
                            $"Cierre de sesión voluntario. " +
                            $"Sesiones revocadas: {sesionesRevocadas}. " +
                            $"Tokens revocados: {tokensRevocados}.");

                        await _context.SaveChangesAsync();
                    }
                }

                await transaccion.CommitAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new
                {
                    mensaje = "La sesión fue modificada por otra operación."
                });
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is SqlException sqlEx &&
                sqlEx.Number == 1205)
            {
                return Conflict(new
                {
                    mensaje = "Conflicto durante el cierre de sesión."
                });
            }
            catch (SqlException ex) when (ex.Number == 1205)
            {
                return Conflict(new
                {
                    mensaje = "Conflicto durante el cierre de sesión."
                });
            }

            Response.Cookies.Delete(
                RefreshTokenCookieName,
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict,
                    Path = "/api/auth",
                    IsEssential = true
                });

            return NoContent();
        }

        [Authorize]
        [HttpPost("logout-all")]
        [ResponseCache(
            NoStore = true,
            Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> CerrarTodasLasSesiones()
        {
            if (Request.Headers["X-CSRF-Protection"].ToString() != "1")
            {
                return StatusCode(403, new
                {
                    mensaje = "Solicitud no permitida."
                });
            }

            var usuarioIdClaim = User.FindFirst("sub")?.Value;

            if (!int.TryParse(usuarioIdClaim, out var usuarioId) ||
                usuarioId <= 0)
            {
                return Unauthorized(new
                {
                    mensaje = "El usuario no es válido."
                });
            }

            try
            {
                await using var transaccion =
                    await _context.Database.BeginTransactionAsync(
                        IsolationLevel.Serializable);

                var ahora = DateTime.UtcNow;

                const string motivo =
                    "Cierre voluntario de todas las sesiones.";

                var sesionesRevocadas = await _context.SesionesUsuarios
                    .Where(s =>
                        s.UsuarioId == usuarioId &&
                        s.FechaRevocacion == null)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(
                            s => s.FechaRevocacion,
                            (DateTime?)ahora)
                        .SetProperty(
                            s => s.MotivoRevocacion,
                            motivo));

                var tokensRevocados = await _context.RefreshTokens
                    .Where(rt =>
                        rt.SesionUsuario.UsuarioId == usuarioId &&
                        rt.FechaRevocacion == null)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(
                            rt => rt.FechaRevocacion,
                            (DateTime?)ahora)
                        .SetProperty(
                            rt => rt.MotivoRevocacion,
                            motivo));

                if (sesionesRevocadas > 0 || tokensRevocados > 0)
                {
                    _auditoriaService.Registrar(
                        usuarioId,
                        "CERRAR_TODAS_SESIONES",
                        "Usuario",
                        usuarioId.ToString(),
                        $"Cierre voluntario de todas las sesiones. " +
                        $"Sesiones revocadas: {sesionesRevocadas}. " +
                        $"Tokens revocados: {tokensRevocados}.");

                    await _context.SaveChangesAsync();
                }

                await transaccion.CommitAsync();
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is SqlException sqlEx &&
                sqlEx.Number == 1205)
            {
                return Conflict(new
                {
                    mensaje = "Conflicto durante el cierre de todas las sesiones."
                });
            }
            catch (SqlException ex) when (ex.Number == 1205)
            {
                return Conflict(new
                {
                    mensaje = "Conflicto durante el cierre de todas las sesiones."
                });
            }

            Response.Cookies.Delete(
                RefreshTokenCookieName,
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict,
                    Path = "/api/auth",
                    IsEssential = true
                });

            return NoContent();
        }

        [Authorize]
        [HttpGet("sesiones")]
        [ResponseCache(
            NoStore = true,
            Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> ObtenerSesionesActivas()
        {
            var usuarioIdClaim = User.FindFirst("sub")?.Value;

            var sesionIdClaim = User.FindFirst("sid")?.Value;

            if (!int.TryParse(usuarioIdClaim, out var usuarioId) ||
                usuarioId <= 0 ||
                !Guid.TryParse(sesionIdClaim, out var sesionId) ||
                sesionId == Guid.Empty)
            {
                return Unauthorized(new
                {
                    mensaje = "La sesión no es válida."
                });
            }

            var ahora = DateTime.UtcNow;

            var limiteInactividad = ahora.AddMinutes(
                -_jwtSettings.SessionIdleTimeoutMinutes);

            var sesiones = await _context.SesionesUsuarios
                .AsNoTracking()
                .Where(s =>
                    s.UsuarioId == usuarioId &&
                    s.Usuario.Activo &&
                    s.FechaRevocacion == null &&
                    s.FechaExpiracion > ahora &&
                    s.UltimaActividad > limiteInactividad)
                .OrderByDescending(s => s.Id == sesionId)
                .ThenByDescending(s => s.UltimaActividad)
                .Select(s => new
                {
                    s.Id,
                    s.FechaCreacion,
                    s.UltimaActividad,
                    s.FechaExpiracion,

                    FechaExpiracionInactividad =
                        s.UltimaActividad.AddMinutes(
                            _jwtSettings.SessionIdleTimeoutMinutes)
                            < s.FechaExpiracion
                        ? s.UltimaActividad.AddMinutes(
                            _jwtSettings.SessionIdleTimeoutMinutes)
                        : s.FechaExpiracion,

                    s.DireccionIp,
                    s.UserAgent,

                    EsSesionActual = s.Id == sesionId
                })
                .ToListAsync(HttpContext.RequestAborted);

            return Ok(new
            {
                CantidadSesiones = sesiones.Count,
                Sesiones = sesiones
            });
        }

        [Authorize]
        [HttpPost("/api/auth/sesiones/{id:guid}/revocar")]
        [ResponseCache(
            NoStore = true,
            Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> RevocarSesion(
            [FromRoute] Guid id)
        {
            if (Request.Headers["X-CSRF-Protection"].ToString() != "1")
            {
                return StatusCode(403, new
                {
                    mensaje = "Solicitud no permitida."
                });
            }

            var usuarioIdClaim = User.FindFirst("sub")?.Value;

            var sesionIdClaim = User.FindFirst("sid")?.Value;

            if (!int.TryParse(usuarioIdClaim, out var usuarioId) ||
                usuarioId <= 0 ||
                !Guid.TryParse(sesionIdClaim, out var sesionActualId) ||
                sesionActualId == Guid.Empty)
            {
                return Unauthorized(new
                {
                    mensaje = "La sesión no es válida."
                });
            }

            if (id == Guid.Empty)
            {
                return BadRequest(new
                {
                    mensaje = "El identificador de la sesión no es válido."
                });
            }

            if (id == sesionActualId)
            {
                return BadRequest(new
                {
                    mensaje = "Para cerrar la sesión actual, utilice el cierre de sesión."
                });
            }

            try
            {
                await using var transaccion =
                    await _context.Database.BeginTransactionAsync(
                        IsolationLevel.Serializable);

                var ahora = DateTime.UtcNow;

                var sesion = await _context.SesionesUsuarios
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s =>
                        s.Id == id &&
                        s.UsuarioId == usuarioId);

                if (sesion == null)
                {
                    return NotFound(new
                    {
                        mensaje = "Sesión no encontrada."
                    });
                }

                if (sesion.FechaRevocacion.HasValue)
                {
                    return NoContent();
                }

                const string motivo =
                    "Cierre remoto de sesión por el usuario.";

                var sesionesRevocadas = await _context.SesionesUsuarios
                    .Where(s =>
                        s.Id == id &&
                        s.UsuarioId == usuarioId &&
                        s.FechaRevocacion == null)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(
                            s => s.FechaRevocacion,
                            (DateTime?)ahora)
                        .SetProperty(
                            s => s.MotivoRevocacion,
                            motivo));

                var tokensRevocados = await _context.RefreshTokens
                    .Where(rt =>
                        rt.SesionUsuarioId == id &&
                        rt.FechaRevocacion == null)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(
                            rt => rt.FechaRevocacion,
                            (DateTime?)ahora)
                        .SetProperty(
                            rt => rt.MotivoRevocacion,
                            motivo));

                if (sesionesRevocadas > 0 || tokensRevocados > 0)
                {
                    _auditoriaService.Registrar(
                        usuarioId,
                        "REVOCAR_SESION",
                        "SesionUsuario",
                        id.ToString(),
                        $"Cierre remoto de sesión por el usuario. " +
                        $"Sesiones revocadas: {sesionesRevocadas}. " +
                        $"Tokens revocados: {tokensRevocados}.");

                    await _context.SaveChangesAsync();
                }

                await transaccion.CommitAsync();

                return NoContent();
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is SqlException sqlEx &&
                sqlEx.Number == 1205)
            {
                return Conflict(new
                {
                    mensaje = "Conflicto durante la revocación de la sesión."
                });
            }
            catch (SqlException ex) when (ex.Number == 1205)
            {
                return Conflict(new
                {
                    mensaje = "Conflicto durante la revocación de la sesión."
                });
            }
        }
    }
}


