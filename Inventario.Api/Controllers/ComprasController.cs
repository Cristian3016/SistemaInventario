
using Inventario.Api.Data;
using Inventario.Api.DTOs;
using Inventario.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

        [HttpPost]
        public async Task<IActionResult> CrearCompra(
            [FromBody] CrearCompraDto datos)
        {
            if (datos == null)
                return BadRequest("Debes proporcionar los datos de la compra.");

            if (datos.ProveedorId <= 0)
                return BadRequest("El ID del proveedor debe ser mayor que cero.");

            var proveedor = await _context.Proveedores
                .FirstOrDefaultAsync(p => p.Id == datos.ProveedorId);

            if (proveedor == null)
                return NotFound("Proveedor no encontrado.");

            if (!proveedor.Activo)
                return Conflict("No se pueden registrar compras a un proveedor inactivo.");

            var numeroComprobante = datos.NumeroComprobante?.Trim();

            if (numeroComprobante?.Length > 100)
                return BadRequest("El número de comprobante no puede superar los 100 caracteres.");

            var compra = new Compra
            {
                ProveedorId = datos.ProveedorId,
                FechaCompra = DateTime.UtcNow,
                NumeroComprobante = string.IsNullOrWhiteSpace(numeroComprobante)
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
        }

        [HttpPost("{compraId:int}/detalles")]
        public async Task<IActionResult> AgregarDetalleCompra(
            int compraId,
            [FromBody] AgregarDetalleCompraDto datos)
        {
            if (compraId <= 0)
                return BadRequest("El ID de la compra debe ser mayor que cero.");

            if (datos == null)
                return BadRequest("Debes proporcionar los datos del producto.");

            if (datos.ProductoId <= 0)
                return BadRequest("El ID del producto debe ser mayor que cero.");

            if (datos.Cantidad <= 0)
                return BadRequest("La cantidad debe ser mayor que cero.");

            if (datos.PrecioUnitario <= 0)
                return BadRequest("El precio unitario debe ser mayor que cero.");

            if (decimal.Round(datos.Cantidad, 3) != datos.Cantidad)
                return BadRequest("La cantidad no puede tener más de tres decimales.");

            if (decimal.Round(datos.PrecioUnitario, 2) != datos.PrecioUnitario)
                return BadRequest("El precio unitario no puede tener más de dos decimales.");

            var compra = await _context.Compras.FindAsync(compraId);

            if (compra == null)
                return NotFound("Compra no encontrada.");

            if (compra.Estado != "Borrador")
                return Conflict("Solo se pueden agregar productos a compras en estado Borrador.");

            var producto = await _context.Productos.FindAsync(datos.ProductoId);

            if (producto == null)
                return NotFound("Producto no encontrado.");

            if (!producto.Activo)
                return Conflict("No se puede agregar un producto inactivo.");

            var proveedor = await _context.Proveedores.FindAsync(compra.ProveedorId);

            if (proveedor == null || !proveedor.Activo)
                return Conflict("El proveedor de la compra no está disponible.");

            var estaVinculado = await _context.ProductoProveedores
                .AnyAsync(pp =>
                    pp.ProductoId == datos.ProductoId &&
                    pp.ProveedorId == compra.ProveedorId);

            if (!estaVinculado)
                return Conflict("El producto no está vinculado con el proveedor de esta compra.");

            var existeDetalle = await _context.DetallesCompra
                .AnyAsync(d =>
                    d.CompraId == compraId &&
                    d.ProductoId == datos.ProductoId);

            if (existeDetalle)
                return Conflict("El producto ya está agregado a esta compra.");

            decimal subtotal;

            try
            {
                subtotal = decimal.Round(
                    checked(datos.Cantidad * datos.PrecioUnitario),
                    2,
                    MidpointRounding.AwayFromZero);

                if (datos.Cantidad >= 1000000000000000m ||
                    datos.PrecioUnitario >= 10000000000000000m ||
                    subtotal >= 10000000000000000m)
                    return BadRequest("Los valores superan los límites permitidos.");

                var nuevoTotal = checked(compra.Total + subtotal);

                if (nuevoTotal >= 10000000000000000m)
                    return BadRequest("El total supera el límite permitido.");

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
            }
            catch (OverflowException)
            {
                return BadRequest("Los valores numéricos superan los límites permitidos.");
            }
        }

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

        [HttpPatch("{id:int}/confirmar")]
        public async Task<IActionResult> ConfirmarCompra(int id)
        {
            if (id <= 0)
                return BadRequest("El ID de la compra debe ser mayor que cero.");

            await using var transaccion =
                await _context.Database.BeginTransactionAsync();

            var filasActualizadas = await _context.Compras
                .Where(c => c.Id == id && c.Estado == "Borrador")
                .ExecuteUpdateAsync(cambios =>
                    cambios.SetProperty(c => c.Estado, "Confirmada"));

            if (filasActualizadas == 0)
            {
                var existeCompra = await _context.Compras
                    .AnyAsync(c => c.Id == id);

                if (!existeCompra)
                    return NotFound("Compra no encontrada.");

                return Conflict("Solo se pueden confirmar compras en estado Borrador.");
            }

            var proveedorActivo = await _context.Compras
                .AsNoTracking()
                .AnyAsync(c => c.Id == id && c.Proveedor.Activo);

            if (!proveedorActivo)
                return Conflict("No se puede confirmar una compra de un proveedor inactivo.");

            var detalles = await _context.DetallesCompra
                .AsNoTracking()
                .Where(d => d.CompraId == id)
                .Select(d => new
                {
                    d.ProductoId,
                    d.Cantidad
                })
                .ToListAsync();

            if (detalles.Count == 0)
                return Conflict("No se puede confirmar una compra sin productos.");

            const decimal stockMaximo = 999999999999999.999m;

            foreach (var detalle in detalles)
            {
                if (detalle.Cantidad <= 0 ||
                    detalle.Cantidad > stockMaximo ||
                    decimal.Round(detalle.Cantidad, 3) != detalle.Cantidad)
                {
                    return Conflict("La compra contiene una cantidad inválida.");
                }

                var producto = await _context.Productos
                    .AsNoTracking()
                    .Where(p => p.Id == detalle.ProductoId)
                    .Select(p => new
                    {
                        p.Activo,
                        p.UnidadMedidaId,
                        PermiteFracciones = p.UnidadMedida != null &&
                                           p.UnidadMedida.PermiteFracciones
                    })
                    .FirstOrDefaultAsync();

                if (producto == null)
                    return NotFound("Uno de los productos de la compra no existe.");

                if (!producto.Activo)
                    return Conflict("No se puede confirmar una compra con productos inactivos.");

                if (detalle.Cantidad != decimal.Truncate(detalle.Cantidad))
                {
                    if (!producto.UnidadMedidaId.HasValue)
                        return Conflict("Un producto no tiene unidad de medida para cantidades decimales.");

                    if (!producto.PermiteFracciones)
                        return Conflict("La unidad de medida de un producto no permite cantidades decimales.");
                }

                var stockActualizado = await _context.Productos
                    .Where(p => p.Id == detalle.ProductoId &&
                                p.Activo &&
                                p.Stock <= stockMaximo - detalle.Cantidad)
                    .ExecuteUpdateAsync(cambios =>
                        cambios.SetProperty(
                            p => p.Stock,
                            p => p.Stock + detalle.Cantidad));

                if (stockActualizado == 0)
                    return Conflict("No se pudo actualizar el stock de uno de los productos.");

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

            await _context.SaveChangesAsync();

            await transaccion.CommitAsync();

            return Ok(new
            {
                mensaje = "Compra confirmada correctamente.",
                compraId = id,
                estado = "Confirmada",
                movimientosRegistrados = detalles.Count
            });
        }

        [HttpDelete("{compraId:int}/detalles/{detalleId:int}")]
        public async Task<IActionResult> EliminarDetalleCompra(
            int compraId, int detalleId)
        {
            if (compraId <= 0 || detalleId <= 0)
                return BadRequest("Los ID deben ser mayores que cero.");

            await using var transaccion =
                await _context.Database.BeginTransactionAsync();

            var compra = await _context.Compras
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == compraId);

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
                return NotFound("El producto no existe en esta compra.");

            var filasActualizadas = await _context.Compras
                .Where(c =>
                    c.Id == compraId &&
                    c.Estado == "Borrador" &&
                    c.Total >= detalle.Subtotal)
                .ExecuteUpdateAsync(cambios =>
                    cambios.SetProperty(
                        c => c.Total,
                        c => c.Total - detalle.Subtotal));

            if (filasActualizadas == 0)
                return Conflict("No se pudo actualizar el total de la compra.");

            _context.DetallesCompra.Remove(detalle);

            await _context.SaveChangesAsync();

            var totalActual = await _context.Compras
                .Where(c => c.Id == compraId)
                .Select(c => c.Total)
                .SingleAsync();

            await transaccion.CommitAsync();

            return Ok(new
            {
                mensaje = "Producto eliminado correctamente de la compra.",
                compraId,
                detalleId,
                totalCompra = totalActual
            });
        }

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

        [HttpPut("{compraId:int}/detalles/{detalleId:int}")]
        public async Task<IActionResult> ActualizarDetalleCompra(
            int compraId,
            int detalleId,
            [FromBody] ActualizarDetalleCompraDto datos)
        {
            if (compraId <= 0 || detalleId <= 0)
                return BadRequest("Los ID deben ser mayores que cero.");

            if (datos == null)
                return BadRequest("Debes proporcionar los datos del detalle.");

            if (datos.Cantidad <= 0)
                return BadRequest("La cantidad debe ser mayor que cero.");

            if (datos.PrecioUnitario <= 0)
                return BadRequest("El precio unitario debe ser mayor que cero.");

            if (decimal.Round(datos.Cantidad, 3) != datos.Cantidad)
                return BadRequest("La cantidad no puede superar tres decimales.");

            if (decimal.Round(datos.PrecioUnitario, 2) != datos.PrecioUnitario)
                return BadRequest("El precio unitario no puede superar dos decimales.");

            const decimal limiteCantidad = 1000000000000000m;
            const decimal limiteDinero = 10000000000000000m;

            if (datos.Cantidad >= limiteCantidad ||
                datos.PrecioUnitario >= limiteDinero)
                return BadRequest("Los valores superan los límites permitidos.");

            decimal nuevoSubtotal;

            try
            {
                nuevoSubtotal = decimal.Round(
                    checked(datos.Cantidad * datos.PrecioUnitario),
                    2,
                    MidpointRounding.AwayFromZero);
            }
            catch (OverflowException)
            {
                return BadRequest("Los valores numéricos superan los límites permitidos.");
            }

            if (nuevoSubtotal >= limiteDinero)
                return BadRequest("El subtotal supera el límite permitido.");

            await using var transaccion =
                await _context.Database.BeginTransactionAsync();

            var compra = await _context.Compras
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == compraId);

            if (compra == null)
                return NotFound("Compra no encontrada.");

            if (compra.Estado != "Borrador")
                return Conflict("Solo se pueden modificar compras en estado Borrador.");

            var detalle = await _context.DetallesCompra
                .FirstOrDefaultAsync(d =>
                    d.Id == detalleId &&
                    d.CompraId == compraId);

            if (detalle == null)
                return NotFound("El detalle no existe en esta compra.");

            if (datos.Cantidad != decimal.Truncate(datos.Cantidad))
            {
                var permiteFracciones = await _context.Productos
                    .AsNoTracking()
                    .Where(p => p.Id == detalle.ProductoId)
                    .Select(p =>
                        p.UnidadMedida != null &&
                        p.UnidadMedida.PermiteFracciones)
                    .FirstOrDefaultAsync();

                if (!permiteFracciones)
                    return BadRequest("La unidad de medida del producto no permite cantidades decimales.");
            }

            var subtotalAnterior = detalle.Subtotal;

            var diferencia = nuevoSubtotal - subtotalAnterior;

            var filasActualizadas = await _context.Compras
                .Where(c =>
                    c.Id == compraId &&
                    c.Estado == "Borrador" &&
                    c.Total >= subtotalAnterior &&
                    (diferencia <= 0 || c.Total < limiteDinero - diferencia))
                .ExecuteUpdateAsync(cambios =>
                    cambios.SetProperty(
                        c => c.Total,
                        c => c.Total - subtotalAnterior + nuevoSubtotal));

            if (filasActualizadas == 0)
                return Conflict("No se pudo actualizar el total de la compra.");

            detalle.Cantidad = datos.Cantidad;
            detalle.PrecioUnitario = datos.PrecioUnitario;
            detalle.Subtotal = nuevoSubtotal;

            await _context.SaveChangesAsync();

            var totalActual = await _context.Compras
                .AsNoTracking()
                .Where(c => c.Id == compraId)
                .Select(c => c.Total)
                .SingleAsync();

            await transaccion.CommitAsync();

            return Ok(new
            {
                mensaje = "Detalle de compra actualizado correctamente.",
                detalle.Id,
                detalle.CompraId,
                detalle.ProductoId,
                detalle.Cantidad,
                detalle.PrecioUnitario,
                detalle.Subtotal,
                totalCompra = totalActual
            });
        }
    }
}



