using Inventario.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var conexion = builder.Configuration
    .GetConnectionString("ConexionSQL")
    ?? throw new InvalidOperationException(
        "No se encontró la cadena de conexión ConexionSQL.");
builder.Services.AddDbContext<InventarioDbContext>(options =>
    options.UseSqlServer(conexion));
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();

