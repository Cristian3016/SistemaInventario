
using Microsoft.AspNetCore.Authorization;

namespace Inventario.Api.Authorization
{
    public class PermisoRequirement : IAuthorizationRequirement
    {
        public string CodigoPermiso { get; }

        public PermisoRequirement(string codigoPermiso)
        {
            CodigoPermiso = codigoPermiso;
        }
    }
}

