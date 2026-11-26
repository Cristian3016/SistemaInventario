
using Inventario.Api.Models;
using Inventario.Api.Settings;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Inventario.Api.Services
{
    public class JwtService : IJwtService
    {
        private readonly JwtSettings _jwtSettings;

        public JwtService(IOptions<JwtSettings> jwtOptions)
        {
            _jwtSettings = jwtOptions.Value;
        }

        public string GenerarAccessToken(
            Usuario usuario,
            SesionUsuario sesion)
        {
            var ahora = DateTime.UtcNow;

            var fechaExpiracionSesion = DateTime.SpecifyKind(
                sesion.FechaExpiracion,
                DateTimeKind.Utc);

            if (!usuario.Activo ||
                sesion.UsuarioId != usuario.Id ||
                sesion.FechaRevocacion.HasValue ||
                fechaExpiracionSesion <= ahora)
            {
                throw new InvalidOperationException(
                    "No se puede generar un token para una sesión inválida.");
            }

            var fechaExpiracion = ahora.AddMinutes(
                _jwtSettings.AccessTokenExpirationMinutes);

            if (fechaExpiracion > fechaExpiracionSesion)
            {
                fechaExpiracion = fechaExpiracionSesion;
            }

            var claims = new List<Claim>
            {
                new Claim(
                    JwtRegisteredClaimNames.Sub,
                    usuario.Id.ToString(CultureInfo.InvariantCulture)),

                new Claim(
                    JwtRegisteredClaimNames.Jti,
                    Guid.NewGuid().ToString()),

                new Claim(
                    "sid",
                    sesion.Id.ToString()),

                new Claim(
                    "preferred_username",
                    usuario.NombreUsuario),

                new Claim(
                    JwtRegisteredClaimNames.Iat,
                    new DateTimeOffset(ahora)
                        .ToUnixTimeSeconds()
                        .ToString(CultureInfo.InvariantCulture),
                    ClaimValueTypes.Integer64)
            };

            var clave = new SymmetricSecurityKey(
                Convert.FromBase64String(_jwtSettings.SecretKey));

            var credenciales = new SigningCredentials(
                clave,
                SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: _jwtSettings.Audience,
                claims: claims,
                notBefore: ahora,
                expires: fechaExpiracion,
                signingCredentials: credenciales);

            return new JwtSecurityTokenHandler()
                .WriteToken(token);
        }

        public string GenerarRefreshToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(64);

            return Base64UrlEncoder.Encode(bytes);
        }

        public string CalcularHashRefreshToken(string refreshToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);

            var bytes = Encoding.UTF8.GetBytes(refreshToken);

            var hash = SHA256.HashData(bytes);

            return Convert.ToHexString(hash);
        }
    }
}
