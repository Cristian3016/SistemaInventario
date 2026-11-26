
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Inventario.Api.Authorization
{
    public class PermisoPolicyProvider
        : DefaultAuthorizationPolicyProvider
    {
        public PermisoPolicyProvider(
            IOptions<AuthorizationOptions> options)
            : base(options)
        {
        }

        public override Task<AuthorizationPolicy?> GetPolicyAsync(
            string policyName)
        {
            if (!policyName.StartsWith(
                PermisoAttribute.PrefijoPolitica,
                StringComparison.Ordinal))
            {
                return base.GetPolicyAsync(policyName);
            }

            var codigoPermiso = policyName[
                PermisoAttribute.PrefijoPolitica.Length..];

            if (string.IsNullOrWhiteSpace(codigoPermiso))
            {
                return Task.FromResult<AuthorizationPolicy?>(null);
            }

            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(
                    new PermisoRequirement(codigoPermiso))
                .Build();

            return Task.FromResult<AuthorizationPolicy?>(policy);
        }
    }
}

