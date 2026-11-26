using Microsoft.Data.SqlClient;
using Inventario.Api.DTOs;
using System.Net.Mail;
using Inventario.Api.Data;
using Inventario.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Inventario.Api.Authorization;

namespace Inventario.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProveedoresController : ControllerBase
    {
        private readonly InventarioDbContext _context;

        public ProveedoresController(InventarioDbContext context)
        {
            _context = context;
        }

        private static bool EsCuitValido(string cuit)
        {
            if (string.IsNullOrWhiteSpace(cuit) ||
                cuit.Length != 11 ||
                cuit.Any(c => c < '0' || c > '9'))
            {
                return false;
            }

            int[] factores = { 5, 4, 3, 2, 7, 6, 5, 4, 3, 2 };

            int suma = 0;

            for (int i = 0; i < 10; i++)
            {
                int digito = cuit[i] - '0';

                suma += digito * factores[i];
            }

            int digitoVerificador = (11 - (suma % 11)) % 11;

            if (digitoVerificador == 10)
            {
                return false;
            }

            return digitoVerificador == cuit[10] - '0';
        }

        private static bool EsEmailValido(string? email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return true;
            }

            return MailAddress.TryCreate(email, out var direccion) &&
                   string.Equals(
                       direccion.Address,
                       email,
                       StringComparison.OrdinalIgnoreCase) &&
                   direccion.Host.Contains('.');
        }
        private async Task<ActionResult> EjecutarEscrituraProveedor(
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
                    "El registro fue modificado por otra operación.");
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

        private static Proveedor PrepararDatosProveedor(Proveedor datos)
        {
            return new Proveedor
            {
                RazonSocial = datos.RazonSocial?.Trim() ?? string.Empty,
                NombreFantasia = datos.NombreFantasia?.Trim() ?? string.Empty,
                Cuit = string.IsNullOrWhiteSpace(datos.Cuit)
                    ? null
                    : datos.Cuit.Trim(),
                Email = string.IsNullOrWhiteSpace(datos.Email)
                    ? null
                    : datos.Email.Trim(),
                Telefono = datos.Telefono?.Trim(),
                Direccion = datos.Direccion?.Trim(),
                Contacto = datos.Contacto?.Trim()
            };
        }

        private async Task<ActionResult?> ValidarDatosProveedor(
            Proveedor datos, int? id = null)
        {
            if (string.IsNullOrWhiteSpace(datos.RazonSocial))
                return BadRequest("La razón social es obligatoria.");

            if (datos.RazonSocial.Length > 200)
                return BadRequest(
                    "La razón social no puede superar los 200 caracteres.");

            if (datos.NombreFantasia?.Length > 200)
                return BadRequest(
                    "El nombre de fantasía no puede superar los 200 caracteres.");

            if (datos.Cuit != null)
            {
                if (datos.Cuit.Length != 11 ||
                    datos.Cuit.Any(c => c < '0' || c > '9'))
                {
                    return BadRequest(
                        "El CUIT debe contener exactamente 11 dígitos numéricos, sin guiones.");
                }

                if (!EsCuitValido(datos.Cuit))
                    return BadRequest(
                        "El CUIT ingresado tiene un dígito verificador inválido.");

                var existeCuit = await _context.Proveedores
                    .AnyAsync(p =>
                        p.Cuit == datos.Cuit &&
                        (!id.HasValue || p.Id != id.Value));

                if (existeCuit)
                    return Conflict(
                        "Ya existe otro proveedor registrado con ese CUIT.");
            }

            if (datos.Email?.Length > 254 ||
                datos.Telefono?.Length > 30 ||
                datos.Direccion?.Length > 250 ||
                datos.Contacto?.Length > 150)
            {
                return BadRequest(
                    "Uno o más campos superan la longitud máxima permitida.");
            }

            if (!EsEmailValido(datos.Email))
                return BadRequest(
                    "El correo electrónico ingresado no tiene un formato válido.");

            return null;
        }

        private static void CopiarDatosProveedor(
            Proveedor destino, Proveedor origen)
        {
            destino.RazonSocial = origen.RazonSocial;
            destino.NombreFantasia = origen.NombreFantasia;
            destino.Cuit = origen.Cuit;
            destino.Email = origen.Email;
            destino.Telefono = origen.Telefono;
            destino.Direccion = origen.Direccion;
            destino.Contacto = origen.Contacto;
        }

        [Permiso(Permisos.ProveedoresGestionar)]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Proveedor>>> ObtenerProveedores(
            [FromQuery] bool? activo,
            [FromQuery] string? buscar)
        {
            IQueryable<Proveedor> consulta = _context.Proveedores
                .AsNoTracking();

            if (activo.HasValue)
            {
                consulta = consulta.Where(p => p.Activo == activo.Value);
            }

            if (!string.IsNullOrWhiteSpace(buscar))
            {
                var termino = buscar.Trim();

                consulta = consulta.Where(p =>
                    p.RazonSocial.Contains(termino) ||
                    p.NombreFantasia.Contains(termino) ||
                    (p.Cuit != null && p.Cuit.Contains(termino)));
            }

            var proveedores = await consulta
                .OrderBy(p => p.RazonSocial)
                .ToListAsync();

            return Ok(proveedores);
        }

        [Permiso(Permisos.ProveedoresGestionar)]
        [HttpPost]
        public async Task<ActionResult<Proveedor>> CrearProveedor(
     [FromBody] Proveedor nuevoProveedor)
        {
            return await EjecutarEscrituraProveedor(async () =>
            {
                var proveedor = PrepararDatosProveedor(nuevoProveedor);

                var validacion = await ValidarDatosProveedor(proveedor);

                if (validacion != null)
                    return validacion;

                proveedor.Activo = true;
                proveedor.FechaCreacion = DateTime.UtcNow;

                _context.Proveedores.Add(proveedor);

                await _context.SaveChangesAsync();

                return CreatedAtAction(
                    nameof(ObtenerProveedorPorId),
                    new { id = proveedor.Id },
                    proveedor);
            });
        }

        [Permiso(Permisos.ProveedoresGestionar)]
        [HttpGet("{id:int}")]
        public async Task<ActionResult<Proveedor>> ObtenerProveedorPorId(int id)
        {
            var proveedor = await _context.Proveedores
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id);

            if (proveedor == null)
            {
                return NotFound("Proveedor no encontrado.");
            }

            return Ok(proveedor);
        }

        [Permiso(Permisos.ProveedoresGestionar)]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> ActualizarProveedor(
    int id, [FromBody] Proveedor proveedorActualizado)
        {
            return await EjecutarEscrituraProveedor(async () =>
            {
                if (id <= 0)
                    return BadRequest(
                        "El ID debe ser mayor que cero.");

                var proveedor = await _context.Proveedores.FindAsync(id);

                if (proveedor == null)
                    return NotFound("Proveedor no encontrado.");

                if (!proveedor.Activo)
                    return Conflict(
                        "No se puede modificar un proveedor inactivo. Primero debes activarlo.");

                var datos = PrepararDatosProveedor(proveedorActualizado);

                var validacion = await ValidarDatosProveedor(datos, id);

                if (validacion != null)
                    return validacion;

                CopiarDatosProveedor(proveedor, datos);

                await _context.SaveChangesAsync();

                return NoContent();
            });
        }

        [Permiso(Permisos.ProveedoresGestionar)]
        [HttpPatch("{id:int}")]
        public async Task<IActionResult> ActualizarParcialProveedor(
    int id, [FromBody] ProveedorPatchDto datosPatch)
        {
            return await EjecutarEscrituraProveedor(async () =>
            {
                if (id <= 0)
                    return BadRequest(
                        "El ID debe ser mayor que cero.");

                var proveedor = await _context.Proveedores.FindAsync(id);

                if (proveedor == null)
                    return NotFound("Proveedor no encontrado.");

                if (!proveedor.Activo)
                    return Conflict(
                        "No se puede modificar un proveedor inactivo. Primero debes activarlo.");

                if (datosPatch.RazonSocial == null &&
                    datosPatch.NombreFantasia == null &&
                    datosPatch.Cuit == null &&
                    datosPatch.Email == null &&
                    datosPatch.Telefono == null &&
                    datosPatch.Direccion == null &&
                    datosPatch.Contacto == null)
                {
                    return BadRequest(
                        "Debes enviar al menos un campo para actualizar.");
                }

                var datos = new Proveedor
                {
                    RazonSocial = datosPatch.RazonSocial
                        ?? proveedor.RazonSocial,

                    NombreFantasia = datosPatch.NombreFantasia
                        ?? proveedor.NombreFantasia,

                    Cuit = datosPatch.Cuit
                        ?? proveedor.Cuit,

                    Email = datosPatch.Email
                        ?? proveedor.Email,

                    Telefono = datosPatch.Telefono
                        ?? proveedor.Telefono,

                    Direccion = datosPatch.Direccion
                        ?? proveedor.Direccion,

                    Contacto = datosPatch.Contacto
                        ?? proveedor.Contacto
                };

                datos = PrepararDatosProveedor(datos);

                var validacion = await ValidarDatosProveedor(datos, id);

                if (validacion != null)
                    return validacion;

                CopiarDatosProveedor(proveedor, datos);

                await _context.SaveChangesAsync();

                return Ok(proveedor);
            });
        }

        [Permiso(Permisos.ProveedoresGestionar)]
        [HttpPatch("{id:int}/desactivar")]
        public async Task<IActionResult> DesactivarProveedor(int id)
        {
            var proveedor = await _context.Proveedores.FindAsync(id);

            if (proveedor == null)
            {
                return NotFound("Proveedor no encontrado.");
            }

            if (!proveedor.Activo)
            {
                return Conflict("El proveedor ya está desactivado.");
            }

            proveedor.Activo = false;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                mensaje = "Proveedor desactivado correctamente.",
                proveedorId = proveedor.Id,
                razonSocial = proveedor.RazonSocial,
                activo = proveedor.Activo
            });
        }

        [Permiso(Permisos.ProveedoresGestionar)]
        [HttpPatch("{id:int}/activar")]
        public async Task<IActionResult> ActivarProveedor(int id)
        {
            var proveedor = await _context.Proveedores.FindAsync(id);

            if (proveedor == null)
            {
                return NotFound("Proveedor no encontrado.");
            }

            if (proveedor.Activo)
            {
                return Conflict("El proveedor ya está activo.");
            }

            proveedor.Activo = true;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                mensaje = "Proveedor activado correctamente.",
                proveedorId = proveedor.Id,
                razonSocial = proveedor.RazonSocial,
                activo = proveedor.Activo
            });
        }

        [Permiso(Permisos.ProveedoresGestionar)]
        [HttpPost("vincular-producto")]
        public async Task<IActionResult> VincularProductoProveedor(
    [FromBody] VincularProductoProveedorDto datos)
        {
            return await EjecutarEscrituraProveedor(async () =>
            {
                if (datos == null)
                    return BadRequest(
                        "Debes proporcionar los datos de la vinculación.");

                if (datos.ProductoId <= 0 ||
                    datos.ProveedorId <= 0)
                {
                    return BadRequest(
                        "Los ID del producto y del proveedor deben ser mayores que cero.");
                }

                var producto = await _context.Productos
                    .FindAsync(datos.ProductoId);

                if (producto == null)
                    return NotFound("Producto no encontrado.");

                if (!producto.Activo)
                    return Conflict(
                        "No se puede vincular un producto inactivo.");

                var proveedor = await _context.Proveedores
                    .FindAsync(datos.ProveedorId);

                if (proveedor == null)
                    return NotFound("Proveedor no encontrado.");

                if (!proveedor.Activo)
                    return Conflict(
                        "No se puede vincular un proveedor inactivo.");

                var existeRelacion = await _context.ProductoProveedores
                    .AnyAsync(pp =>
                        pp.ProductoId == datos.ProductoId &&
                        pp.ProveedorId == datos.ProveedorId);

                if (existeRelacion)
                    return Conflict(
                        "El producto ya está vinculado con este proveedor.");

                var relacion = new ProductoProveedor
                {
                    ProductoId = datos.ProductoId,
                    ProveedorId = datos.ProveedorId
                };

                _context.ProductoProveedores.Add(relacion);

                await _context.SaveChangesAsync();

                return StatusCode(201, new
                {
                    mensaje = "Producto vinculado correctamente con el proveedor.",
                    relacion.Id,
                    relacion.ProductoId,
                    relacion.ProveedorId
                });
            });
        }

        [Permiso(Permisos.ProveedoresGestionar)]
        [HttpGet("producto/{productoId:int}")]
        public async Task<IActionResult> ObtenerProveedoresPorProducto(int productoId)
        {
            var producto = await _context.Productos
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == productoId);

            if (producto == null)
                return NotFound("Producto no encontrado.");

            var proveedores = await _context.ProductoProveedores
                .AsNoTracking()
                .Where(pp => pp.ProductoId == productoId)
                .OrderBy(pp => pp.Proveedor.RazonSocial)
                .Select(pp => new
                {
                    pp.Proveedor.Id,
                    pp.Proveedor.RazonSocial,
                    pp.Proveedor.NombreFantasia,
                    pp.Proveedor.Cuit,
                    pp.Proveedor.Email,
                    pp.Proveedor.Telefono,
                    pp.Proveedor.Direccion,
                    pp.Proveedor.Contacto,
                    pp.Proveedor.Activo
                })
                .ToListAsync();

            return Ok(proveedores);
        }

        [Permiso(Permisos.ProveedoresGestionar)]
        [HttpGet("{id:int}/productos")]
        public async Task<IActionResult> ObtenerProductosPorProveedor(int id)
        {
            var proveedor = await _context.Proveedores
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id);

            if (proveedor == null)
                return NotFound("Proveedor no encontrado.");

            var productos = await _context.ProductoProveedores
                .AsNoTracking()
                .Where(pp => pp.ProveedorId == id)
                .OrderBy(pp => pp.Producto.Nombre)
                .Select(pp => new
                {
                    pp.Producto.Id,
                    pp.Producto.Nombre,
                    pp.Producto.Descripcion,
                    pp.Producto.CodigoSku,
                    pp.Producto.Precio,
                    pp.Producto.Stock,
                    pp.Producto.StockMinimo,
                    pp.Producto.Activo
                })
                .ToListAsync();

            return Ok(productos);
        }

        [Permiso(Permisos.ProveedoresGestionar)]
        [HttpDelete("{proveedorId:int}/productos/{productoId:int}")]
        public async Task<IActionResult> DesvincularProductoProveedor(
    int proveedorId, int productoId)
        {
            return await EjecutarEscrituraProveedor(async () =>
            {
                if (proveedorId <= 0 || productoId <= 0)
                    return BadRequest(
                        "Los ID deben ser mayores que cero.");

                var relacion = await _context.ProductoProveedores
                    .FirstOrDefaultAsync(pp =>
                        pp.ProveedorId == proveedorId &&
                        pp.ProductoId == productoId);

                if (relacion == null)
                    return NotFound(
                        "No existe una vinculación entre este producto y proveedor.");

                _context.ProductoProveedores.Remove(relacion);

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    mensaje = "Producto desvinculado correctamente del proveedor.",
                    productoId,
                    proveedorId
                });
            });
        }
    }
}