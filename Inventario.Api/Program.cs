using Inventario.Api.Services;
using Inventario.Api.Data;
using Inventario.Api.Models;
using Inventario.Api.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Inventario.Api.Authentication;
using Inventario.Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

var conexion = builder.Configuration
    .GetConnectionString("ConexionSQL")
    ?? throw new InvalidOperationException(
        "No se encontró la cadena de conexión ConexionSQL.");

builder.Services.AddDbContext<InventarioDbContext>(options =>
    options.UseSqlServer(conexion));

builder.Services.AddControllers();

builder.Services.AddProblemDetails();

builder.Services.AddScoped<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();

builder.Services.AddScoped<IJwtService, JwtService>();

builder.Services.AddScoped<JwtSessionEvents>();

builder.Services.AddOpenApi();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode =
        StatusCodes.Status429TooManyRequests;

    options.AddPolicy("login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey:
                context.Connection.RemoteIpAddress?.ToString()
                ?? "desconocida",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

builder.Services.AddOptions<JwtSettings>()
    .Bind(builder.Configuration.GetSection(JwtSettings.SectionName))
    .Validate(settings =>
    {
        if (string.IsNullOrWhiteSpace(settings.SecretKey))
            return false;

        try
        {
            return Convert.FromBase64String(settings.SecretKey).Length >= 64;
        }
        catch (FormatException)
        {
            return false;
        }
    }, "La clave secreta JWT debe ser válida y contener al menos 64 bytes.")
    .Validate(settings =>
        !string.IsNullOrWhiteSpace(settings.Issuer),
        "El emisor JWT es obligatorio.")
    .Validate(settings =>
        !string.IsNullOrWhiteSpace(settings.Audience),
        "La audiencia JWT es obligatoria.")
    .Validate(settings =>
        settings.AccessTokenExpirationMinutes > 0,
        "La duración del Access Token debe ser mayor a cero.")
    .Validate(settings =>
        settings.RefreshTokenExpirationHours > 0,
        "La duración del Refresh Token debe ser mayor a cero.")
    .Validate(settings =>
        settings.SessionIdleTimeoutMinutes > 0,
        "El tiempo de inactividad debe ser mayor a cero.")
    .Validate(settings =>
        settings.SessionMaxDurationHours > 0,
        "La duración máxima de la sesión debe ser mayor a cero.")
    .Validate(settings =>
        settings.SessionIdleTimeoutMinutes <
        settings.SessionMaxDurationHours * 60L,
        "La inactividad debe ser menor a la duración máxima de la sesión.")
    .Validate(settings =>
        settings.SessionWarningSeconds > 0 &&
        settings.SessionWarningSeconds <
        settings.SessionIdleTimeoutMinutes * 60L,
        "El tiempo de advertencia de cierre es inválido.")
    .ValidateOnStart();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services
    .AddOptions<JwtBearerOptions>(
        JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtSettings>>((options, jwtOptions) =>
    {
        var settings = jwtOptions.Value;

        options.EventsType = typeof(JwtSessionEvents);

        options.MapInboundClaims = false;

        options.SaveToken = false;

        options.IncludeErrorDetails = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,

            ValidateAudience = true,

            ValidateLifetime = true,

            ValidateIssuerSigningKey = true,

            RequireExpirationTime = true,

            RequireSignedTokens = true,

            ValidIssuer = settings.Issuer,

            ValidAudience = settings.Audience,

            IssuerSigningKey = new SymmetricSecurityKey(
                Convert.FromBase64String(settings.SecretKey)),

            ValidAlgorithms = new[]
            {
                SecurityAlgorithms.HmacSha256
            },

            ClockSkew = TimeSpan.Zero,

            NameClaimType = "sub"
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddScoped<
    IAuthorizationHandler,
    PermisoAuthorizationHandler>();

builder.Services.AddSingleton<
    IAuthorizationPolicyProvider,
    PermisoPolicyProvider>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseExceptionHandler();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseRateLimiter();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();