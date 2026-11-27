using Inventario.Api.Models;
using Inventario.Api.Authorization;
using Inventario.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Api.Controllers
{
    [ApiController]
    [Route("api/reportes")]
    public class ReportesController : ControllerBase
    {
        private readonly InventarioDbContext _context;

        public ReportesController(InventarioDbContext context)
        {
            _context = context;
        }

        [Permiso(Permisos.ReportesVer)]
        [HttpGet("resumen")]
        [ResponseCache(
            NoStore = true,
            Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> ObtenerResumenGeneral()
        {
            var productosActivos = await _context.Productos
                .CountAsync(p => p.Activo);

            var productosInactivos = await _context.Productos
                .CountAsync(p => !p.Activo);

            var productosStockBajo = await _context.Productos
                .CountAsync(p =>
                    p.Activo &&
                    p.Stock <= p.StockMinimo);

            var productosSinStock = await _context.Productos
                .CountAsync(p =>
                    p.Activo &&
                    p.Stock == 0);

            var clientesActivos = await _context.Clientes
                .CountAsync(c => c.Activo);

            var proveedoresActivos = await _context.Proveedores
                .CountAsync(p => p.Activo);

            var ventasConfirmadas = await _context.Ventas
                .CountAsync(v => v.Estado == "Confirmada");

            var totalVentas = await _context.Ventas
                .Where(v => v.Estado == "Confirmada")
                .SumAsync(v => (decimal?)v.Total) ?? 0m;

            var comprasConfirmadas = await _context.Compras
                .CountAsync(c => c.Estado == "Confirmada");

            var totalCompras = await _context.Compras
                .Where(c => c.Estado == "Confirmada")
                .SumAsync(c => (decimal?)c.Total) ?? 0m;

            var totalCobrado = await _context.PagosVenta
                .SumAsync(p => (decimal?)p.Monto) ?? 0m;

            var saldoPendiente = totalVentas - totalCobrado;

            var cajaAbierta = await _context.Cajas
                .AsNoTracking()
                .Where(c => c.Estado == "Abierta")
                .Select(c => new
                {
                    c.Id,
                    c.FechaApertura,
                    c.MontoInicial,
                    c.Estado
                })
                .FirstOrDefaultAsync();

            return Ok(new
            {
                productos = new
                {
                    total = productosActivos + productosInactivos,
                    activos = productosActivos,
                    inactivos = productosInactivos,
                    stockBajo = productosStockBajo,
                    sinStock = productosSinStock
                },
                clientes = new
                {
                    activos = clientesActivos
                },
                proveedores = new
                {
                    activos = proveedoresActivos
                },
                ventas = new
                {
                    cantidad = ventasConfirmadas,
                    total = totalVentas,
                    totalCobrado,
                    saldoPendiente
                },
                compras = new
                {
                    cantidad = comprasConfirmadas,
                    total = totalCompras
                },
                caja = new
                {
                    abierta = cajaAbierta != null,
                    datos = cajaAbierta == null
                        ? null
                        : new
                        {
                            cajaAbierta.Id,
                            FechaApertura = DateTime.SpecifyKind(
                                cajaAbierta.FechaApertura,
                                DateTimeKind.Utc),
                            cajaAbierta.MontoInicial,
                            cajaAbierta.Estado
                        }
                },
                fechaConsultaUtc = DateTime.UtcNow
            });
        }

        [Permiso(Permisos.ReportesVer)]
        [HttpGet("ventas")]
        [ResponseCache(
            NoStore = true,
            Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> ObtenerReporteVentas(
            [FromQuery] DateTimeOffset? desde,
            [FromQuery] DateTimeOffset? hasta)
        {
            if (!desde.HasValue || !hasta.HasValue)
                return BadRequest(
                    "Debes proporcionar las fechas desde y hasta.");

            var fechaDesde = desde.Value.UtcDateTime;
            var fechaHasta = hasta.Value.UtcDateTime;

            if (fechaDesde >= fechaHasta)
                return BadRequest(
                    "La fecha desde debe ser anterior a la fecha hasta.");

            if ((fechaHasta - fechaDesde).TotalDays > 366)
                return BadRequest(
                    "El período no puede superar los 366 días.");

            var ventasPorDia = await _context.Ventas
                .AsNoTracking()
                .Where(v =>
                    v.Estado == "Confirmada" &&
                    v.FechaVenta >= fechaDesde &&
                    v.FechaVenta < fechaHasta)
                .GroupBy(v => v.FechaVenta.Date)
                .Select(g => new
                {
                    fecha = g.Key,
                    cantidad = g.Count(),
                    total = g.Sum(v => v.Total)
                })
                .OrderBy(v => v.fecha)
                .ToListAsync(HttpContext.RequestAborted);

            var cantidadVentas = ventasPorDia.Sum(v => v.cantidad);
            var totalVentas = ventasPorDia.Sum(v => v.total);

            var promedioVenta = cantidadVentas > 0
                ? decimal.Round(
                    totalVentas / cantidadVentas,
                    2,
                    MidpointRounding.AwayFromZero)
                : 0m;

            return Ok(new
            {
                desde = fechaDesde,
                hasta = fechaHasta,
                cantidadVentas,
                totalVentas,
                promedioVenta,
                ventasPorDia = ventasPorDia.Select(v => new
                {
                    fecha = DateOnly.FromDateTime(v.fecha),
                    v.cantidad,
                    v.total
                })
            });
        }

        [Permiso(Permisos.ReportesVer)]
        [HttpGet("compras")]
        [ResponseCache(
            NoStore = true,
            Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> ObtenerReporteCompras(
            [FromQuery] DateTimeOffset? desde,
            [FromQuery] DateTimeOffset? hasta)
        {
            if (!desde.HasValue || !hasta.HasValue)
                return BadRequest(
                    "Debes proporcionar las fechas desde y hasta.");

            var fechaDesde = desde.Value.UtcDateTime;
            var fechaHasta = hasta.Value.UtcDateTime;

            if (fechaDesde >= fechaHasta)
                return BadRequest(
                    "La fecha desde debe ser anterior a la fecha hasta.");

            if ((fechaHasta - fechaDesde).TotalDays > 366)
                return BadRequest(
                    "El período no puede superar los 366 días.");

            var comprasPorDia = await _context.Compras
                .AsNoTracking()
                .Where(c =>
                    c.Estado == "Confirmada" &&
                    c.FechaCompra >= fechaDesde &&
                    c.FechaCompra < fechaHasta)
                .GroupBy(c => c.FechaCompra.Date)
                .Select(g => new
                {
                    fecha = g.Key,
                    cantidad = g.Count(),
                    total = g.Sum(c => c.Total)
                })
                .OrderBy(c => c.fecha)
                .ToListAsync(HttpContext.RequestAborted);

            var cantidadCompras = comprasPorDia.Sum(c => c.cantidad);
            var totalCompras = comprasPorDia.Sum(c => c.total);

            var promedioCompra = cantidadCompras > 0
                ? decimal.Round(
                    totalCompras / cantidadCompras,
                    2,
                    MidpointRounding.AwayFromZero)
                : 0m;

            return Ok(new
            {
                desde = fechaDesde,
                hasta = fechaHasta,
                cantidadCompras,
                totalCompras,
                promedioCompra,
                comprasPorDia = comprasPorDia.Select(c => new
                {
                    fecha = DateOnly.FromDateTime(c.fecha),
                    c.cantidad,
                    c.total
                })
            });
        }

        [Permiso(Permisos.ReportesVer)]
        [HttpGet("productos-mas-vendidos")]
        [ResponseCache(
            NoStore = true,
            Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> ObtenerProductosMasVendidos(
            [FromQuery] DateTimeOffset? desde,
            [FromQuery] DateTimeOffset? hasta,
            [FromQuery] int limite = 10)
        {
            if (!desde.HasValue || !hasta.HasValue)
                return BadRequest(
                    "Debes proporcionar las fechas desde y hasta.");

            var fechaDesde = desde.Value.UtcDateTime;
            var fechaHasta = hasta.Value.UtcDateTime;

            if (fechaDesde >= fechaHasta)
                return BadRequest(
                    "La fecha desde debe ser anterior a la fecha hasta.");

            if ((fechaHasta - fechaDesde).TotalDays > 366)
                return BadRequest(
                    "El período no puede superar los 366 días.");

            if (limite < 1 || limite > 100)
                return BadRequest(
                    "El límite debe estar entre 1 y 100 productos.");

            var consulta =
                from detalle in _context.DetallesVenta.AsNoTracking()
                join venta in _context.Ventas.AsNoTracking()
                    on detalle.VentaId equals venta.Id
                join producto in _context.Productos.AsNoTracking()
                    on detalle.ProductoId equals producto.Id
                where venta.Estado == "Confirmada" &&
                      venta.FechaVenta >= fechaDesde &&
                      venta.FechaVenta < fechaHasta
                group detalle by new
                {
                    producto.Id,
                    producto.Nombre,
                    producto.CodigoSku
                }
                into grupo
                select new
                {
                    productoId = grupo.Key.Id,
                    nombre = grupo.Key.Nombre,
                    codigoSku = grupo.Key.CodigoSku,
                    cantidadVendida = grupo.Sum(d => d.Cantidad),
                    totalVendido = grupo.Sum(d => d.Subtotal),
                    cantidadVentas = grupo.Count()
                };

            var productos = await consulta
                .OrderByDescending(p => p.cantidadVendida)
                .ThenByDescending(p => p.totalVendido)
                .ThenBy(p => p.productoId)
                .Take(limite)
                .ToListAsync(HttpContext.RequestAborted);

            return Ok(new
            {
                desde = fechaDesde,
                hasta = fechaHasta,
                limite,
                cantidadProductos = productos.Count,
                productos
            });
        }

        [Permiso(Permisos.ReportesVer)]
        [HttpGet("stock-bajo")]
        [ResponseCache(
            NoStore = true,
            Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> ObtenerReporteStockBajo(
            [FromQuery] int pagina = 1,
            [FromQuery] int tamanoPagina = 20)
        {
            if (pagina < 1)
                return BadRequest(
                    "La página debe ser mayor que cero.");

            if (tamanoPagina < 1 || tamanoPagina > 100)
                return BadRequest(
                    "El tamaño de página debe estar entre 1 y 100.");

            var desplazamiento = ((long)pagina - 1) * tamanoPagina;

            if (desplazamiento > int.MaxValue)
                return BadRequest(
                    "La página solicitada supera el límite permitido.");

            var consulta = _context.Productos
                .AsNoTracking()
                .Where(p =>
                    p.Activo &&
                    p.Stock <= p.StockMinimo);

            var totalRegistros = await consulta
                .CountAsync(HttpContext.RequestAborted);

            var productos = await consulta
                .OrderByDescending(p => p.Stock == 0)
                .ThenByDescending(p => p.StockMinimo - p.Stock)
                .ThenBy(p => p.Id)
                .Skip((int)desplazamiento)
                .Take(tamanoPagina)
                .Select(p => new
                {
                    productoId = p.Id,
                    p.Nombre,
                    p.CodigoSku,
                    p.Stock,
                    p.StockMinimo,
                    p.UnidadMedidaId,
                    cantidadFaltante = p.StockMinimo - p.Stock,
                    estado = p.Stock == 0
                        ? "Sin stock"
                        : "Stock bajo"
                })
                .ToListAsync(HttpContext.RequestAborted);

            var totalPaginas = (int)Math.Ceiling(
                totalRegistros / (double)tamanoPagina);

            return Ok(new
            {
                pagina,
                tamanoPagina,
                totalRegistros,
                totalPaginas,
                tienePaginaAnterior = pagina > 1,
                tienePaginaSiguiente = pagina < totalPaginas,
                productos
            });
        }

        [Permiso(Permisos.ReportesVer)]
        [HttpGet("valorizacion-inventario")]
        [ResponseCache(
            NoStore = true,
            Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> ObtenerValorizacionInventario()
        {
            var resumen = await _context.Productos
                .AsNoTracking()
                .GroupBy(p => p.Activo)
                .Select(g => new
                {
                    activo = g.Key,
                    cantidadProductos = g.Count(),
                    productosConStock = g.Count(p => p.Stock > 0),
                    valorizacion = g.Sum(p => p.Stock * p.Precio)
                })
                .ToListAsync(HttpContext.RequestAborted);

            var activos = resumen.FirstOrDefault(p => p.activo);
            var inactivos = resumen.FirstOrDefault(p => !p.activo);

            var valorActivos = decimal.Round(
                activos?.valorizacion ?? 0m,
                2,
                MidpointRounding.AwayFromZero);

            var valorInactivos = decimal.Round(
                inactivos?.valorizacion ?? 0m,
                2,
                MidpointRounding.AwayFromZero);

            return Ok(new
            {
                cantidadProductos = resumen.Sum(p => p.cantidadProductos),
                productosConStock = resumen.Sum(p => p.productosConStock),
                valorizacionTotal = valorActivos + valorInactivos,
                productosActivos = new
                {
                    cantidad = activos?.cantidadProductos ?? 0,
                    valorizacion = valorActivos
                },
                productosInactivos = new
                {
                    cantidad = inactivos?.cantidadProductos ?? 0,
                    valorizacion = valorInactivos
                },
                fechaConsultaUtc = DateTime.UtcNow
            });
        }

        [Permiso(Permisos.ReportesVer)]
        [HttpGet("cobros-por-metodo")]
        [ResponseCache(
            NoStore = true,
            Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> ObtenerCobrosPorMetodo(
            [FromQuery] DateTimeOffset? desde,
            [FromQuery] DateTimeOffset? hasta)
        {
            if (!desde.HasValue || !hasta.HasValue)
                return BadRequest(
                    "Debes proporcionar las fechas desde y hasta.");

            var fechaDesde = desde.Value.UtcDateTime;
            var fechaHasta = hasta.Value.UtcDateTime;

            if (fechaDesde >= fechaHasta)
                return BadRequest(
                    "La fecha desde debe ser anterior a la fecha hasta.");

            if ((fechaHasta - fechaDesde).TotalDays > 366)
                return BadRequest(
                    "El período no puede superar los 366 días.");

            var cobros = await (
                from pago in _context.PagosVenta.AsNoTracking()
                join metodo in _context.MetodosPago.AsNoTracking()
                    on pago.MetodoPagoId equals metodo.Id
                where pago.FechaPago >= fechaDesde &&
                      pago.FechaPago < fechaHasta
                group pago by new
                {
                    metodo.Id,
                    metodo.Nombre,
                    metodo.EsEfectivo
                }
                into grupo
                select new
                {
                    metodoPagoId = grupo.Key.Id,
                    metodoPago = grupo.Key.Nombre,
                    esEfectivo = grupo.Key.EsEfectivo,
                    cantidadPagos = grupo.Count(),
                    total = grupo.Sum(p => p.Monto)
                })
                .OrderByDescending(p => p.total)
                .ThenBy(p => p.metodoPagoId)
                .ToListAsync(HttpContext.RequestAborted);

            var totalCobrado = cobros.Sum(p => p.total);

            var totalEfectivo = cobros
                .Where(p => p.esEfectivo)
                .Sum(p => p.total);

            var totalElectronico = cobros
                .Where(p => !p.esEfectivo)
                .Sum(p => p.total);

            return Ok(new
            {
                desde = fechaDesde,
                hasta = fechaHasta,
                cantidadPagos = cobros.Sum(p => p.cantidadPagos),
                totalCobrado,
                totalEfectivo,
                totalElectronico,
                metodosPago = cobros.Select(p => new
                {
                    p.metodoPagoId,
                    p.metodoPago,
                    p.esEfectivo,
                    p.cantidadPagos,
                    p.total,
                    porcentaje = totalCobrado > 0
                        ? decimal.Round(
                            p.total / totalCobrado * 100,
                            2,
                            MidpointRounding.AwayFromZero)
                        : 0m
                })
            });
        }

        [Permiso(Permisos.ReportesVer)]
        [HttpGet("cajas")]
        [ResponseCache(
            NoStore = true,
            Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> ObtenerReporteCajas(
            [FromQuery] DateTimeOffset? desde,
            [FromQuery] DateTimeOffset? hasta)
        {
            if (!desde.HasValue || !hasta.HasValue)
                return BadRequest(
                    "Debes proporcionar las fechas desde y hasta.");

            var fechaDesde = desde.Value.UtcDateTime;
            var fechaHasta = hasta.Value.UtcDateTime;

            if (fechaDesde >= fechaHasta)
                return BadRequest(
                    "La fecha desde debe ser anterior a la fecha hasta.");

            if ((fechaHasta - fechaDesde).TotalDays > 366)
                return BadRequest(
                    "El período no puede superar los 366 días.");

            var cajas = await _context.Cajas
                .AsNoTracking()
                .Where(c =>
                    c.FechaApertura >= fechaDesde &&
                    c.FechaApertura < fechaHasta)
                .OrderByDescending(c => c.FechaApertura)
                .ThenByDescending(c => c.Id)
                .Select(c => new
                {
                    cajaId = c.Id,
                    c.Estado,
                    c.FechaApertura,
                    c.FechaCierre,
                    c.MontoInicial,
                    c.MontoEsperado,
                    c.MontoReal,
                    Diferencia = (decimal?)c.Diferencia,

                    cobrosEfectivo = _context.PagosVenta
                        .Where(p =>
                            p.CajaId == c.Id &&
                            p.MetodoPago.EsEfectivo)
                        .Sum(p => (decimal?)p.Monto) ?? 0m,

                    cobrosElectronicos = _context.PagosVenta
                        .Where(p =>
                            p.CajaId == c.Id &&
                            !p.MetodoPago.EsEfectivo)
                        .Sum(p => (decimal?)p.Monto) ?? 0m,

                    totalIngresos = _context.MovimientosCaja
                        .Where(m =>
                            m.CajaId == c.Id &&
                            m.Tipo == TipoMovimientoCaja.Ingreso)
                        .Sum(m => (decimal?)m.Monto) ?? 0m,

                    totalEgresos = _context.MovimientosCaja
                        .Where(m =>
                            m.CajaId == c.Id &&
                            m.Tipo == TipoMovimientoCaja.Egreso)
                        .Sum(m => (decimal?)m.Monto) ?? 0m
                })
                .ToListAsync(HttpContext.RequestAborted);

            var totalEfectivo = cajas.Sum(c => c.cobrosEfectivo);

            var totalElectronico = cajas.Sum(c => c.cobrosElectronicos);

            var cajasCerradas = cajas
                .Where(c => c.Estado == "Cerrada")
                .ToList();

            return Ok(new
            {
                desde = fechaDesde,
                hasta = fechaHasta,
                cantidadCajas = cajas.Count,
                cajasAbiertas = cajas.Count(c => c.Estado == "Abierta"),
                cajasCerradas = cajasCerradas.Count,

                totalCobros = totalEfectivo + totalElectronico,
                totalEfectivo,
                totalElectronico,

                totalIngresos = cajas.Sum(c => c.totalIngresos),
                totalEgresos = cajas.Sum(c => c.totalEgresos),

                cajasCuadradas = cajasCerradas.Count(c =>
                    c.Diferencia == 0m),

                cajasConSobrante = cajasCerradas.Count(c =>
                    c.Diferencia > 0m),

                cajasConFaltante = cajasCerradas.Count(c =>
                    c.Diferencia < 0m),

                diferenciaTotal = cajasCerradas.Sum(c =>
                    c.Diferencia ?? 0m),

                cajas = cajas.Select(c => new
                {
                    c.cajaId,
                    c.Estado,

                    FechaApertura = DateTime.SpecifyKind(
                        c.FechaApertura,
                        DateTimeKind.Utc),

                    FechaCierre = c.FechaCierre.HasValue
                        ? (DateTime?)DateTime.SpecifyKind(
                            c.FechaCierre.Value,
                            DateTimeKind.Utc)
                        : null,

                    c.MontoInicial,

                    totalCobros = c.cobrosEfectivo +
                                  c.cobrosElectronicos,

                    c.cobrosEfectivo,
                    c.cobrosElectronicos,
                    c.totalIngresos,
                    c.totalEgresos,

                    montoEsperado = c.Estado == "Cerrada"
                        ? (decimal?)c.MontoEsperado
                        : c.MontoInicial +
                          c.cobrosEfectivo +
                          c.totalIngresos -
                          c.totalEgresos,

                    montoReal = c.Estado == "Cerrada"
                        ? (decimal?)c.MontoReal
                        : null,

                    diferencia = c.Estado == "Cerrada"
                        ? c.Diferencia
                        : null,

                    resultadoArqueo = c.Estado == "Abierta"
                        ? "Pendiente"
                        : c.Diferencia == null
                            ? "Sin datos"
                            : c.Diferencia == 0m
                                ? "Caja cuadrada"
                                : c.Diferencia > 0m
                                    ? "Sobrante"
                                    : "Faltante"
                })
            });
        }
    }
}