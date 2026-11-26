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
    public class CategoriasController : ControllerBase
    {
        private readonly InventarioDbContext _context;

        public CategoriasController(InventarioDbContext context)
        {
            _context = context;
        }
        private async Task<ActionResult> EjecutarEscrituraCategoria(
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
                    "La categoría fue modificada por otra operación.");
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is SqlException sqlEx &&
                (sqlEx.Number == 2601 || sqlEx.Number == 2627))
            {
                return Conflict(
                    "Ya existe una categoría con esos datos.");
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
        [Permiso(Permisos.CategoriasGestionar)]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Categoria>>> ObtenerCategorias()
        {
            var categorias = await _context.Categorias
                .AsNoTracking()
                .ToListAsync();

            return Ok(categorias);
        }

        [Permiso(Permisos.CategoriasGestionar)]
        [HttpGet("{id:int}")]
        public async Task<ActionResult<Categoria>> ObtenerCategoriaPorId(int id)
        {
            var categoria = await _context.Categorias
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id);

            if (categoria == null)
            {
                return NotFound("Categoría no encontrada.");
            }

            return Ok(categoria);
        }

        [Permiso(Permisos.CategoriasGestionar)]
        [HttpPost]
        public async Task<ActionResult<Categoria>> CrearCategoria(
    [FromBody] Categoria nuevaCategoria)
        {
            return await EjecutarEscrituraCategoria(async () =>
            {
                if (string.IsNullOrWhiteSpace(nuevaCategoria.Nombre))
                    return BadRequest(
                        "El nombre de la categoría es obligatorio.");

                var nombre = nuevaCategoria.Nombre.Trim();

                if (nuevaCategoria.CategoriaPadreId.HasValue)
                {
                    if (nuevaCategoria.CategoriaPadreId.Value <= 0)
                        return BadRequest(
                            "La categoría padre no es válida.");

                    var existePadre = await _context.Categorias
                        .AnyAsync(c =>
                            c.Id == nuevaCategoria.CategoriaPadreId.Value);

                    if (!existePadre)
                        return BadRequest(
                            "La categoría padre indicada no existe.");
                }

                var existe = await _context.Categorias
                    .AnyAsync(c =>
                        c.Nombre.ToLower() == nombre.ToLower() &&
                        c.CategoriaPadreId ==
                            nuevaCategoria.CategoriaPadreId);

                if (existe)
                    return Conflict(
                        "Ya existe una categoría con ese nombre en el mismo nivel.");

                var categoria = new Categoria
                {
                    Nombre = nombre,
                    Descripcion = nuevaCategoria.Descripcion?.Trim()
                        ?? string.Empty,
                    CategoriaPadreId = nuevaCategoria.CategoriaPadreId
                };

                _context.Categorias.Add(categoria);

                await _context.SaveChangesAsync();

                return CreatedAtAction(
                    nameof(ObtenerCategoriaPorId),
                    new { id = categoria.Id },
                    categoria);
            });
        }

        [Permiso(Permisos.CategoriasGestionar)]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> ActualizarCategoria(
    int id, [FromBody] Categoria categoriaActualizada)
        {
            return await EjecutarEscrituraCategoria(async () =>
            {
                if (id <= 0)
                    return BadRequest(
                        "El ID debe ser mayor que cero.");

                var categoria = await _context.Categorias.FindAsync(id);

                if (categoria == null)
                    return NotFound("Categoría no encontrada.");

                if (string.IsNullOrWhiteSpace(categoriaActualizada.Nombre))
                    return BadRequest(
                        "El nombre de la categoría es obligatorio.");

                var nombre = categoriaActualizada.Nombre.Trim();

                if (categoriaActualizada.CategoriaPadreId.HasValue)
                {
                    var padreId =
                        categoriaActualizada.CategoriaPadreId.Value;

                    if (padreId <= 0)
                        return BadRequest(
                            "La categoría padre no es válida.");

                    if (padreId == id)
                        return BadRequest(
                            "Una categoría no puede ser su propia categoría padre.");

                    var existePadre = await _context.Categorias
                        .AnyAsync(c => c.Id == padreId);

                    if (!existePadre)
                        return BadRequest(
                            "La categoría padre indicada no existe.");

                    int? ancestroId = padreId;

                    var visitados = new HashSet<int>();

                    while (ancestroId.HasValue)
                    {
                        if (ancestroId.Value == id)
                            return BadRequest(
                                "No se puede crear una relación circular entre categorías.");

                        if (!visitados.Add(ancestroId.Value))
                            return BadRequest(
                                "Se detectó una relación circular entre categorías.");

                        var actual = ancestroId.Value;

                        ancestroId = await _context.Categorias
                            .Where(c => c.Id == actual)
                            .Select(c => c.CategoriaPadreId)
                            .FirstOrDefaultAsync();
                    }
                }

                var existe = await _context.Categorias
                    .AnyAsync(c =>
                        c.Id != id &&
                        c.Nombre.ToLower() == nombre.ToLower() &&
                        c.CategoriaPadreId ==
                            categoriaActualizada.CategoriaPadreId);

                if (existe)
                    return Conflict(
                        "Ya existe otra categoría con ese nombre en el mismo nivel.");

                categoria.Nombre = nombre;

                categoria.Descripcion =
                    categoriaActualizada.Descripcion?.Trim()
                    ?? string.Empty;

                categoria.CategoriaPadreId =
                    categoriaActualizada.CategoriaPadreId;

                await _context.SaveChangesAsync();

                return NoContent();
            });
        }

        [Permiso(Permisos.CategoriasGestionar)]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> EliminarCategoria(int id)
        {
            return await EjecutarEscrituraCategoria(async () =>
            {
                if (id <= 0)
                    return BadRequest(
                        "El ID debe ser mayor que cero.");

                var categoria = await _context.Categorias.FindAsync(id);

                if (categoria == null)
                    return NotFound("Categoría no encontrada.");

                var tieneProductos = await _context.Productos
                    .AnyAsync(p => p.CategoriaId == id);

                if (tieneProductos)
                    return Conflict(
                        "No se puede eliminar una categoría que tiene productos asociados.");

                var tieneSubcategorias = await _context.Categorias
                    .AnyAsync(c => c.CategoriaPadreId == id);

                if (tieneSubcategorias)
                    return Conflict(
                        "No se puede eliminar una categoría que tiene subcategorías asociadas.");

                _context.Categorias.Remove(categoria);

                await _context.SaveChangesAsync();

                return NoContent();
            });
        }
    }
}
