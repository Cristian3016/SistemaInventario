
using Inventario.Api.Models;

namespace Inventario.Api.Services
{
    public interface IJwtService
    {
        string GenerarAccessToken(
            Usuario usuario,
            SesionUsuario sesion);

        string GenerarRefreshToken();

        string CalcularHashRefreshToken(string refreshToken);
    }
}
