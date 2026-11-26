using Microsoft.Data.SqlClient;
using Inventario.Api.Data;
using Inventario.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Inventario.Api.Authorization;

namespace Inventario.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UnidadesMedidaController : ControllerBase
    {
        private readonly InventarioDbContext _context;

        public UnidadesMedidaController(InventarioDbContext context)
        {
            _context = context;
        }

        [Permiso(Permisos.UnidadesGestionar)]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<UnidadMedida>>> ObtenerUnidades()
        {
            var unidades = await _context.UnidadesMedida
                .AsNoTracking()
                .OrderBy(u => u.Nombre)
                .ToListAsync();

            return Ok(unidades);
        }

        [Permiso(Permisos.UnidadesGestionar)]
        [HttpGet("{id:int}")]
        public async Task<ActionResult<UnidadMedida>> ObtenerUnidadPorId(int id)
        {
            var unidad = await _context.UnidadesMedida
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == id);

            if (unidad == null)
            {
                return NotFound("Unidad de medida no encontrada.");
            }

            return Ok(unidad);
        }

        [Permiso(Permisos.UnidadesGestionar)]
        [HttpPost]
        public async Task<ActionResult<UnidadMedida>> CrearUnidad(
     [FromBody] UnidadMedida nuevaUnidad)
        {
            if (string.IsNullOrWhiteSpace(nuevaUnidad.Nombre) ||
                string.IsNullOrWhiteSpace(nuevaUnidad.Abreviatura))
            {
                return BadRequest(
                    "El nombre y la abreviatura son obligatorios.");
            }

            var nombre = nuevaUnidad.Nombre.Trim();
            var abreviatura = nuevaUnidad.Abreviatura.Trim();

            try
            {
                await using var transaccion = await _context.Database
                    .BeginTransactionAsync(
                        System.Data.IsolationLevel.Serializable);

                var existe = await _context.UnidadesMedida
                    .AnyAsync(u =>
                        u.Nombre.ToLower() == nombre.ToLower() ||
                        u.Abreviatura.ToLower() == abreviatura.ToLower());

                if (existe)
                {
                    return Conflict(
                        "Ya existe una unidad de medida con ese nombre o abreviatura.");
                }

                var unidad = new UnidadMedida
                {
                    Nombre = nombre,
                    Abreviatura = abreviatura,
                    PermiteFracciones = nuevaUnidad.PermiteFracciones
                };

                _context.UnidadesMedida.Add(unidad);

                await _context.SaveChangesAsync();

                await transaccion.CommitAsync();

                return CreatedAtAction(
                    nameof(ObtenerUnidadPorId),
                    new { id = unidad.Id },
                    unidad);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(
                    "La unidad de medida fue modificada por otra operación.");
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is SqlException sqlEx &&
                (sqlEx.Number == 2601 || sqlEx.Number == 2627))
            {
                return Conflict(
                    "Ya existe una unidad de medida con esos datos.");
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is SqlException sqlEx &&
                sqlEx.Number == 1205)
            {
                return Conflict(
                    "Existe una operación simultánea. Intente nuevamente.");
            }
            catch (SqlException ex) when (ex.Number == 1205)
            {
                return Conflict(
                    "Existe una operación simultánea. Intente nuevamente.");
            }
        }
    }
}
