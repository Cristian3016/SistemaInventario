using Inventario.Api.DTOs;
using Inventario.Api.Data;
using Inventario.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

        [HttpPost]
        public async Task<ActionResult<Producto>> CrearProducto(
            [FromBody] Producto nuevoProducto)
        {
            if (string.IsNullOrWhiteSpace(nuevoProducto.Nombre))
            {
                return BadRequest("El nombre es obligatorio.");
            }

            if (nuevoProducto.Precio < 0)
            {
                return BadRequest("El precio no puede ser negativo.");
            }

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
                {
                    return BadRequest("La categoría indicada no existe.");
                }
            }

            if (nuevoProducto.MarcaId.HasValue)
            {
                var existeMarca = await _context.Marcas
                    .AnyAsync(m => m.Id == nuevoProducto.MarcaId.Value);

                if (!existeMarca)
                {
                    return BadRequest("La marca indicada no existe.");
                }
            }

            UnidadMedida? unidadMedida = null;

            if (nuevoProducto.UnidadMedidaId.HasValue)
            {
                unidadMedida = await _context.UnidadesMedida
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        u => u.Id == nuevoProducto.UnidadMedidaId.Value);

                if (unidadMedida == null)
                {
                    return BadRequest("La unidad de medida indicada no existe.");
                }
            }

            if (TieneFracciones(nuevoProducto.Stock) ||
                TieneFracciones(nuevoProducto.StockMinimo))
            {
                if (unidadMedida == null)
                {
                    return BadRequest(
                        "Debes asignar una unidad de medida para utilizar cantidades decimales.");
                }

                if (!unidadMedida.PermiteFracciones)
                {
                    return BadRequest(
                        "La unidad de medida indicada no permite cantidades decimales.");
                }
            }

            var codigoSku = string.IsNullOrWhiteSpace(nuevoProducto.CodigoSku)
                ? null
                : nuevoProducto.CodigoSku.Trim();

            if (codigoSku != null)
            {
                if (codigoSku.Length > 50)
                {
                    return BadRequest(
                        "El código SKU no puede superar los 50 caracteres.");
                }

                var existeSku = await _context.Productos
                    .AnyAsync(p => p.CodigoSku == codigoSku);

                if (existeSku)
                {
                    return Conflict(
                        "Ya existe un producto con ese código SKU.");
                }
            }

            var producto = new Producto
            {
                Nombre = nuevoProducto.Nombre.Trim(),
                Descripcion = nuevoProducto.Descripcion?.Trim() ?? string.Empty,
                CodigoSku = codigoSku,
                Precio = nuevoProducto.Precio,
                Stock = nuevoProducto.Stock,
                StockMinimo = nuevoProducto.StockMinimo,
                CategoriaId = nuevoProducto.CategoriaId,
                UnidadMedidaId = nuevoProducto.UnidadMedidaId,
                MarcaId = nuevoProducto.MarcaId,
                FechaCreacion = DateTime.UtcNow
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
                    FechaMovimiento = DateTime.UtcNow
                };

                _context.MovimientosStock.Add(movimientoInicial);
            }

            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(ObtenerProductoPorId),
                new { id = producto.Id },
                producto);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> ActualizarProducto(
            int id, [FromBody] Producto productoActualizado)
        {
            var producto = await _context.Productos.FindAsync(id);

            if (producto == null)
            {
                return NotFound("Producto no encontrado.");
            }

            if (!producto.Activo)
            {
                return Conflict(
                    "No se puede modificar un producto inactivo. Primero debes activarlo.");
            }

            if (productoActualizado.Stock != producto.Stock)
            {
                return BadRequest(
                    "El stock solo puede modificarse mediante movimientos de inventario.");
            }

            if (string.IsNullOrWhiteSpace(productoActualizado.Nombre))
            {
                return BadRequest("El nombre es obligatorio.");
            }

            if (productoActualizado.Precio < 0)
            {
                return BadRequest("El precio no puede ser negativo.");
            }

            if (CantidadFueraDeRango(productoActualizado.StockMinimo))
            {
                return BadRequest(
                    "El stock mínimo debe ser positivo o cero, no superar el rango permitido y tener como máximo tres decimales.");
            }

            var codigoSku = string.IsNullOrWhiteSpace(productoActualizado.CodigoSku)
                ? null
                : productoActualizado.CodigoSku.Trim();

            if (codigoSku != null)
            {
                if (codigoSku.Length > 50)
                {
                    return BadRequest(
                        "El código SKU no puede superar los 50 caracteres.");
                }

                var existeSku = await _context.Productos
                    .AnyAsync(p => p.CodigoSku == codigoSku && p.Id != id);

                if (existeSku)
                {
                    return Conflict(
                        "Ya existe otro producto con ese código SKU.");
                }
            }

            if (productoActualizado.CategoriaId.HasValue)
            {
                var existeCategoria = await _context.Categorias
                    .AnyAsync(c => c.Id == productoActualizado.CategoriaId.Value);

                if (!existeCategoria)
                {
                    return BadRequest("La categoría indicada no existe.");
                }
            }

            if (productoActualizado.MarcaId.HasValue)
            {
                var existeMarca = await _context.Marcas
                    .AnyAsync(m => m.Id == productoActualizado.MarcaId.Value);

                if (!existeMarca)
                {
                    return BadRequest("La marca indicada no existe.");
                }
            }

            UnidadMedida? unidadMedida = null;

            if (productoActualizado.UnidadMedidaId.HasValue)
            {
                unidadMedida = await _context.UnidadesMedida
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        u => u.Id == productoActualizado.UnidadMedidaId.Value);

                if (unidadMedida == null)
                {
                    return BadRequest("La unidad de medida indicada no existe.");
                }
            }

            if (TieneFracciones(producto.Stock) ||
                TieneFracciones(productoActualizado.StockMinimo))
            {
                if (unidadMedida == null)
                {
                    return BadRequest(
                        "El producto necesita una unidad de medida que permita cantidades decimales.");
                }

                if (!unidadMedida.PermiteFracciones)
                {
                    return BadRequest(
                        "La unidad de medida indicada no permite cantidades decimales.");
                }
            }

            if (producto.UnidadMedidaId != productoActualizado.UnidadMedidaId)
            {
                var tieneMovimientos = await _context.MovimientosStock
                    .AnyAsync(m => m.ProductoId == id);

                if (tieneMovimientos)
                {
                    return Conflict(
                        "No se puede cambiar la unidad de medida de un producto que tiene movimientos registrados.");
                }
            }

            producto.Nombre = productoActualizado.Nombre.Trim();
            producto.Descripcion =
                productoActualizado.Descripcion?.Trim() ?? string.Empty;
            producto.CodigoSku = codigoSku;
            producto.Precio = productoActualizado.Precio;
            producto.StockMinimo = productoActualizado.StockMinimo;
            producto.CategoriaId = productoActualizado.CategoriaId;
            producto.UnidadMedidaId = productoActualizado.UnidadMedidaId;

            if (productoActualizado.MarcaId.HasValue)
            {
                producto.MarcaId = productoActualizado.MarcaId.Value;
            }

            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpPatch("{id:int}")]
        public async Task<IActionResult> ActualizarParcialProducto(
            int id, [FromBody] ProductoPatchDto datosPatch)
        {
            var producto = await _context.Productos.FindAsync(id);

            if (producto == null)
            {
                return NotFound("Producto no encontrado.");
            }

            if (!producto.Activo)
            {
                return Conflict(
                    "No se puede modificar un producto inactivo. Primero debes activarlo.");
            }

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
            {
                return BadRequest(
                    "El stock solo puede modificarse mediante movimientos de inventario.");
            }

            if (datosPatch.Nombre != null &&
                string.IsNullOrWhiteSpace(datosPatch.Nombre))
            {
                return BadRequest("El nombre no puede estar vacío.");
            }

            if (datosPatch.Precio.HasValue &&
                datosPatch.Precio.Value < 0)
            {
                return BadRequest("El precio no puede ser negativo.");
            }

            if (datosPatch.StockMinimo.HasValue)
            {
                if (CantidadFueraDeRango(datosPatch.StockMinimo.Value))
                {
                    return BadRequest(
                        "El stock mínimo debe ser positivo o cero, no superar el rango permitido y tener como máximo tres decimales.");
                }

                if (TieneFracciones(datosPatch.StockMinimo.Value))
                {
                    if (!producto.UnidadMedidaId.HasValue)
                    {
                        return BadRequest(
                            "Debes asignar una unidad de medida para utilizar un stock mínimo decimal.");
                    }

                    var permiteFracciones = await _context.UnidadesMedida
                        .AsNoTracking()
                        .Where(u => u.Id == producto.UnidadMedidaId.Value)
                        .Select(u => u.PermiteFracciones)
                        .FirstOrDefaultAsync();

                    if (!permiteFracciones)
                    {
                        return BadRequest(
                            "La unidad de medida de este producto no permite cantidades decimales.");
                    }
                }
            }

            if (datosPatch.CodigoSku != null)
            {
                var codigoSku = string.IsNullOrWhiteSpace(datosPatch.CodigoSku)
                    ? null
                    : datosPatch.CodigoSku.Trim();

                if (codigoSku != null)
                {
                    if (codigoSku.Length > 50)
                    {
                        return BadRequest(
                            "El código SKU no puede superar los 50 caracteres.");
                    }

                    var existeSku = await _context.Productos
                        .AnyAsync(
                            p => p.CodigoSku == codigoSku && p.Id != id);

                    if (existeSku)
                    {
                        return Conflict(
                            "Ya existe otro producto con ese código SKU.");
                    }
                }

                producto.CodigoSku = codigoSku;
            }

            if (datosPatch.CategoriaId.HasValue)
            {
                var existeCategoria = await _context.Categorias
                    .AnyAsync(
                        c => c.Id == datosPatch.CategoriaId.Value);

                if (!existeCategoria)
                {
                    return BadRequest("La categoría indicada no existe.");
                }
            }

            if (datosPatch.MarcaId.HasValue)
            {
                var existeMarca = await _context.Marcas
                    .AnyAsync(m => m.Id == datosPatch.MarcaId.Value);

                if (!existeMarca)
                {
                    return BadRequest("La marca indicada no existe.");
                }
            }

            if (datosPatch.Nombre != null)
            {
                producto.Nombre = datosPatch.Nombre.Trim();
            }

            if (datosPatch.Descripcion != null)
            {
                producto.Descripcion = datosPatch.Descripcion.Trim();
            }

            if (datosPatch.Precio.HasValue)
            {
                producto.Precio = datosPatch.Precio.Value;
            }

            if (datosPatch.StockMinimo.HasValue)
            {
                producto.StockMinimo = datosPatch.StockMinimo.Value;
            }

            if (datosPatch.CategoriaId.HasValue)
            {
                producto.CategoriaId = datosPatch.CategoriaId.Value;
            }

            if (datosPatch.MarcaId.HasValue)
            {
                producto.MarcaId = datosPatch.MarcaId.Value;
            }

            await _context.SaveChangesAsync();

            return Ok(producto);
        }

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

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> EliminarProducto(int id)
        {
            var producto = await _context.Productos.FindAsync(id);

            if (producto == null)
            {
                return NotFound("Producto no encontrado.");
            }

            var tieneMovimientos = await _context.MovimientosStock
                .AnyAsync(m => m.ProductoId == id);

            if (tieneMovimientos)
            {
                return Conflict(
                    "No se puede eliminar un producto que tiene movimientos de stock registrados.");
            }

            _context.Productos.Remove(producto);

            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
