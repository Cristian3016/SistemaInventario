using Microsoft.Data.SqlClient;
using Inventario.Api.Data;
using Inventario.Api.DTOs;
using Inventario.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Inventario.Api.Authorization;
using Inventario.Api.Services;

namespace Inventario.Api.Controllers
{
    [ApiController]
    [Route("api/ventas")]
    public class VentasController : ControllerBase
    {
        private readonly InventarioDbContext _context;
        private readonly IAuditoriaService _auditoriaService;

        public VentasController(
            InventarioDbContext context,
            IAuditoriaService auditoriaService)
        {
            _context = context;
            _auditoriaService = auditoriaService;
        }

        private const decimal LimiteCantidad = 1000000000000000m;
        private const decimal LimiteDinero = 10000000000000000m;

        private async Task<IActionResult> EjecutarEscrituraVenta(
            Func<int, Task<IActionResult>> operacion)
        {
            if (!int.TryParse(User.FindFirst("sub")?.Value, out var usuarioId) ||
                usuarioId <= 0)
            {
                return Unauthorized();
            }

            try
            {
                await using var transaccion = await _context.Database
                    .BeginTransactionAsync(
                        System.Data.IsolationLevel.Serializable);

                var resultado = await operacion(usuarioId);

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
                    "La venta fue modificada por otra operación.");
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

            if (subtotal <= 0 || subtotal >= LimiteDinero)
                return BadRequest(
                    "El subtotal está fuera de los límites permitidos.");

            return null;
        }

        private async Task<(Producto? Producto, IActionResult? Error)>
            ValidarProductoVenta(int productoId, decimal cantidad)
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
                    "No se puede vender un producto inactivo."));

            if (cantidad != decimal.Truncate(cantidad))
            {
                if (producto.UnidadMedida == null)
                    return (null, BadRequest(
                        "El producto no tiene una unidad de medida asignada para cantidades decimales."));

                if (!producto.UnidadMedida.PermiteFracciones)
                    return (null, BadRequest(
                        "La unidad de medida del producto no permite cantidades decimales."));
            }

            return (producto, null);
        }

        [Permiso(Permisos.VentasGestionar)]
        [HttpPost]
        public async Task<IActionResult> CrearVenta(
            [FromBody] CrearVentaDto datos)
        {
            return await EjecutarEscrituraVenta(async usuarioId =>
            {
                if (datos == null)
                    return BadRequest(
                        "Debes proporcionar los datos de la venta.");

                if (datos.ClienteId.HasValue &&
                    datos.ClienteId.Value <= 0)
                {
                    return BadRequest(
                        "El ID del cliente debe ser mayor que cero.");
                }

                string? nombreCliente = null;

                if (datos.ClienteId.HasValue)
                {
                    var cliente = await _context.Clientes
                        .AsNoTracking()
                        .FirstOrDefaultAsync(c =>
                            c.Id == datos.ClienteId.Value);

                    if (cliente == null)
                        return NotFound("Cliente no encontrado.");

                    if (!cliente.Activo)
                        return Conflict(
                            "No se pueden registrar ventas a un cliente inactivo.");

                    nombreCliente = cliente.NombreRazonSocial;
                }

                var numeroComprobante = datos.NumeroComprobante?.Trim();

                if (numeroComprobante?.Length > 100)
                    return BadRequest(
                        "El número de comprobante no puede superar los 100 caracteres.");

                var venta = new Venta
                {
                    ClienteId = datos.ClienteId,
                    FechaVenta = DateTime.UtcNow,
                    NumeroComprobante =
                        string.IsNullOrWhiteSpace(numeroComprobante)
                            ? null
                            : numeroComprobante,
                    Estado = "Borrador",
                    Total = 0m
                };

                _context.Ventas.Add(venta);

                await _context.SaveChangesAsync();

                _auditoriaService.Registrar(
                    usuarioId,
                    "CREAR_VENTA",
                    "Venta",
                    venta.Id.ToString(),
                    $"Venta creada en estado Borrador. " +
                    $"Cliente ID: {venta.ClienteId?.ToString() ?? "Consumidor final"}.");

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
            });
        }

        [Permiso(Permisos.VentasGestionar)]
        [HttpPost("{ventaId:int}/detalles")]
        public async Task<IActionResult> AgregarDetalleVenta(
            int ventaId,
            [FromBody] AgregarDetalleVentaDto datos)
        {
            return await EjecutarEscrituraVenta(async usuarioId =>
            {
                if (ventaId <= 0)
                    return BadRequest(
                        "El ID de la venta debe ser mayor que cero.");

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

                var venta = await _context.Ventas
                    .FindAsync(ventaId);

                if (venta == null)
                    return NotFound("Venta no encontrada.");

                if (venta.Estado != "Borrador")
                    return Conflict(
                        "Solo se pueden agregar productos a ventas en estado Borrador.");

                var (producto, errorProducto) =
                    await ValidarProductoVenta(
                        datos.ProductoId,
                        datos.Cantidad);

                if (errorProducto != null)
                    return errorProducto;

                if (producto == null)
                    return NotFound("Producto no encontrado.");

                var existeDetalle = await _context.DetallesVenta
                    .AnyAsync(d =>
                        d.VentaId == ventaId &&
                        d.ProductoId == datos.ProductoId);

                if (existeDetalle)
                    return Conflict(
                        "El producto ya está agregado a esta venta.");

                var nuevoTotal = checked(venta.Total + subtotal);

                if (nuevoTotal >= LimiteDinero)
                    return BadRequest(
                        "El total de la venta supera el límite permitido.");

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

                _auditoriaService.Registrar(
                    usuarioId,
                    "AGREGAR_DETALLE",
                    "Venta",
                    ventaId.ToString(),
                    $"Detalle ID: {detalle.Id}. " +
                    $"Producto ID: {detalle.ProductoId}. " +
                    $"Cantidad: {detalle.Cantidad}. " +
                    $"Precio unitario: {detalle.PrecioUnitario}. " +
                    $"Subtotal: {detalle.Subtotal}. " +
                    $"Total venta: {venta.Total}.");

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
            });
        }

        [Permiso(Permisos.VentasVer)]
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

        [Permiso(Permisos.VentasVer)]
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
                    return BadRequest(
                        "El estado debe ser Borrador, Confirmada o Anulada.");
            }

            var consulta = _context.Ventas.AsNoTracking();

            if (clienteId.HasValue)
                consulta = consulta.Where(
                    v => v.ClienteId == clienteId.Value);

            if (estadoNormalizado != null)
                consulta = consulta.Where(
                    v => v.Estado == estadoNormalizado);

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

        [Permiso(Permisos.VentasGestionar)]
        [HttpPatch("{id:int}/confirmar")]
        public async Task<IActionResult> ConfirmarVenta(int id)
        {
            return await EjecutarEscrituraVenta(async usuarioId =>
            {
                if (id <= 0)
                    return BadRequest(
                        "El ID de la venta debe ser mayor que cero.");

                var venta = await _context.Ventas.FindAsync(id);

                if (venta == null)
                    return NotFound("Venta no encontrada.");

                if (venta.Estado != "Borrador")
                    return Conflict(
                        "Solo se pueden confirmar ventas en estado Borrador.");

                if (venta.ClienteId.HasValue)
                {
                    var clienteActivo = await _context.Clientes
                        .AnyAsync(c =>
                            c.Id == venta.ClienteId.Value &&
                            c.Activo);

                    if (!clienteActivo)
                        return Conflict(
                            "No se puede confirmar una venta de un cliente inactivo.");
                }

                var detalles = await _context.DetallesVenta
                    .AsNoTracking()
                    .Where(d => d.VentaId == id)
                    .OrderBy(d => d.ProductoId)
                    .ToListAsync();

                if (detalles.Count == 0)
                    return Conflict(
                        "No se puede confirmar una venta sin productos.");

                var totalDetalles = detalles.Sum(d => d.Subtotal);

                if (totalDetalles != venta.Total ||
                    venta.Total <= 0 ||
                    venta.Total >= LimiteDinero)
                {
                    return Conflict(
                        "El total de la venta es inválido o no coincide con sus detalles.");
                }

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
                            "La venta contiene un subtotal incorrecto.");

                    var (producto, errorProducto) =
                        await ValidarProductoVenta(
                            detalle.ProductoId,
                            detalle.Cantidad);

                    if (errorProducto != null)
                        return errorProducto;

                    if (producto == null)
                        return NotFound(
                            "Uno de los productos de la venta no existe.");

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
                        return Conflict(
                            $"Stock insuficiente o producto no disponible: {producto.Nombre}.");

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

                venta.Estado = "Confirmada";

                _auditoriaService.Registrar(
                    usuarioId,
                    "CONFIRMAR_VENTA",
                    "Venta",
                    venta.Id.ToString(),
                    $"Venta confirmada correctamente. " +
                    $"Cliente ID: {venta.ClienteId?.ToString() ?? "Consumidor final"}. " +
                    $"Total: {venta.Total}. " +
                    $"Movimientos de stock: {detalles.Count}.");

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    mensaje = "Venta confirmada correctamente.",
                    ventaId = id,
                    estado = "Confirmada",
                    movimientosRegistrados = detalles.Count
                });
            });
        }

        [Permiso(Permisos.VentasGestionar)]
        [HttpPatch("{id:int}/anular")]
        public async Task<IActionResult> AnularVenta(int id)
        {
            return await EjecutarEscrituraVenta(async usuarioId =>
            {
                if (id <= 0)
                    return BadRequest(
                        "El ID de la venta debe ser mayor que cero.");

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
                        return Conflict(
                            "No se puede anular una venta confirmada.");

                    return Conflict(
                        "Solo se pueden anular ventas en estado Borrador.");
                }

                _auditoriaService.Registrar(
                    usuarioId,
                    "ANULAR_VENTA",
                    "Venta",
                    id.ToString(),
                    "Venta anulada correctamente desde el estado Borrador.");

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    mensaje = "Venta anulada correctamente.",
                    ventaId = id,
                    estado = "Anulada"
                });
            });
        }

        [Permiso(Permisos.VentasGestionar)]
        [HttpDelete("{ventaId:int}/detalles/{detalleId:int}")]
        public async Task<IActionResult> EliminarDetalleVenta(
            int ventaId,
            int detalleId)
        {
            return await EjecutarEscrituraVenta(async usuarioId =>
            {
                if (ventaId <= 0 || detalleId <= 0)
                    return BadRequest(
                        "Los ID deben ser mayores que cero.");

                var venta = await _context.Ventas
                    .FindAsync(ventaId);

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
                    return NotFound(
                        "El producto no existe en esta venta.");

                if (detalle.Subtotal < 0 ||
                    venta.Total < detalle.Subtotal)
                {
                    return Conflict(
                        "El total de la venta es inconsistente.");
                }

                venta.Total -= detalle.Subtotal;

                _auditoriaService.Registrar(
                    usuarioId,
                    "ELIMINAR_DETALLE",
                    "Venta",
                    ventaId.ToString(),
                    $"Detalle ID: {detalle.Id}. " +
                    $"Producto ID: {detalle.ProductoId}. " +
                    $"Cantidad eliminada: {detalle.Cantidad}. " +
                    $"Subtotal eliminado: {detalle.Subtotal}. " +
                    $"Total venta: {venta.Total}.");

                _context.DetallesVenta.Remove(detalle);

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    mensaje = "Producto eliminado correctamente de la venta.",
                    ventaId,
                    detalleId,
                    totalVenta = venta.Total
                });
            });
        }

        [Permiso(Permisos.VentasGestionar)]
        [HttpPut("{ventaId:int}/detalles/{detalleId:int}")]
        public async Task<IActionResult> ActualizarDetalleVenta(
            int ventaId,
            int detalleId,
            [FromBody] ActualizarDetalleVentaDto datos)
        {
            return await EjecutarEscrituraVenta(async usuarioId =>
            {
                if (ventaId <= 0 || detalleId <= 0)
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

                var venta = await _context.Ventas
                    .FindAsync(ventaId);

                if (venta == null)
                    return NotFound("Venta no encontrada.");

                if (venta.Estado != "Borrador")
                    return Conflict(
                        "Solo se pueden modificar ventas en estado Borrador.");

                var detalle = await _context.DetallesVenta
                    .FirstOrDefaultAsync(d =>
                        d.Id == detalleId &&
                        d.VentaId == ventaId);

                if (detalle == null)
                    return NotFound(
                        "El detalle no existe en esta venta.");

                var (_, errorProducto) =
                    await ValidarProductoVenta(
                        detalle.ProductoId,
                        datos.Cantidad);

                if (errorProducto != null)
                    return errorProducto;

                if (detalle.Subtotal < 0 ||
                    venta.Total < detalle.Subtotal)
                {
                    return Conflict(
                        "El total de la venta es inconsistente.");
                }

                var nuevoTotal = checked(
                    venta.Total - detalle.Subtotal + nuevoSubtotal);

                if (nuevoTotal >= LimiteDinero)
                    return BadRequest(
                        "El total de la venta supera el límite permitido.");

                var cantidadAnterior = detalle.Cantidad;
                var precioAnterior = detalle.PrecioUnitario;
                var subtotalAnterior = detalle.Subtotal;
                var totalAnterior = venta.Total;

                detalle.Cantidad = datos.Cantidad;
                detalle.PrecioUnitario = datos.PrecioUnitario;
                detalle.Subtotal = nuevoSubtotal;

                venta.Total = nuevoTotal;

                _auditoriaService.Registrar(
                    usuarioId,
                    "MODIFICAR_DETALLE",
                    "Venta",
                    ventaId.ToString(),
                    $"Detalle ID: {detalle.Id}. " +
                    $"Producto ID: {detalle.ProductoId}. " +
                    $"Cantidad anterior: {cantidadAnterior}. " +
                    $"Nueva cantidad: {detalle.Cantidad}. " +
                    $"Precio anterior: {precioAnterior}. " +
                    $"Nuevo precio: {detalle.PrecioUnitario}. " +
                    $"Subtotal anterior: {subtotalAnterior}. " +
                    $"Nuevo subtotal: {detalle.Subtotal}. " +
                    $"Total anterior: {totalAnterior}. " +
                    $"Nuevo total: {venta.Total}.");

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    mensaje = "Detalle de venta actualizado correctamente.",
                    detalle.Id,
                    detalle.VentaId,
                    detalle.ProductoId,
                    detalle.Cantidad,
                    detalle.PrecioUnitario,
                    detalle.Subtotal,
                    totalVenta = venta.Total
                });
            });
        }
    }
}