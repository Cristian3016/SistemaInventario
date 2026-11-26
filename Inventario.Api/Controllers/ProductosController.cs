using Microsoft.Data.SqlClient;
using Inventario.Api.DTOs;
using Inventario.Api.Data;
using Inventario.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Inventario.Api.Authorization;

namespace Inventario.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductosController : ControllerBase
    {
        private readonly InventarioDbContext _context;

        private const decimal CantidadMaxima = 999999999999999.999m;

        public ProductosController(InventarioDbContext context)
        {
            _context = context;
        }

        private static bool TieneFracciones(decimal cantidad)
        {
            return cantidad != decimal.Truncate(cantidad);
        }

        private static bool CantidadFueraDeRango(decimal cantidad)
        {
            return cantidad < 0 ||
                   cantidad > CantidadMaxima ||
                   decimal.Round(cantidad, 3) != cantidad;
        }
        private static bool PrecioFueraDeRango(decimal precio)
        {
            return precio < 0 ||
                   precio > 9999999999999999.99m ||
                   decimal.Round(precio, 2) != precio;
        }

        private async Task<ActionResult> EjecutarEscrituraProducto(
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
                    "El producto fue modificado por otra operación.");
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is SqlException sqlEx &&
                (sqlEx.Number == 2601 || sqlEx.Number == 2627))
            {
                return Conflict(
                    "Existe un registro duplicado.");
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is SqlException sqlEx &&
                sqlEx.Number == 547)
            {
                return Conflict(
                    "La operación no puede realizarse debido a una restricción de la base de datos.");
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

        [Permiso(Permisos.ProductosVer)]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Producto>>> ObtenerProductos(
            [FromQuery] bool? activo)
        {
            IQueryable<Producto> consulta = _context.Productos
                .AsNoTracking()
                .Include(p => p.UnidadMedida)
                .Include(p => p.Marca);

            if (activo.HasValue)
            {
                consulta = consulta.Where(p => p.Activo == activo.Value);
            }

            var productos = await consulta.ToListAsync();

            return Ok(productos);
        }

        [Permiso(Permisos.ProductosVer)]
        [HttpGet("stock-bajo")]
        public async Task<IActionResult> ObtenerProductosConStockBajo()
        {
            var productos = await _context.Productos
                .AsNoTracking()
                .Include(p => p.UnidadMedida)
                .Include(p => p.Marca)
                .Where(p => p.Activo &&
                            p.StockMinimo > 0 &&
                            p.Stock <= p.StockMinimo)
                .OrderBy(p => p.Stock)
                .ToListAsync();

            if (productos.Count == 0)
            {
                return Ok(new
                {
                    mensaje = "No hay productos con stock bajo.",
                    productos
                });
            }

            return Ok(new
            {
                mensaje = "¡Atención! Hay productos con stock bajo. Es necesario reponer mercadería.",
                cantidadProductos = productos.Count,
                productos
            });
        }

        [Permiso(Permisos.ProductosVer)]
        [HttpGet("{id:int}")]
        public async Task<ActionResult<Producto>> ObtenerProductoPorId(int id)
        {
            var producto = await _context.Productos
                .AsNoTracking()
                .Include(p => p.UnidadMedida)
                .Include(p => p.Marca)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (producto == null)
            {
                return NotFound("Producto no encontrado.");
            }

            return Ok(producto);
        }

        [Permiso(Permisos.ProductosGestionar)]
        [HttpPost]
        public async Task<ActionResult<Producto>> CrearProducto(
    [FromBody] Producto nuevoProducto)
        {
            return await EjecutarEscrituraProducto(async () =>
            {
                if (string.IsNullOrWhiteSpace(nuevoProducto.Nombre))
                    return BadRequest("El nombre es obligatorio.");

                if (PrecioFueraDeRango(nuevoProducto.Precio))
                    return BadRequest(
                        "El precio debe estar dentro del rango permitido y tener como máximo dos decimales.");

                if (CantidadFueraDeRango(nuevoProducto.Stock) ||
                    CantidadFueraDeRango(nuevoProducto.StockMinimo))
                {
                    return BadRequest(
                        "El stock y el stock mínimo deben ser positivos o cero, no superar el rango permitido y tener como máximo tres decimales.");
                }

                if (nuevoProducto.CategoriaId.HasValue)
                {
                    var existeCategoria = await _context.Categorias
                        .AnyAsync(c => c.Id == nuevoProducto.CategoriaId.Value);

                    if (!existeCategoria)
                        return BadRequest("La categoría indicada no existe.");
                }

                if (nuevoProducto.MarcaId.HasValue)
                {
                    var existeMarca = await _context.Marcas
                        .AnyAsync(m => m.Id == nuevoProducto.MarcaId.Value);

                    if (!existeMarca)
                        return BadRequest("La marca indicada no existe.");
                }

                UnidadMedida? unidadMedida = null;

                if (nuevoProducto.UnidadMedidaId.HasValue)
                {
                    unidadMedida = await _context.UnidadesMedida
                        .AsNoTracking()
                        .FirstOrDefaultAsync(u =>
                            u.Id == nuevoProducto.UnidadMedidaId.Value);

                    if (unidadMedida == null)
                        return BadRequest(
                            "La unidad de medida indicada no existe.");
                }

                if (TieneFracciones(nuevoProducto.Stock) ||
                    TieneFracciones(nuevoProducto.StockMinimo))
                {
                    if (unidadMedida == null)
                        return BadRequest(
                            "Debes asignar una unidad de medida para utilizar cantidades decimales.");

                    if (!unidadMedida.PermiteFracciones)
                        return BadRequest(
                            "La unidad de medida indicada no permite cantidades decimales.");
                }

                var codigoSku = string.IsNullOrWhiteSpace(
                    nuevoProducto.CodigoSku)
                    ? null
                    : nuevoProducto.CodigoSku.Trim();

                if (codigoSku != null)
                {
                    if (codigoSku.Length > 50)
                        return BadRequest(
                            "El código SKU no puede superar los 50 caracteres.");

                    var existeSku = await _context.Productos
                        .AnyAsync(p => p.CodigoSku == codigoSku);

                    if (existeSku)
                        return Conflict(
                            "Ya existe un producto con ese código SKU.");
                }

                var ahora = DateTime.UtcNow;

                var producto = new Producto
                {
                    Nombre = nuevoProducto.Nombre.Trim(),
                    Descripcion = nuevoProducto.Descripcion?.Trim()
                        ?? string.Empty,
                    CodigoSku = codigoSku,
                    Precio = nuevoProducto.Precio,
                    Stock = nuevoProducto.Stock,
                    StockMinimo = nuevoProducto.StockMinimo,
                    CategoriaId = nuevoProducto.CategoriaId,
                    UnidadMedidaId = nuevoProducto.UnidadMedidaId,
                    MarcaId = nuevoProducto.MarcaId,
                    FechaCreacion = ahora,
                    Activo = true
                };

                _context.Productos.Add(producto);

                if (nuevoProducto.Stock > 0)
                {
                    var movimientoInicial = new MovimientoStock
                    {
                        Producto = producto,
                        Tipo = TipoMovimiento.Entrada,
                        Cantidad = nuevoProducto.Stock,
                        Observacion = "Registro de stock inicial del producto.",
                        FechaMovimiento = ahora
                    };

                    _context.MovimientosStock.Add(movimientoInicial);
                }

                await _context.SaveChangesAsync();

                return CreatedAtAction(
                    nameof(ObtenerProductoPorId),
                    new { id = producto.Id },
                    producto);
            });
        }

        [Permiso(Permisos.ProductosGestionar)]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> ActualizarProducto(
    int id, [FromBody] Producto productoActualizado)
        {
            return await EjecutarEscrituraProducto(async () =>
            {
                if (id <= 0)
                    return BadRequest("El ID debe ser mayor que cero.");

                var producto = await _context.Productos.FindAsync(id);

                if (producto == null)
                    return NotFound("Producto no encontrado.");

                if (!producto.Activo)
                    return Conflict(
                        "No se puede modificar un producto inactivo. Primero debes activarlo.");

                if (productoActualizado.Stock != producto.Stock)
                    return BadRequest(
                        "El stock solo puede modificarse mediante movimientos de inventario.");

                if (string.IsNullOrWhiteSpace(productoActualizado.Nombre))
                    return BadRequest("El nombre es obligatorio.");

                if (PrecioFueraDeRango(productoActualizado.Precio))
                    return BadRequest(
                        "El precio debe estar dentro del rango permitido y tener como máximo dos decimales.");

                if (CantidadFueraDeRango(productoActualizado.StockMinimo))
                    return BadRequest(
                        "El stock mínimo debe estar dentro del rango permitido y tener como máximo tres decimales.");

                var codigoSku = string.IsNullOrWhiteSpace(
                    productoActualizado.CodigoSku)
                    ? null
                    : productoActualizado.CodigoSku.Trim();

                if (codigoSku != null)
                {
                    if (codigoSku.Length > 50)
                        return BadRequest(
                            "El código SKU no puede superar los 50 caracteres.");

                    var existeSku = await _context.Productos
                        .AnyAsync(p =>
                            p.CodigoSku == codigoSku &&
                            p.Id != id);

                    if (existeSku)
                        return Conflict(
                            "Ya existe otro producto con ese código SKU.");
                }

                if (productoActualizado.CategoriaId.HasValue)
                {
                    var existeCategoria = await _context.Categorias
                        .AnyAsync(c =>
                            c.Id == productoActualizado.CategoriaId.Value);

                    if (!existeCategoria)
                        return BadRequest("La categoría indicada no existe.");
                }

                if (productoActualizado.MarcaId.HasValue)
                {
                    var existeMarca = await _context.Marcas
                        .AnyAsync(m =>
                            m.Id == productoActualizado.MarcaId.Value);

                    if (!existeMarca)
                        return BadRequest("La marca indicada no existe.");
                }

                UnidadMedida? unidadMedida = null;

                if (productoActualizado.UnidadMedidaId.HasValue)
                {
                    unidadMedida = await _context.UnidadesMedida
                        .AsNoTracking()
                        .FirstOrDefaultAsync(u =>
                            u.Id == productoActualizado.UnidadMedidaId.Value);

                    if (unidadMedida == null)
                        return BadRequest(
                            "La unidad de medida indicada no existe.");
                }

                if (TieneFracciones(producto.Stock) ||
                    TieneFracciones(productoActualizado.StockMinimo))
                {
                    if (unidadMedida == null)
                        return BadRequest(
                            "El producto necesita una unidad de medida que permita cantidades decimales.");

                    if (!unidadMedida.PermiteFracciones)
                        return BadRequest(
                            "La unidad de medida indicada no permite cantidades decimales.");
                }

                if (producto.UnidadMedidaId !=
                    productoActualizado.UnidadMedidaId)
                {
                    var tieneMovimientos = await _context.MovimientosStock
                        .AnyAsync(m => m.ProductoId == id);

                    if (tieneMovimientos)
                        return Conflict(
                            "No se puede cambiar la unidad de medida de un producto que tiene movimientos registrados.");
                }

                producto.Nombre = productoActualizado.Nombre.Trim();
                producto.Descripcion = productoActualizado.Descripcion?.Trim()
                    ?? string.Empty;
                producto.CodigoSku = codigoSku;
                producto.Precio = productoActualizado.Precio;
                producto.StockMinimo = productoActualizado.StockMinimo;
                producto.CategoriaId = productoActualizado.CategoriaId;
                producto.UnidadMedidaId = productoActualizado.UnidadMedidaId;
                producto.MarcaId = productoActualizado.MarcaId;

                await _context.SaveChangesAsync();

                return NoContent();
            });
        }

        [Permiso(Permisos.ProductosGestionar)]
        [HttpPatch("{id:int}")]
        public async Task<IActionResult> ActualizarParcialProducto(
    int id, [FromBody] ProductoPatchDto datosPatch)
        {
            return await EjecutarEscrituraProducto(async () =>
            {
                if (id <= 0)
                    return BadRequest("El ID debe ser mayor que cero.");

                var producto = await _context.Productos.FindAsync(id);

                if (producto == null)
                    return NotFound("Producto no encontrado.");

                if (!producto.Activo)
                    return Conflict(
                        "No se puede modificar un producto inactivo. Primero debes activarlo.");

                if (datosPatch.Nombre == null &&
                    datosPatch.Descripcion == null &&
                    datosPatch.CodigoSku == null &&
                    datosPatch.Precio == null &&
                    datosPatch.Stock == null &&
                    datosPatch.StockMinimo == null &&
                    datosPatch.CategoriaId == null &&
                    datosPatch.MarcaId == null)
                {
                    return BadRequest(
                        "Debes enviar al menos un campo para actualizar.");
                }

                if (datosPatch.Stock.HasValue)
                    return BadRequest(
                        "El stock solo puede modificarse mediante movimientos de inventario.");

                if (datosPatch.Nombre != null &&
                    string.IsNullOrWhiteSpace(datosPatch.Nombre))
                    return BadRequest("El nombre no puede estar vacío.");

                if (datosPatch.Precio.HasValue &&
                    PrecioFueraDeRango(datosPatch.Precio.Value))
                    return BadRequest(
                        "El precio debe estar dentro del rango permitido y tener como máximo dos decimales.");

                if (datosPatch.StockMinimo.HasValue)
                {
                    if (CantidadFueraDeRango(datosPatch.StockMinimo.Value))
                        return BadRequest(
                            "El stock mínimo debe estar dentro del rango permitido y tener como máximo tres decimales.");

                    if (TieneFracciones(datosPatch.StockMinimo.Value))
                    {
                        if (!producto.UnidadMedidaId.HasValue)
                            return BadRequest(
                                "Debes asignar una unidad de medida para utilizar un stock mínimo decimal.");

                        var permiteFracciones = await _context.UnidadesMedida
                            .AsNoTracking()
                            .Where(u => u.Id == producto.UnidadMedidaId.Value)
                            .Select(u => u.PermiteFracciones)
                            .FirstOrDefaultAsync();

                        if (!permiteFracciones)
                            return BadRequest(
                                "La unidad de medida de este producto no permite cantidades decimales.");
                    }
                }

                var codigoSku = producto.CodigoSku;

                if (datosPatch.CodigoSku != null)
                {
                    codigoSku = string.IsNullOrWhiteSpace(datosPatch.CodigoSku)
                        ? null
                        : datosPatch.CodigoSku.Trim();

                    if (codigoSku != null)
                    {
                        if (codigoSku.Length > 50)
                            return BadRequest(
                                "El código SKU no puede superar los 50 caracteres.");

                        var existeSku = await _context.Productos
                            .AnyAsync(p =>
                                p.CodigoSku == codigoSku &&
                                p.Id != id);

                        if (existeSku)
                            return Conflict(
                                "Ya existe otro producto con ese código SKU.");
                    }
                }

                if (datosPatch.CategoriaId.HasValue)
                {
                    var existeCategoria = await _context.Categorias
                        .AnyAsync(c =>
                            c.Id == datosPatch.CategoriaId.Value);

                    if (!existeCategoria)
                        return BadRequest("La categoría indicada no existe.");
                }

                if (datosPatch.MarcaId.HasValue)
                {
                    var existeMarca = await _context.Marcas
                        .AnyAsync(m =>
                            m.Id == datosPatch.MarcaId.Value);

                    if (!existeMarca)
                        return BadRequest("La marca indicada no existe.");
                }

                if (datosPatch.Nombre != null)
                    producto.Nombre = datosPatch.Nombre.Trim();

                if (datosPatch.Descripcion != null)
                    producto.Descripcion = datosPatch.Descripcion.Trim();

                if (datosPatch.CodigoSku != null)
                    producto.CodigoSku = codigoSku;

                if (datosPatch.Precio.HasValue)
                    producto.Precio = datosPatch.Precio.Value;

                if (datosPatch.StockMinimo.HasValue)
                    producto.StockMinimo = datosPatch.StockMinimo.Value;

                if (datosPatch.CategoriaId.HasValue)
                    producto.CategoriaId = datosPatch.CategoriaId.Value;

                if (datosPatch.MarcaId.HasValue)
                    producto.MarcaId = datosPatch.MarcaId.Value;

                await _context.SaveChangesAsync();

                return Ok(producto);
            });
        }

        [Permiso(Permisos.ProductosGestionar)]
        [HttpPatch("{id:int}/desactivar")]
        public async Task<IActionResult> DesactivarProducto(int id)
        {
            var producto = await _context.Productos.FindAsync(id);

            if (producto == null)
            {
                return NotFound("Producto no encontrado.");
            }

            if (!producto.Activo)
            {
                return Conflict("El producto ya está desactivado.");
            }

            producto.Activo = false;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                mensaje = "Producto desactivado correctamente.",
                productoId = producto.Id,
                nombre = producto.Nombre,
                activo = producto.Activo
            });
        }

        [Permiso(Permisos.ProductosGestionar)]
        [HttpPatch("{id:int}/activar")]
        public async Task<IActionResult> ActivarProducto(int id)
        {
            var producto = await _context.Productos.FindAsync(id);

            if (producto == null)
            {
                return NotFound("Producto no encontrado.");
            }

            if (producto.Activo)
            {
                return Conflict("El producto ya está activo.");
            }

            producto.Activo = true;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                mensaje = "Producto activado correctamente.",
                productoId = producto.Id,
                nombre = producto.Nombre,
                activo = producto.Activo
            });
        }

        [Permiso(Permisos.ProductosGestionar)]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> EliminarProducto(int id)
        {
            return await EjecutarEscrituraProducto(async () =>
            {
                if (id <= 0)
                    return BadRequest("El ID debe ser mayor que cero.");

                var producto = await _context.Productos.FindAsync(id);

                if (producto == null)
                    return NotFound("Producto no encontrado.");

                var tieneMovimientos = await _context.MovimientosStock
                    .AnyAsync(m => m.ProductoId == id);

                if (tieneMovimientos)
                    return Conflict(
                        "No se puede eliminar un producto que tiene movimientos de stock registrados.");

                var tieneCompras = await _context.DetallesCompra
                    .AnyAsync(d => d.ProductoId == id);

                if (tieneCompras)
                    return Conflict(
                        "No se puede eliminar un producto asociado a compras.");

                var tieneVentas = await _context.DetallesVenta
                    .AnyAsync(d => d.ProductoId == id);

                if (tieneVentas)
                    return Conflict(
                        "No se puede eliminar un producto asociado a ventas.");

                var tieneProveedores = await _context.ProductoProveedores
                    .AnyAsync(pp => pp.ProductoId == id);

                if (tieneProveedores)
                    return Conflict(
                        "No se puede eliminar un producto asociado a proveedores.");

                _context.Productos.Remove(producto);

                await _context.SaveChangesAsync();

                return NoContent();
            });
        }
    }
}
