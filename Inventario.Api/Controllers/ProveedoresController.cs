using Inventario.Api.DTOs;
using System.Net.Mail;
using Inventario.Api.Data;
using Inventario.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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



        [HttpPost]
        public async Task<ActionResult<Proveedor>> CrearProveedor(
            [FromBody] Proveedor nuevoProveedor)
        {
            var razonSocial = nuevoProveedor.RazonSocial?.Trim();

            if (string.IsNullOrWhiteSpace(razonSocial))
            {
                return BadRequest("La razón social es obligatoria.");
            }

            if (razonSocial.Length > 200)
            {
                return BadRequest(
                    "La razón social no puede superar los 200 caracteres.");
            }

            var nombreFantasia = nuevoProveedor.NombreFantasia?.Trim()
                ?? string.Empty;

            if (nombreFantasia.Length > 200)
            {
                return BadRequest(
                    "El nombre de fantasía no puede superar los 200 caracteres.");
            }

            var cuit = string.IsNullOrWhiteSpace(nuevoProveedor.Cuit)
                ? null
                : nuevoProveedor.Cuit.Trim();

            if (cuit != null)
            {
                if (cuit.Length != 11 ||
                    cuit.Any(c => c < '0' || c > '9'))
                {
                    return BadRequest(
                        "El CUIT debe contener exactamente 11 dígitos numéricos, sin guiones.");
                }

                if (!EsCuitValido(cuit))
                {
                    return BadRequest(
                        "El CUIT ingresado tiene un dígito verificador inválido.");
                }

                var existeCuit = await _context.Proveedores
                    .AnyAsync(p => p.Cuit == cuit);

                if (existeCuit)
                {
                    return Conflict(
                        "Ya existe un proveedor registrado con ese CUIT.");
                }
            }

            var email = nuevoProveedor.Email?.Trim();
            var telefono = nuevoProveedor.Telefono?.Trim();
            var direccion = nuevoProveedor.Direccion?.Trim();
            var contacto = nuevoProveedor.Contacto?.Trim();

            if (email?.Length > 254 ||
                telefono?.Length > 30 ||
                direccion?.Length > 250 ||
                contacto?.Length > 150)
            {
                return BadRequest(
                    "Uno o más campos superan la longitud máxima permitida.");
            }

            if (!EsEmailValido(email))
            {
                return BadRequest(
                    "El correo electrónico ingresado no tiene un formato válido.");
            }

            var proveedor = new Proveedor
            {
                RazonSocial = razonSocial,
                NombreFantasia = nombreFantasia,
                Cuit = cuit,
                Email = email,
                Telefono = telefono,
                Direccion = direccion,
                Contacto = contacto,
                Activo = true,
                FechaCreacion = DateTime.UtcNow
            };

            _context.Proveedores.Add(proveedor);

            await _context.SaveChangesAsync();

            return StatusCode(StatusCodes.Status201Created, proveedor);
        }

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

        [HttpPut("{id:int}")]
        public async Task<IActionResult> ActualizarProveedor(
            int id, [FromBody] Proveedor proveedorActualizado)
        {
            var proveedor = await _context.Proveedores.FindAsync(id);

            if (proveedor == null)
            {
                return NotFound("Proveedor no encontrado.");
            }

            if (!proveedor.Activo)
            {
                return Conflict(
                    "No se puede modificar un proveedor inactivo. Primero debes activarlo.");
            }

            var razonSocial = proveedorActualizado.RazonSocial?.Trim();

            if (string.IsNullOrWhiteSpace(razonSocial))
            {
                return BadRequest("La razón social es obligatoria.");
            }

            if (razonSocial.Length > 200)
            {
                return BadRequest(
                    "La razón social no puede superar los 200 caracteres.");
            }

            var nombreFantasia = proveedorActualizado.NombreFantasia?.Trim()
                ?? string.Empty;

            if (nombreFantasia.Length > 200)
            {
                return BadRequest(
                    "El nombre de fantasía no puede superar los 200 caracteres.");
            }

            var cuit = string.IsNullOrWhiteSpace(proveedorActualizado.Cuit)
                ? null
                : proveedorActualizado.Cuit.Trim();

            if (cuit != null)
            {
                if (cuit.Length != 11 ||
                    cuit.Any(c => c < '0' || c > '9'))
                {
                    return BadRequest(
                        "El CUIT debe contener exactamente 11 dígitos numéricos, sin guiones.");
                }

                if (!EsCuitValido(cuit))
                {
                    return BadRequest(
                        "El CUIT ingresado tiene un dígito verificador inválido.");
                }

                var existeCuit = await _context.Proveedores
                    .AnyAsync(p => p.Cuit == cuit && p.Id != id);

                if (existeCuit)
                {
                    return Conflict(
                        "Ya existe otro proveedor registrado con ese CUIT.");
                }
            }

            var email = proveedorActualizado.Email?.Trim();
            var telefono = proveedorActualizado.Telefono?.Trim();
            var direccion = proveedorActualizado.Direccion?.Trim();
            var contacto = proveedorActualizado.Contacto?.Trim();

            if (email?.Length > 254 ||
                telefono?.Length > 30 ||
                direccion?.Length > 250 ||
                contacto?.Length > 150)
            {
                return BadRequest(
                    "Uno o más campos superan la longitud máxima permitida.");
            }

            if (!EsEmailValido(email))
            {
                return BadRequest(
                    "El correo electrónico ingresado no tiene un formato válido.");
            }

            proveedor.RazonSocial = razonSocial;
            proveedor.NombreFantasia = nombreFantasia;
            proveedor.Cuit = cuit;
            proveedor.Email = email;
            proveedor.Telefono = telefono;
            proveedor.Direccion = direccion;
            proveedor.Contacto = contacto;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpPatch("{id:int}")]
        public async Task<IActionResult> ActualizarParcialProveedor(
            int id, [FromBody] ProveedorPatchDto datosPatch)
        {
            var proveedor = await _context.Proveedores.FindAsync(id);

            if (proveedor == null)
            {
                return NotFound("Proveedor no encontrado.");
            }

            if (!proveedor.Activo)
            {
                return Conflict(
                    "No se puede modificar un proveedor inactivo. Primero debes activarlo.");
            }

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

            var razonSocial = datosPatch.RazonSocial?.Trim();
            var nombreFantasia = datosPatch.NombreFantasia?.Trim();
            var cuit = datosPatch.Cuit?.Trim();
            var email = datosPatch.Email?.Trim();
            var telefono = datosPatch.Telefono?.Trim();
            var direccion = datosPatch.Direccion?.Trim();
            var contacto = datosPatch.Contacto?.Trim();

            if (datosPatch.RazonSocial != null &&
                string.IsNullOrWhiteSpace(razonSocial))
            {
                return BadRequest("La razón social es obligatoria.");
            }

            if (razonSocial?.Length > 200)
            {
                return BadRequest(
                    "La razón social no puede superar los 200 caracteres.");
            }

            if (nombreFantasia?.Length > 200)
            {
                return BadRequest(
                    "El nombre de fantasía no puede superar los 200 caracteres.");
            }

            if (cuit != null && cuit.Length > 0)
            {
                if (cuit.Length != 11 ||
                    cuit.Any(c => c < '0' || c > '9'))
                {
                    return BadRequest(
                        "El CUIT debe contener exactamente 11 dígitos numéricos, sin guiones.");
                }

                if (!EsCuitValido(cuit))
                {
                    return BadRequest(
                        "El CUIT ingresado tiene un dígito verificador inválido.");
                }

                var existeCuit = await _context.Proveedores
                    .AnyAsync(p => p.Cuit == cuit && p.Id != id);

                if (existeCuit)
                {
                    return Conflict(
                        "Ya existe otro proveedor registrado con ese CUIT.");
                }
            }

            if (email?.Length > 254 ||
                telefono?.Length > 30 ||
                direccion?.Length > 250 ||
                contacto?.Length > 150)
            {
                return BadRequest(
                    "Uno o más campos superan la longitud máxima permitida.");
            }

            if (email != null && !EsEmailValido(email))
            {
                return BadRequest(
                    "El correo electrónico ingresado no tiene un formato válido.");
            }

            if (datosPatch.RazonSocial != null)
            {
                proveedor.RazonSocial = razonSocial!;
            }

            if (datosPatch.NombreFantasia != null)
            {
                proveedor.NombreFantasia = nombreFantasia!;
            }

            if (datosPatch.Cuit != null)
            {
                proveedor.Cuit = string.IsNullOrWhiteSpace(cuit)
                    ? null
                    : cuit;
            }

            if (datosPatch.Email != null)
            {
                proveedor.Email = string.IsNullOrWhiteSpace(email)
                    ? null
                    : email;
            }

            if (datosPatch.Telefono != null)
            {
                proveedor.Telefono = telefono;
            }

            if (datosPatch.Direccion != null)
            {
                proveedor.Direccion = direccion;
            }

            if (datosPatch.Contacto != null)
            {
                proveedor.Contacto = contacto;
            }

            await _context.SaveChangesAsync();

            return Ok(proveedor);
        }


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
        [HttpPost("vincular-producto")]
        public async Task<IActionResult> VincularProductoProveedor(
            [FromBody] VincularProductoProveedorDto datos)
        {
            if (datos == null)
                return BadRequest("Debes proporcionar los datos de la vinculación.");

            if (datos.ProductoId <= 0 || datos.ProveedorId <= 0)
                return BadRequest("Los ID del producto y del proveedor deben ser mayores que cero.");

            var producto = await _context.Productos.FindAsync(datos.ProductoId);

            if (producto == null)
                return NotFound("Producto no encontrado.");

            if (!producto.Activo)
                return Conflict("No se puede vincular un producto inactivo.");

            var proveedor = await _context.Proveedores.FindAsync(datos.ProveedorId);

            if (proveedor == null)
                return NotFound("Proveedor no encontrado.");

            if (!proveedor.Activo)
                return Conflict("No se puede vincular un proveedor inactivo.");

            var existeRelacion = await _context.ProductoProveedores
                .AnyAsync(pp =>
                    pp.ProductoId == datos.ProductoId &&
                    pp.ProveedorId == datos.ProveedorId);

            if (existeRelacion)
                return Conflict("El producto ya está vinculado con este proveedor.");

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
        }

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

        [HttpDelete("{proveedorId:int}/productos/{productoId:int}")]
        public async Task<IActionResult> DesvincularProductoProveedor(
            int proveedorId, int productoId)
        {
            if (proveedorId <= 0 || productoId <= 0)
                return BadRequest("Los ID deben ser mayores que cero.");

            var relacion = await _context.ProductoProveedores
                .FirstOrDefaultAsync(pp =>
                    pp.ProveedorId == proveedorId &&
                    pp.ProductoId == productoId);

            if (relacion == null)
                return NotFound("No existe una vinculación entre este producto y proveedor.");

            _context.ProductoProveedores.Remove(relacion);

            await _context.SaveChangesAsync();

            return Ok(new
            {
                mensaje = "Producto desvinculado correctamente del proveedor.",
                productoId,
                proveedorId
            });
        }
    }
}