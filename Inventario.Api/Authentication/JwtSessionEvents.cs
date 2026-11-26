
using Inventario.Api.Data;
using Inventario.Api.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Inventario.Api.Authentication
{
    public class JwtSessionEvents : JwtBearerEvents
    {
        private readonly InventarioDbContext _context;
        private readonly JwtSettings _jwtSettings;

        public JwtSessionEvents(
            InventarioDbContext context,
            IOptions<JwtSettings> jwtOptions)
        {
            _context = context;
            _jwtSettings = jwtOptions.Value;
        }

        public override async Task TokenValidated(
            TokenValidatedContext context)
        {
            var usuarioIdClaim = context.Principal?
                .FindFirst("sub")?.Value;

            var sesionIdClaim = context.Principal?
                .FindFirst("sid")?.Value;

            if (!int.TryParse(usuarioIdClaim, out var usuarioId) ||
                usuarioId <= 0 ||
                !Guid.TryParse(sesionIdClaim, out var sesionId) ||
                sesionId == Guid.Empty)
            {
                context.Fail("Token inválido.");
                return;
            }

            var ahora = DateTime.UtcNow;

            var limiteInactividad = ahora.AddMinutes(
                -_jwtSettings.SessionIdleTimeoutMinutes);

            var sesionValida = await _context.SesionesUsuarios
                .AsNoTracking()
                .AnyAsync(s =>
                    s.Id == sesionId &&
                    s.UsuarioId == usuarioId &&
                    s.Usuario.Activo &&
                    s.FechaRevocacion == null &&
                    s.FechaExpiracion > ahora &&
                    s.UltimaActividad > limiteInactividad,
                    context.HttpContext.RequestAborted);

            if (!sesionValida)
            {
                context.Fail(
                    "La sesión no es válida o ha expirado.");

                return;
            }
        }
    }
}

