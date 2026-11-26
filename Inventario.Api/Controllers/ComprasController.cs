using Microsoft.Data.SqlClient;
using Inventario.Api.Data;
using Inventario.Api.DTOs;
using Inventario.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Inventario.Api.Authorization;

namespace Inventario.Api.Controllers
{
    [ApiController]
    [Route("api/compras")]
    public class ComprasController : ControllerBase
    {
        private readonly InventarioDbContext _context;

        public ComprasController(InventarioDbContext context)
        {
            _context = context;
        }
        private const decimal LimiteCantidad = 1000000000000000m;
        private const decimal LimiteDinero = 10000000000000000m;
        private const decimal StockMaximo = 999999999999999.999m;

        private async Task<IActionResult> EjecutarEscrituraCompra(
            Func<Task<IActionResult>> operacion)
        {
            try
            {
                await using var transaccion = await _context.Database
                    .BeginTransactionAsync(
                        System.Data.IsolationLevel.Serializable);

                var resultado = await operacion();

                if (resultado is ObjectResult
                    { StatusCode: >= 200 and < 300 } ||
                    resultado is StatusCodeResult
                    { StatusCode: >= 200 and < 300 })
                {
                    await transaccion.CommitAsync();
                }

                return resultado;
            }
            catch (OverflowException)
            {
                return BadRequest(
                    "Los valores numéricos superan los límites permitidos.");
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(
                    "La compra fue modificada por otra operación.");
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is SqlException sqlEx &&
                (sqlEx.Number == 2601 || sqlEx.Number == 2627))
            {
                return Conflict(
                    "Ya existe un registro con esos datos.");
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

        private IActionResult? ValidarImportesDetalle(
            decimal cantidad,
            decimal precioUnitario,
            out decimal subtotal)
        {
            subtotal = 0m;

            if (cantidad <= 0)
                return BadRequest(
                    "La cantidad debe ser mayor que cero.");

            if (precioUnitario <= 0)
                return BadRequest(
                    "El precio unitario debe ser mayor que cero.");

            if (decimal.Round(cantidad, 3) != cantidad)
                return BadRequest(
                    "La cantidad no puede superar tres decimales.");

            if (decimal.Round(precioUnitario, 2) != precioUnitario)
                return BadRequest(
                    "El precio unitario no puede superar dos decimales.");

            if (cantidad >= LimiteCantidad ||
                precioUnitario >= LimiteDinero)
            {
                return BadRequest(
                    "Los valores superan los límites permitidos.");
            }

            subtotal = decimal.Round(
                checked(cantidad * precioUnitario),
                2,
                MidpointRounding.AwayFromZero);

            if (subtotal >= LimiteDinero)
                return BadRequest(
                    "El subtotal supera el límite permitido.");

            return null;
        }

        private async Task<(Producto? Producto, IActionResult? Error)>
            ValidarProductoCompra(
                int proveedorId,
                int productoId,
                decimal cantidad)
        {
            var producto = await _context.Productos
                .AsNoTracking()
                .Include(p => p.UnidadMedida)
                .FirstOrDefaultAsync(p => p.Id == productoId);

            if (producto == null)
                return (null, NotFound(
                    "Producto no encontrado."));

            if (!producto.Activo)
                return (null, Conflict(
                    "El producto está inactivo."));

            if (cantidad != decimal.Truncate(cantidad) &&
                (producto.UnidadMedida == null ||
                 !producto.UnidadMedida.PermiteFracciones))
            {
                return (null, BadRequest(
                    "La unidad de medida del producto no permite cantidades decimales."));
            }

            var proveedorActivo = await _context.Proveedores
                .AnyAsync(p =>
                    p.Id == proveedorId &&
                    p.Activo);

            if (!proveedorActivo)
                return (null, Conflict(
                    "El proveedor de la compra no está disponible."));

            var estaVinculado = await _context.ProductoProveedores
                .AnyAsync(pp =>
                    pp.ProductoId == productoId &&
                    pp.ProveedorId == proveedorId);

            if (!estaVinculado)
                return (null, Conflict(
                    "El producto no está vinculado con el proveedor de esta compra."));

            return (producto, null);
        }

        [Permiso(Permisos.ComprasGestionar)]
        [HttpPost]
        public async Task<IActionResult> CrearCompra(
    [FromBody] CrearCompraDto datos)
        {
            return await EjecutarEscrituraCompra(async () =>
            {
                if (datos == null)
                    return BadRequest(
                        "Debes proporcionar los datos de la compra.");

                if (datos.ProveedorId <= 0)
                    return BadRequest(
                        "El ID del proveedor debe ser mayor que cero.");

                var proveedor = await _context.Proveedores
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p =>
                        p.Id == datos.ProveedorId);

                if (proveedor == null)
                    return NotFound("Proveedor no encontrado.");

                if (!proveedor.Activo)
                    return Conflict(
                        "No se pueden registrar compras a un proveedor inactivo.");

                var numeroComprobante = datos.NumeroComprobante?.Trim();

                if (numeroComprobante?.Length > 100)
                    return BadRequest(
                        "El número de comprobante no puede superar los 100 caracteres.");

                var compra = new Compra
                {
                    ProveedorId = datos.ProveedorId,
                    FechaCompra = DateTime.UtcNow,
                    NumeroComprobante =
                        string.IsNullOrWhiteSpace(numeroComprobante)
                            ? null
                            : numeroComprobante,
                    Estado = "Borrador",
                    Total = 0m
                };

                _context.Compras.Add(compra);

                await _context.SaveChangesAsync();

                return StatusCode(201, new
                {
                    mensaje = "Compra registrada correctamente.",
                    compra.Id,
                    compra.ProveedorId,
                    proveedor.RazonSocial,
                    compra.FechaCompra,
                    compra.NumeroComprobante,
                    compra.Estado,
                    compra.Total
                });
            });
        }

        [Permiso(Permisos.ComprasGestionar)]
        [HttpPost("{compraId:int}/detalles")]
        public async Task<IActionResult> AgregarDetalleCompra(
    int compraId,
    [FromBody] AgregarDetalleCompraDto datos)
        {
            return await EjecutarEscrituraCompra(async () =>
            {
                if (compraId <= 0)
                    return BadRequest(
                        "El ID de la compra debe ser mayor que cero.");

                if (datos == null)
                    return BadRequest(
                        "Debes proporcionar los datos del producto.");

                if (datos.ProductoId <= 0)
                    return BadRequest(
                        "El ID del producto debe ser mayor que cero.");

                var errorImportes = ValidarImportesDetalle(
                    datos.Cantidad,
                    datos.PrecioUnitario,
                    out var subtotal);

                if (errorImportes != null)
                    return errorImportes;

                var compra = await _context.Compras
                    .FindAsync(compraId);

                if (compra == null)
                    return NotFound("Compra no encontrada.");

                if (compra.Estado != "Borrador")
                    return Conflict(
                        "Solo se pueden agregar productos a compras en estado Borrador.");

                var (producto, errorProducto) =
                    await ValidarProductoCompra(
                        compra.ProveedorId,
                        datos.ProductoId,
                        datos.Cantidad);

                if (errorProducto != null)
                    return errorProducto;

                if (producto == null)
                    return NotFound("Producto no encontrado.");

                var existeDetalle = await _context.DetallesCompra
                    .AnyAsync(d =>
                        d.CompraId == compraId &&
                        d.ProductoId == datos.ProductoId);

                if (existeDetalle)
                    return Conflict(
                        "El producto ya está agregado a esta compra.");

                var nuevoTotal = checked(compra.Total + subtotal);

                if (nuevoTotal >= LimiteDinero)
                    return BadRequest(
                        "El total supera el límite permitido.");

                var detalle = new DetalleCompra
                {
                    CompraId = compraId,
                    ProductoId = datos.ProductoId,
                    Cantidad = datos.Cantidad,
                    PrecioUnitario = datos.PrecioUnitario,
                    Subtotal = subtotal
                };

                _context.DetallesCompra.Add(detalle);

                compra.Total = nuevoTotal;

                await _context.SaveChangesAsync();

                return StatusCode(201, new
                {
                    mensaje = "Producto agregado correctamente a la compra.",
                    detalle.Id,
                    detalle.CompraId,
                    detalle.ProductoId,
                    producto.Nombre,
                    detalle.Cantidad,
                    detalle.PrecioUnitario,
                    detalle.Subtotal,
                    totalCompra = compra.Total
                });
            });
        }

        [Permiso(Permisos.ComprasGestionar)]
        [HttpGet("{id:int}")]
        public async Task<IActionResult> ObtenerCompraPorId(int id)
        {
            if (id <= 0)
                return BadRequest("El ID de la compra debe ser mayor que cero.");

            var compra = await _context.Compras
                .AsNoTracking()
                .Where(c => c.Id == id)
                .Select(c => new
                {
                    c.Id,
                    c.ProveedorId,
                    Proveedor = c.Proveedor.RazonSocial,
                    c.FechaCompra,
                    c.NumeroComprobante,
                    c.Estado,
                    c.Total
                })
                .FirstOrDefaultAsync();

            if (compra == null)
                return NotFound("Compra no encontrada.");

            var detalles = await _context.DetallesCompra
                .AsNoTracking()
                .Where(d => d.CompraId == id)
                .OrderBy(d => d.Producto.Nombre)
                .Select(d => new
                {
                    d.Id,
                    d.ProductoId,
                    Producto = d.Producto.Nombre,
                    d.Cantidad,
                    d.PrecioUnitario,
                    d.Subtotal
                })
                .ToListAsync();

            return Ok(new
            {
                compra.Id,
                compra.ProveedorId,
                compra.Proveedor,
                compra.FechaCompra,
                compra.NumeroComprobante,
                compra.Estado,
                compra.Total,
                Detalles = detalles
            });
        }

        [Permiso(Permisos.ComprasGestionar)]
        [HttpGet]
        public async Task<IActionResult> ObtenerCompras(
            [FromQuery] int? proveedorId,
            [FromQuery] string? estado)
        {
            if (proveedorId.HasValue && proveedorId.Value <= 0)
                return BadRequest("El ID del proveedor debe ser mayor que cero.");

            string? estadoNormalizado = null;

            if (!string.IsNullOrWhiteSpace(estado))
            {
                estadoNormalizado = estado.Trim().ToLowerInvariant() switch
                {
                    "borrador" => "Borrador",
                    "confirmada" => "Confirmada",
                    "anulada" => "Anulada",
                    _ => null
                };

                if (estadoNormalizado == null)
                    return BadRequest("El estado debe ser Borrador, Confirmada o Anulada.");
            }

            var consulta = _context.Compras.AsNoTracking();

            if (proveedorId.HasValue)
                consulta = consulta.Where(c => c.ProveedorId == proveedorId.Value);

            if (estadoNormalizado != null)
                consulta = consulta.Where(c => c.Estado == estadoNormalizado);

            var compras = await consulta
                .OrderByDescending(c => c.FechaCompra)
                .ThenByDescending(c => c.Id)
                .Select(c => new
                {
                    c.Id,
                    c.ProveedorId,
                    Proveedor = c.Proveedor.RazonSocial,
                    c.FechaCompra,
                    c.NumeroComprobante,
                    c.Estado,
                    c.Total,
                    CantidadProductos = _context.DetallesCompra
                        .Count(d => d.CompraId == c.Id)
                })
                .ToListAsync();

            return Ok(compras);
        }

        [Permiso(Permisos.ComprasGestionar)]
        [HttpPatch("{id:int}/confirmar")]
        public async Task<IActionResult> ConfirmarCompra(int id)
        {
            return await EjecutarEscrituraCompra(async () =>
            {
                if (id <= 0)
                    return BadRequest(
                        "El ID de la compra debe ser mayor que cero.");

                var compra = await _context.Compras.FindAsync(id);

                if (compra == null)
                    return NotFound("Compra no encontrada.");

                if (compra.Estado != "Borrador")
                    return Conflict(
                        "Solo se pueden confirmar compras en estado Borrador.");

                var proveedorActivo = await _context.Proveedores
                    .AnyAsync(p =>
                        p.Id == compra.ProveedorId &&
                        p.Activo);

                if (!proveedorActivo)
                    return Conflict(
                        "No se puede confirmar una compra de un proveedor inactivo.");

                var detalles = await _context.DetallesCompra
                    .AsNoTracking()
                    .Where(d => d.CompraId == id)
                    .OrderBy(d => d.ProductoId)
                    .ToListAsync();

                if (detalles.Count == 0)
                    return Conflict(
                        "No se puede confirmar una compra sin productos.");

                var totalDetalles = detalles.Sum(d => d.Subtotal);

                if (totalDetalles != compra.Total)
                    return Conflict(
                        "El total de la compra no coincide con sus detalles.");

                foreach (var detalle in detalles)
                {
                    var errorImportes = ValidarImportesDetalle(
                        detalle.Cantidad,
                        detalle.PrecioUnitario,
                        out var subtotalCalculado);

                    if (errorImportes != null)
                        return errorImportes;

                    if (subtotalCalculado != detalle.Subtotal)
                        return Conflict(
                            "La compra contiene un subtotal incorrecto.");

                    var (_, errorProducto) =
                        await ValidarProductoCompra(
                            compra.ProveedorId,
                            detalle.ProductoId,
                            detalle.Cantidad);

                    if (errorProducto != null)
                        return errorProducto;

                    var stockActualizado = await _context.Productos
                        .Where(p =>
                            p.Id == detalle.ProductoId &&
                            p.Activo &&
                            p.Stock <= StockMaximo - detalle.Cantidad)
                        .ExecuteUpdateAsync(cambios =>
                            cambios.SetProperty(
                                p => p.Stock,
                                p => p.Stock + detalle.Cantidad));

                    if (stockActualizado == 0)
                        return Conflict(
                            "No se pudo actualizar el stock de uno de los productos.");

                    var movimiento = new MovimientoStock
                    {
                        ProductoId = detalle.ProductoId,
                        Tipo = TipoMovimiento.Entrada,
                        Cantidad = detalle.Cantidad,
                        Observacion = $"Entrada por compra #{id}",
                        FechaMovimiento = DateTime.UtcNow
                    };

                    _context.MovimientosStock.Add(movimiento);
                }

                compra.Estado = "Confirmada";

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    mensaje = "Compra confirmada correctamente.",
                    compraId = id,
                    estado = "Confirmada",
                    movimientosRegistrados = detalles.Count
                });
            });
        }

        [Permiso(Permisos.ComprasGestionar)]
        [HttpDelete("{compraId:int}/detalles/{detalleId:int}")]
        public async Task<IActionResult> EliminarDetalleCompra(
    int compraId, int detalleId)
        {
            return await EjecutarEscrituraCompra(async () =>
            {
                if (compraId <= 0 || detalleId <= 0)
                    return BadRequest(
                        "Los ID deben ser mayores que cero.");

                var compra = await _context.Compras
                    .FindAsync(compraId);

                if (compra == null)
                    return NotFound("Compra no encontrada.");

                if (compra.Estado != "Borrador")
                    return Conflict(
                        "Solo se pueden eliminar productos de compras en estado Borrador.");

                var detalle = await _context.DetallesCompra
                    .FirstOrDefaultAsync(d =>
                        d.Id == detalleId &&
                        d.CompraId == compraId);

                if (detalle == null)
                    return NotFound(
                        "El producto no existe en esta compra.");

                if (detalle.Subtotal < 0 ||
                    compra.Total < detalle.Subtotal)
                {
                    return Conflict(
                        "El total de la compra es inconsistente.");
                }

                compra.Total -= detalle.Subtotal;

                _context.DetallesCompra.Remove(detalle);

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    mensaje = "Producto eliminado correctamente de la compra.",
                    compraId,
                    detalleId,
                    totalCompra = compra.Total
                });
            });
        }

        [Permiso(Permisos.ComprasGestionar)]
        [HttpPatch("{id:int}/anular")]
        public async Task<IActionResult> AnularCompra(int id)
        {
            if (id <= 0)
                return BadRequest("El ID de la compra debe ser mayor que cero.");

            var filasActualizadas = await _context.Compras
                .Where(c => c.Id == id && c.Estado == "Borrador")
                .ExecuteUpdateAsync(cambios =>
                    cambios.SetProperty(c => c.Estado, "Anulada"));

            if (filasActualizadas == 0)
            {
                var compra = await _context.Compras
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (compra == null)
                    return NotFound("Compra no encontrada.");

                if (compra.Estado == "Anulada")
                    return Conflict("La compra ya está anulada.");

                if (compra.Estado == "Confirmada")
                    return Conflict("No se puede anular una compra confirmada.");

                return Conflict("Solo se pueden anular compras en estado Borrador.");
            }

            return Ok(new
            {
                mensaje = "Compra anulada correctamente.",
                compraId = id,
                estado = "Anulada"
            });
        }

        [Permiso(Permisos.ComprasGestionar)]
        [HttpPut("{compraId:int}/detalles/{detalleId:int}")]
        public async Task<IActionResult> ActualizarDetalleCompra(
    int compraId,
    int detalleId,
    [FromBody] ActualizarDetalleCompraDto datos)
        {
            return await EjecutarEscrituraCompra(async () =>
            {
                if (compraId <= 0 || detalleId <= 0)
                    return BadRequest(
                        "Los ID deben ser mayores que cero.");

                if (datos == null)
                    return BadRequest(
                        "Debes proporcionar los datos del detalle.");

                var errorImportes = ValidarImportesDetalle(
                    datos.Cantidad,
                    datos.PrecioUnitario,
                    out var nuevoSubtotal);

                if (errorImportes != null)
                    return errorImportes;

                var compra = await _context.Compras
                    .FindAsync(compraId);

                if (compra == null)
                    return NotFound("Compra no encontrada.");

                if (compra.Estado != "Borrador")
                    return Conflict(
                        "Solo se pueden modificar compras en estado Borrador.");

                var detalle = await _context.DetallesCompra
                    .FirstOrDefaultAsync(d =>
                        d.Id == detalleId &&
                        d.CompraId == compraId);

                if (detalle == null)
                    return NotFound(
                        "El detalle no existe en esta compra.");

                var (_, errorProducto) =
                    await ValidarProductoCompra(
                        compra.ProveedorId,
                        detalle.ProductoId,
                        datos.Cantidad);

                if (errorProducto != null)
                    return errorProducto;

                if (detalle.Subtotal < 0 ||
                    compra.Total < detalle.Subtotal)
                {
                    return Conflict(
                        "El total de la compra es inconsistente.");
                }

                var nuevoTotal = checked(
                    compra.Total - detalle.Subtotal + nuevoSubtotal);

                if (nuevoTotal >= LimiteDinero)
                    return BadRequest(
                        "El total supera el límite permitido.");

                detalle.Cantidad = datos.Cantidad;
                detalle.PrecioUnitario = datos.PrecioUnitario;
                detalle.Subtotal = nuevoSubtotal;

                compra.Total = nuevoTotal;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    mensaje = "Detalle de compra actualizado correctamente.",
                    detalle.Id,
                    detalle.CompraId,
                    detalle.ProductoId,
                    detalle.Cantidad,
                    detalle.PrecioUnitario,
                    detalle.Subtotal,
                    totalCompra = compra.Total
                });
            });
        }
    }
}