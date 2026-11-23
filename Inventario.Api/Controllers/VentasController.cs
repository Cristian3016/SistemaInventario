
using Inventario.Api.Data;
using Inventario.Api.DTOs;
using Inventario.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Api.Controllers
{
    [ApiController]
    [Route("api/ventas")]
    public class VentasController : ControllerBase
    {
        private readonly InventarioDbContext _context;

        public VentasController(InventarioDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> CrearVenta(
            [FromBody] CrearVentaDto datos)
        {
            if (datos == null)
                return BadRequest("Debes proporcionar los datos de la venta.");

            if (datos.ClienteId.HasValue && datos.ClienteId.Value <= 0)
                return BadRequest("El ID del cliente debe ser mayor que cero.");

            string? nombreCliente = null;

            if (datos.ClienteId.HasValue)
            {
                var cliente = await _context.Clientes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == datos.ClienteId.Value);

                if (cliente == null)
                    return NotFound("Cliente no encontrado.");

                if (!cliente.Activo)
                    return Conflict("No se pueden registrar ventas a un cliente inactivo.");

                nombreCliente = cliente.NombreRazonSocial;
            }

            var numeroComprobante = datos.NumeroComprobante?.Trim();

            if (numeroComprobante?.Length > 100)
                return BadRequest("El número de comprobante no puede superar los 100 caracteres.");

            var venta = new Venta
            {
                ClienteId = datos.ClienteId,
                FechaVenta = DateTime.UtcNow,
                NumeroComprobante = string.IsNullOrWhiteSpace(numeroComprobante)
                    ? null
                    : numeroComprobante,
                Estado = "Borrador",
                Total = 0m
            };

            _context.Ventas.Add(venta);

            await _context.SaveChangesAsync();

            return StatusCode(201, new
            {
                mensaje = "Venta registrada correctamente.",
                venta.Id,
                venta.ClienteId,
                Cliente = nombreCliente ?? "Consumidor final",
                venta.FechaVenta,
                venta.NumeroComprobante,
                venta.Estado,
                venta.Total
            });
        }

        [HttpPost("{ventaId:int}/detalles")]
        public async Task<IActionResult> AgregarDetalleVenta(
            int ventaId,
            [FromBody] AgregarDetalleVentaDto datos)
        {
            if (ventaId <= 0)
                return BadRequest("El ID de la venta debe ser mayor que cero.");

            if (datos == null)
                return BadRequest("Debes proporcionar los datos del producto.");

            if (datos.ProductoId <= 0)
                return BadRequest("El ID del producto debe ser mayor que cero.");

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

            var venta = await _context.Ventas.FindAsync(ventaId);

            if (venta == null)
                return NotFound("Venta no encontrada.");

            if (venta.Estado != "Borrador")
                return Conflict("Solo se pueden agregar productos a ventas en estado Borrador.");

            var producto = await _context.Productos
                .AsNoTracking()
                .Where(p => p.Id == datos.ProductoId)
                .Select(p => new
                {
                    p.Id,
                    p.Nombre,
                    p.Activo,
                    p.UnidadMedidaId,
                    PermiteFracciones = p.UnidadMedida != null &&
                                       p.UnidadMedida.PermiteFracciones
                })
                .FirstOrDefaultAsync();

            if (producto == null)
                return NotFound("Producto no encontrado.");

            if (!producto.Activo)
                return Conflict("No se puede vender un producto inactivo.");

            if (datos.Cantidad != decimal.Truncate(datos.Cantidad))
            {
                if (!producto.UnidadMedidaId.HasValue)
                    return BadRequest("El producto no tiene una unidad de medida asignada para cantidades decimales.");

                if (!producto.PermiteFracciones)
                    return BadRequest("La unidad de medida del producto no permite cantidades decimales.");
            }

            var existeDetalle = await _context.DetallesVenta
                .AnyAsync(d =>
                    d.VentaId == ventaId &&
                    d.ProductoId == datos.ProductoId);

            if (existeDetalle)
                return Conflict("El producto ya está agregado a esta venta.");

            decimal subtotal;
            decimal nuevoTotal;

            try
            {
                subtotal = decimal.Round(
                    checked(datos.Cantidad * datos.PrecioUnitario),
                    2,
                    MidpointRounding.AwayFromZero);

                if (subtotal >= limiteDinero)
                    return BadRequest("El subtotal supera el límite permitido.");

                nuevoTotal = checked(venta.Total + subtotal);

                if (nuevoTotal >= limiteDinero)
                    return BadRequest("El total de la venta supera el límite permitido.");
            }
            catch (OverflowException)
            {
                return BadRequest("Los valores numéricos superan los límites permitidos.");
            }

            var detalle = new DetalleVenta
            {
                VentaId = ventaId,
                ProductoId = datos.ProductoId,
                Cantidad = datos.Cantidad,
                PrecioUnitario = datos.PrecioUnitario,
                Subtotal = subtotal
            };

            _context.DetallesVenta.Add(detalle);

            venta.Total = nuevoTotal;

            await _context.SaveChangesAsync();

            return StatusCode(201, new
            {
                mensaje = "Producto agregado correctamente a la venta.",
                detalle.Id,
                detalle.VentaId,
                detalle.ProductoId,
                producto.Nombre,
                detalle.Cantidad,
                detalle.PrecioUnitario,
                detalle.Subtotal,
                totalVenta = venta.Total
            });
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> ObtenerVentaPorId(int id)
        {
            if (id <= 0)
                return BadRequest("El ID de la venta debe ser mayor que cero.");

            var venta = await _context.Ventas
                .AsNoTracking()
                .Where(v => v.Id == id)
                .Select(v => new
                {
                    v.Id,
                    v.ClienteId,
                    Cliente = v.Cliente != null
                        ? v.Cliente.NombreRazonSocial
                        : "Consumidor final",
                    v.FechaVenta,
                    v.NumeroComprobante,
                    v.Estado,
                    v.Total
                })
                .FirstOrDefaultAsync();

            if (venta == null)
                return NotFound("Venta no encontrada.");

            var detalles = await _context.DetallesVenta
                .AsNoTracking()
                .Where(d => d.VentaId == id)
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
                venta.Id,
                venta.ClienteId,
                venta.Cliente,
                venta.FechaVenta,
                venta.NumeroComprobante,
                venta.Estado,
                venta.Total,
                Detalles = detalles
            });
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerVentas(
            [FromQuery] int? clienteId,
            [FromQuery] string? estado)
        {
            if (clienteId.HasValue && clienteId.Value <= 0)
                return BadRequest("El ID del cliente debe ser mayor que cero.");

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

            var consulta = _context.Ventas.AsNoTracking();

            if (clienteId.HasValue)
                consulta = consulta.Where(v => v.ClienteId == clienteId.Value);

            if (estadoNormalizado != null)
                consulta = consulta.Where(v => v.Estado == estadoNormalizado);

            var ventas = await consulta
                .OrderByDescending(v => v.FechaVenta)
                .ThenByDescending(v => v.Id)
                .Select(v => new
                {
                    v.Id,
                    v.ClienteId,
                    Cliente = v.Cliente != null
                        ? v.Cliente.NombreRazonSocial
                        : "Consumidor final",
                    v.FechaVenta,
                    v.NumeroComprobante,
                    v.Estado,
                    v.Total,
                    CantidadProductos = _context.DetallesVenta
                        .Count(d => d.VentaId == v.Id)
                })
                .ToListAsync();

            return Ok(ventas);
        }

        [HttpPatch("{id:int}/confirmar")]
        public async Task<IActionResult> ConfirmarVenta(int id)
        {
            if (id <= 0)
                return BadRequest("El ID de la venta debe ser mayor que cero.");

            await using var transaccion =
                await _context.Database.BeginTransactionAsync();

            var filasActualizadas = await _context.Ventas
                .Where(v => v.Id == id && v.Estado == "Borrador")
                .ExecuteUpdateAsync(cambios =>
                    cambios.SetProperty(v => v.Estado, "Confirmada"));

            if (filasActualizadas == 0)
            {
                var ventaExistente = await _context.Ventas
                    .AsNoTracking()
                    .FirstOrDefaultAsync(v => v.Id == id);

                if (ventaExistente == null)
                    return NotFound("Venta no encontrada.");

                return Conflict("Solo se pueden confirmar ventas en estado Borrador.");
            }

            var clienteId = await _context.Ventas
                .AsNoTracking()
                .Where(v => v.Id == id)
                .Select(v => v.ClienteId)
                .SingleAsync();

            if (clienteId.HasValue)
            {
                var clienteActivo = await _context.Clientes
                    .AsNoTracking()
                    .AnyAsync(c =>
                        c.Id == clienteId.Value &&
                        c.Activo);

                if (!clienteActivo)
                    return Conflict("No se puede confirmar una venta de un cliente inactivo.");
            }

            var detalles = await _context.DetallesVenta
                .AsNoTracking()
                .Where(d => d.VentaId == id)
                .Select(d => new
                {
                    d.ProductoId,
                    d.Cantidad
                })
                .ToListAsync();

            if (detalles.Count == 0)
                return Conflict("No se puede confirmar una venta sin productos.");

            const decimal stockMaximo = 999999999999999.999m;

            foreach (var detalle in detalles)
            {
                if (detalle.Cantidad <= 0 ||
                    detalle.Cantidad > stockMaximo ||
                    decimal.Round(detalle.Cantidad, 3) != detalle.Cantidad)
                {
                    return Conflict("La venta contiene una cantidad inválida.");
                }

                var producto = await _context.Productos
                    .AsNoTracking()
                    .Where(p => p.Id == detalle.ProductoId)
                    .Select(p => new
                    {
                        p.Nombre,
                        p.Activo,
                        p.UnidadMedidaId,
                        PermiteFracciones = p.UnidadMedida != null &&
                                           p.UnidadMedida.PermiteFracciones
                    })
                    .FirstOrDefaultAsync();

                if (producto == null)
                    return NotFound("Uno de los productos de la venta no existe.");

                if (!producto.Activo)
                    return Conflict("No se puede confirmar una venta con productos inactivos.");

                if (detalle.Cantidad != decimal.Truncate(detalle.Cantidad))
                {
                    if (!producto.UnidadMedidaId.HasValue)
                        return Conflict("Un producto no tiene unidad de medida para cantidades decimales.");

                    if (!producto.PermiteFracciones)
                        return Conflict("La unidad de medida de un producto no permite cantidades decimales.");
                }

                var stockActualizado = await _context.Productos
                    .Where(p =>
                        p.Id == detalle.ProductoId &&
                        p.Activo &&
                        p.Stock >= detalle.Cantidad)
                    .ExecuteUpdateAsync(cambios =>
                        cambios.SetProperty(
                            p => p.Stock,
                            p => p.Stock - detalle.Cantidad));

                if (stockActualizado == 0)
                {
                    return Conflict(
                        $"Stock insuficiente o producto no disponible: {producto.Nombre}.");
                }

                var movimiento = new MovimientoStock
                {
                    ProductoId = detalle.ProductoId,
                    Tipo = TipoMovimiento.Salida,
                    Cantidad = detalle.Cantidad,
                    Observacion = $"Salida por venta #{id}",
                    FechaMovimiento = DateTime.UtcNow
                };

                _context.MovimientosStock.Add(movimiento);
            }

            await _context.SaveChangesAsync();

            await transaccion.CommitAsync();

            return Ok(new
            {
                mensaje = "Venta confirmada correctamente.",
                ventaId = id,
                estado = "Confirmada",
                movimientosRegistrados = detalles.Count
            });
        }

        [HttpPatch("{id:int}/anular")]
        public async Task<IActionResult> AnularVenta(int id)
        {
            if (id <= 0)
                return BadRequest("El ID de la venta debe ser mayor que cero.");

            var filasActualizadas = await _context.Ventas
                .Where(v => v.Id == id && v.Estado == "Borrador")
                .ExecuteUpdateAsync(cambios =>
                    cambios.SetProperty(v => v.Estado, "Anulada"));

            if (filasActualizadas == 0)
            {
                var venta = await _context.Ventas
                    .AsNoTracking()
                    .FirstOrDefaultAsync(v => v.Id == id);

                if (venta == null)
                    return NotFound("Venta no encontrada.");

                if (venta.Estado == "Anulada")
                    return Conflict("La venta ya está anulada.");

                if (venta.Estado == "Confirmada")
                    return Conflict("No se puede anular una venta confirmada.");

                return Conflict("Solo se pueden anular ventas en estado Borrador.");
            }

            return Ok(new
            {
                mensaje = "Venta anulada correctamente.",
                ventaId = id,
                estado = "Anulada"
            });
        }

        [HttpDelete("{ventaId:int}/detalles/{detalleId:int}")]
        public async Task<IActionResult> EliminarDetalleVenta(
            int ventaId, int detalleId)
        {
            if (ventaId <= 0 || detalleId <= 0)
                return BadRequest("Los ID deben ser mayores que cero.");

            await using var transaccion =
                await _context.Database.BeginTransactionAsync();

            var venta = await _context.Ventas
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == ventaId);

            if (venta == null)
                return NotFound("Venta no encontrada.");

            if (venta.Estado != "Borrador")
                return Conflict(
                    "Solo se pueden eliminar productos de ventas en estado Borrador.");

            var detalle = await _context.DetallesVenta
                .FirstOrDefaultAsync(d =>
                    d.Id == detalleId &&
                    d.VentaId == ventaId);

            if (detalle == null)
                return NotFound("El producto no existe en esta venta.");

            var filasActualizadas = await _context.Ventas
                .Where(v =>
                    v.Id == ventaId &&
                    v.Estado == "Borrador" &&
                    v.Total >= detalle.Subtotal)
                .ExecuteUpdateAsync(cambios =>
                    cambios.SetProperty(
                        v => v.Total,
                        v => v.Total - detalle.Subtotal));

            if (filasActualizadas == 0)
                return Conflict("No se pudo actualizar el total de la venta.");

            _context.DetallesVenta.Remove(detalle);

            await _context.SaveChangesAsync();

            var totalActual = await _context.Ventas
                .AsNoTracking()
                .Where(v => v.Id == ventaId)
                .Select(v => v.Total)
                .SingleAsync();

            await transaccion.CommitAsync();

            return Ok(new
            {
                mensaje = "Producto eliminado correctamente de la venta.",
                ventaId,
                detalleId,
                totalVenta = totalActual
            });
        }

        [HttpPut("{ventaId:int}/detalles/{detalleId:int}")]
        public async Task<IActionResult> ActualizarDetalleVenta(
            int ventaId,
            int detalleId,
            [FromBody] ActualizarDetalleVentaDto datos)
        {
            if (ventaId <= 0 || detalleId <= 0)
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

            var venta = await _context.Ventas
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == ventaId);

            if (venta == null)
                return NotFound("Venta no encontrada.");

            if (venta.Estado != "Borrador")
                return Conflict("Solo se pueden modificar ventas en estado Borrador.");

            var detalle = await _context.DetallesVenta
                .FirstOrDefaultAsync(d =>
                    d.Id == detalleId &&
                    d.VentaId == ventaId);

            if (detalle == null)
                return NotFound("El detalle no existe en esta venta.");

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

            var filasActualizadas = await _context.Ventas
                .Where(v =>
                    v.Id == ventaId &&
                    v.Estado == "Borrador" &&
                    v.Total >= subtotalAnterior &&
                    (diferencia <= 0 || v.Total < limiteDinero - diferencia))
                .ExecuteUpdateAsync(cambios =>
                    cambios.SetProperty(
                        v => v.Total,
                        v => v.Total - subtotalAnterior + nuevoSubtotal));

            if (filasActualizadas == 0)
                return Conflict("No se pudo actualizar el total de la venta.");

            detalle.Cantidad = datos.Cantidad;
            detalle.PrecioUnitario = datos.PrecioUnitario;
            detalle.Subtotal = nuevoSubtotal;

            await _context.SaveChangesAsync();

            var totalActual = await _context.Ventas
                .AsNoTracking()
                .Where(v => v.Id == ventaId)
                .Select(v => v.Total)
                .SingleAsync();

            await transaccion.CommitAsync();

            return Ok(new
            {
                mensaje = "Detalle de venta actualizado correctamente.",
                detalle.Id,
                detalle.VentaId,
                detalle.ProductoId,
                detalle.Cantidad,
                detalle.PrecioUnitario,
                detalle.Subtotal,
                totalVenta = totalActual
            });
        }

    }
}
