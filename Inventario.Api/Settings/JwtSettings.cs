
namespace Inventario.Api.Settings
{
    public class JwtSettings
    {
        public const string SectionName = "Jwt";

        public string SecretKey { get; set; } = string.Empty;

        public string Issuer { get; set; } = string.Empty;

        public string Audience { get; set; } = string.Empty;

        public int AccessTokenExpirationMinutes { get; set; } = 15;

        public int RefreshTokenExpirationHours { get; set; } = 12;

        public int SessionIdleTimeoutMinutes { get; set; } = 15;

        public int SessionMaxDurationHours { get; set; } = 12;

        public int SessionWarningSeconds { get; set; } = 60;
    }
}
