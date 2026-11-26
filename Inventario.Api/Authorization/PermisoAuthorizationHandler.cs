
using Inventario.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Api.Authorization
{
    public class PermisoAuthorizationHandler
        : AuthorizationHandler<PermisoRequirement>
    {
        private readonly InventarioDbContext _context;

        public PermisoAuthorizationHandler(
            InventarioDbContext context)
        {
            _context = context;
        }

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            PermisoRequirement requirement)
        {
            if (context.User.Identity?.IsAuthenticated != true)
            {
                return;
            }

            var usuarioIdClaim = context.User.FindFirst("sub")?.Value;

            if (!int.TryParse(usuarioIdClaim, out var usuarioId) ||
                usuarioId <= 0)
            {
                return;
            }

            var tienePermiso = await _context.RolesPermisos
                .AsNoTracking()
                .AnyAsync(rp =>
                    rp.Rol.Activo &&
                    rp.Permiso.Activo &&
                    rp.Permiso.Codigo == requirement.CodigoPermiso &&
                    _context.UsuariosRoles.Any(ur =>
                        ur.UsuarioId == usuarioId &&
                        ur.Usuario.Activo &&
                        ur.RolId == rp.RolId));

            if (tienePermiso)
            {
                context.Succeed(requirement);
            }
        }
    }
}
