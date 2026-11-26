
using Microsoft.AspNetCore.Authorization;

namespace Inventario.Api.Authorization
{
    [AttributeUsage(
        AttributeTargets.Class | AttributeTargets.Method,
        AllowMultiple = true,
        Inherited = true)]
    public class PermisoAttribute : AuthorizeAttribute
    {
        public const string PrefijoPolitica = "Permiso:";

        public PermisoAttribute(string codigoPermiso)
        {
            Policy = PrefijoPolitica + codigoPermiso;
        }
    }
}
