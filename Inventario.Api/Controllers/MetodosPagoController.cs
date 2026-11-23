
using Inventario.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Api.Controllers
{
    [ApiController]
    [Route("api/metodospago")]
    public class MetodosPagoController : ControllerBase
    {
        private readonly InventarioDbContext _context;

        public MetodosPagoController(InventarioDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerMetodosPago(
            [FromQuery] bool? activo)
        {
            var consulta = _context.MetodosPago.AsNoTracking();

            if (activo.HasValue)
                consulta = consulta.Where(m => m.Activo == activo.Value);

            var metodosPago = await consulta
                .OrderByDescending(m => m.EsEfectivo)
                .ThenBy(m => m.Nombre)
                .Select(m => new
                {
                    m.Id,
                    m.Nombre,
                    m.Descripcion,
                    m.EsEfectivo,
                    m.Activo,
                    m.FechaCreacion
                })
                .ToListAsync();

            return Ok(metodosPago);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> ObtenerMetodoPagoPorId(int id)
        {
            if (id <= 0)
                return BadRequest("El ID debe ser mayor que cero.");

            var metodoPago = await _context.MetodosPago
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == id);

            if (metodoPago == null)
                return NotFound("Método de pago no encontrado.");

            return Ok(metodoPago);
        }
    }
}
