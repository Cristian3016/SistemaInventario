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
    public class MarcasController : ControllerBase
    {
        private readonly InventarioDbContext _context;

        public MarcasController(InventarioDbContext context)
        {
            _context = context;
        }
        private async Task<ActionResult> EjecutarEscrituraMarca(
    Func<Task<ActionResult>> operacion)
        {
            try
            {
                await using var transaccion = await _context.Database
                    .BeginTransactionAsync(
                        System.Data.IsolationLevel.Serializable);

                var resultado = await operacion();

                await transaccion.CommitAsync();

                return resultado;
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(
                    "La marca fue modificada por otra operación.");
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is SqlException sqlEx &&
                (sqlEx.Number == 2601 || sqlEx.Number == 2627))
            {
                return Conflict(
                    "Ya existe una marca con esos datos.");
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is SqlException sqlEx &&
                sqlEx.Number == 547)
            {
                return Conflict(
                    "La operación no puede realizarse debido a registros relacionados.");
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

        [Permiso(Permisos.MarcasGestionar)]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Marca>>> ObtenerMarcas()
        {
            var marcas = await _context.Marcas
                .AsNoTracking()
                .OrderBy(m => m.Nombre)
                .ToListAsync();

            return Ok(marcas);
        }

        [Permiso(Permisos.MarcasGestionar)]
        [HttpGet("{id:int}")]
        public async Task<ActionResult<Marca>> ObtenerMarcaPorId(int id)
        {
            var marca = await _context.Marcas
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == id);

            if (marca == null)
            {
                return NotFound("Marca no encontrada.");
            }

            return Ok(marca);
        }

        [Permiso(Permisos.MarcasGestionar)]
        [HttpPost]
        public async Task<ActionResult<Marca>> CrearMarca(
    [FromBody] Marca nuevaMarca)
        {
            return await EjecutarEscrituraMarca(async () =>
            {
                if (string.IsNullOrWhiteSpace(nuevaMarca.Nombre))
                    return BadRequest(
                        "El nombre de la marca es obligatorio.");

                var nombre = nuevaMarca.Nombre.Trim();

                var existeMarca = await _context.Marcas
                    .AnyAsync(m =>
                        m.Nombre.ToLower() == nombre.ToLower());

                if (existeMarca)
                    return Conflict(
                        "Ya existe una marca con ese nombre.");

                var marca = new Marca
                {
                    Nombre = nombre,
                    Descripcion = nuevaMarca.Descripcion?.Trim()
                        ?? string.Empty
                };

                _context.Marcas.Add(marca);

                await _context.SaveChangesAsync();

                return CreatedAtAction(
                    nameof(ObtenerMarcaPorId),
                    new { id = marca.Id },
                    marca);
            });
        }

        [Permiso(Permisos.MarcasGestionar)]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> ActualizarMarca(
    int id, [FromBody] Marca marcaActualizada)
        {
            return await EjecutarEscrituraMarca(async () =>
            {
                if (id <= 0)
                    return BadRequest(
                        "El ID debe ser mayor que cero.");

                var marca = await _context.Marcas.FindAsync(id);

                if (marca == null)
                    return NotFound("Marca no encontrada.");

                if (string.IsNullOrWhiteSpace(marcaActualizada.Nombre))
                    return BadRequest(
                        "El nombre de la marca es obligatorio.");

                var nombre = marcaActualizada.Nombre.Trim();

                var existeMarca = await _context.Marcas
                    .AnyAsync(m =>
                        m.Id != id &&
                        m.Nombre.ToLower() == nombre.ToLower());

                if (existeMarca)
                    return Conflict(
                        "Ya existe otra marca con ese nombre.");

                marca.Nombre = nombre;

                marca.Descripcion =
                    marcaActualizada.Descripcion?.Trim()
                    ?? string.Empty;

                await _context.SaveChangesAsync();

                return NoContent();
            });
        }

        [Permiso(Permisos.MarcasGestionar)]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> EliminarMarca(int id)
        {
            return await EjecutarEscrituraMarca(async () =>
            {
                if (id <= 0)
                    return BadRequest(
                        "El ID debe ser mayor que cero.");

                var marca = await _context.Marcas.FindAsync(id);

                if (marca == null)
                    return NotFound("Marca no encontrada.");

                var tieneProductos = await _context.Productos
                    .AnyAsync(p => p.MarcaId == id);

                if (tieneProductos)
                    return Conflict(
                        "No se puede eliminar una marca que tiene productos asociados.");

                _context.Marcas.Remove(marca);

                await _context.SaveChangesAsync();

                return NoContent();
            });
        }
    }
}
