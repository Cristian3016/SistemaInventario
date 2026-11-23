
using System.Net.Mail;
using Inventario.Api.Data;
using Inventario.Api.DTOs;
using Inventario.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Api.Controllers
{
    [ApiController]
    [Route("api/clientes")]
    public class ClientesController : ControllerBase
    {
        private readonly InventarioDbContext _context;

        public ClientesController(InventarioDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> CrearCliente(
            [FromBody] CrearClienteDto datos)
        {
            if (datos == null)
                return BadRequest("Debes proporcionar los datos del cliente.");

            var nombre = datos.NombreRazonSocial?.Trim();
            var tipoDocumento = datos.TipoDocumento?.Trim().ToUpperInvariant();
            var numeroDocumento = datos.NumeroDocumento?.Trim();
            var email = datos.Email?.Trim();
            var telefono = datos.Telefono?.Trim();
            var direccion = datos.Direccion?.Trim();

            if (string.IsNullOrWhiteSpace(nombre))
                return BadRequest("El nombre o razón social es obligatorio.");

            if (nombre.Length > 200)
                return BadRequest("El nombre o razón social no puede superar los 200 caracteres.");

            if (string.IsNullOrWhiteSpace(tipoDocumento))
                tipoDocumento = null;

            if (string.IsNullOrWhiteSpace(numeroDocumento))
                numeroDocumento = null;

            if ((tipoDocumento == null) != (numeroDocumento == null))
                return BadRequest("Debes proporcionar el tipo y el número de documento juntos.");

            if (tipoDocumento != null)
            {
                if (tipoDocumento != "DNI" && tipoDocumento != "CUIT")
                    return BadRequest("El tipo de documento debe ser DNI o CUIT.");

                if (numeroDocumento!.Any(c => c < '0' || c > '9'))
                    return BadRequest("El documento debe contener solamente números.");

                if (tipoDocumento == "DNI")
                {
                    if (numeroDocumento.Length < 7 || numeroDocumento.Length > 8)
                        return BadRequest("El DNI debe contener 7 u 8 dígitos.");
                }

                if (tipoDocumento == "CUIT")
                {
                    if (numeroDocumento.Length != 11)
                        return BadRequest("El CUIT debe contener exactamente 11 dígitos.");

                    if (!EsCuitValido(numeroDocumento))
                        return BadRequest("El CUIT tiene un dígito verificador inválido.");
                }

                var documentoExistente = await _context.Clientes
                    .AnyAsync(c =>
                        c.TipoDocumento == tipoDocumento &&
                        c.NumeroDocumento == numeroDocumento);

                if (documentoExistente)
                    return Conflict("Ya existe un cliente registrado con ese documento.");
            }

            if (!string.IsNullOrWhiteSpace(email))
            {
                if (email.Length > 254)
                    return BadRequest("El correo electrónico no puede superar los 254 caracteres.");

                if (!MailAddress.TryCreate(email, out var direccionEmail) ||
                    !direccionEmail.Host.Contains('.'))
                    return BadRequest("El correo electrónico ingresado no tiene un formato válido.");
            }

            if (telefono?.Length > 30)
                return BadRequest("El teléfono no puede superar los 30 caracteres.");

            if (direccion?.Length > 250)
                return BadRequest("La dirección no puede superar los 250 caracteres.");

            var cliente = new Cliente
            {
                NombreRazonSocial = nombre,
                TipoDocumento = tipoDocumento,
                NumeroDocumento = numeroDocumento,
                Email = string.IsNullOrWhiteSpace(email) ? null : email,
                Telefono = string.IsNullOrWhiteSpace(telefono) ? null : telefono,
                Direccion = string.IsNullOrWhiteSpace(direccion) ? null : direccion,
                Activo = true,
                FechaCreacion = DateTime.UtcNow
            };

            _context.Clientes.Add(cliente);

            await _context.SaveChangesAsync();

            return StatusCode(201, new
            {
                mensaje = "Cliente registrado correctamente.",
                cliente.Id,
                cliente.NombreRazonSocial,
                cliente.TipoDocumento,
                cliente.NumeroDocumento,
                cliente.Email,
                cliente.Telefono,
                cliente.Direccion,
                cliente.Activo,
                cliente.FechaCreacion
            });
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> ObtenerClientePorId(int id)
        {
            if (id <= 0)
                return BadRequest("El ID del cliente debe ser mayor que cero.");

            var cliente = await _context.Clientes
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id);

            if (cliente == null)
                return NotFound("Cliente no encontrado.");

            return Ok(cliente);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> ActualizarCliente(
            int id,
            [FromBody] CrearClienteDto datos)
        {
            if (id <= 0)
                return BadRequest("El ID del cliente debe ser mayor que cero.");

            if (datos == null)
                return BadRequest("Debes proporcionar los datos del cliente.");

            var cliente = await _context.Clientes.FindAsync(id);

            if (cliente == null)
                return NotFound("Cliente no encontrado.");

            if (!cliente.Activo)
                return Conflict("No se puede modificar un cliente inactivo. Primero debes activarlo.");

            var nombre = datos.NombreRazonSocial?.Trim();
            var tipoDocumento = datos.TipoDocumento?.Trim().ToUpperInvariant();
            var numeroDocumento = datos.NumeroDocumento?.Trim();
            var email = datos.Email?.Trim();
            var telefono = datos.Telefono?.Trim();
            var direccion = datos.Direccion?.Trim();

            if (string.IsNullOrWhiteSpace(nombre))
                return BadRequest("El nombre o razón social es obligatorio.");

            if (nombre.Length > 200)
                return BadRequest("El nombre o razón social no puede superar los 200 caracteres.");

            if (string.IsNullOrWhiteSpace(tipoDocumento))
                tipoDocumento = null;

            if (string.IsNullOrWhiteSpace(numeroDocumento))
                numeroDocumento = null;

            if ((tipoDocumento == null) != (numeroDocumento == null))
                return BadRequest("Debes proporcionar el tipo y el número de documento juntos.");

            if (tipoDocumento != null)
            {
                if (tipoDocumento != "DNI" && tipoDocumento != "CUIT")
                    return BadRequest("El tipo de documento debe ser DNI o CUIT.");

                if (numeroDocumento!.Any(c => c < '0' || c > '9'))
                    return BadRequest("El documento debe contener solamente números.");

                if (tipoDocumento == "DNI")
                {
                    if (numeroDocumento.Length < 7 || numeroDocumento.Length > 8)
                        return BadRequest("El DNI debe contener 7 u 8 dígitos.");
                }

                if (tipoDocumento == "CUIT")
                {
                    if (numeroDocumento.Length != 11)
                        return BadRequest("El CUIT debe contener exactamente 11 dígitos.");

                    if (!EsCuitValido(numeroDocumento))
                        return BadRequest("El CUIT tiene un dígito verificador inválido.");
                }

                var documentoExistente = await _context.Clientes
                    .AnyAsync(c =>
                        c.TipoDocumento == tipoDocumento &&
                        c.NumeroDocumento == numeroDocumento &&
                        c.Id != id);

                if (documentoExistente)
                    return Conflict("Ya existe otro cliente registrado con ese documento.");
            }

            if (!string.IsNullOrWhiteSpace(email))
            {
                if (email.Length > 254)
                    return BadRequest("El correo electrónico no puede superar los 254 caracteres.");

                if (!MailAddress.TryCreate(email, out var direccionEmail) ||
                    !direccionEmail.Host.Contains('.'))
                    return BadRequest("El correo electrónico ingresado no tiene un formato válido.");
            }

            if (telefono?.Length > 30)
                return BadRequest("El teléfono no puede superar los 30 caracteres.");

            if (direccion?.Length > 250)
                return BadRequest("La dirección no puede superar los 250 caracteres.");

            cliente.NombreRazonSocial = nombre;
            cliente.TipoDocumento = tipoDocumento;
            cliente.NumeroDocumento = numeroDocumento;
            cliente.Email = string.IsNullOrWhiteSpace(email) ? null : email;
            cliente.Telefono = string.IsNullOrWhiteSpace(telefono) ? null : telefono;
            cliente.Direccion = string.IsNullOrWhiteSpace(direccion) ? null : direccion;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                mensaje = "Cliente actualizado correctamente.",
                cliente.Id,
                cliente.NombreRazonSocial,
                cliente.TipoDocumento,
                cliente.NumeroDocumento,
                cliente.Email,
                cliente.Telefono,
                cliente.Direccion,
                cliente.Activo,
                cliente.FechaCreacion
            });
        }

        [HttpPatch("{id:int}/desactivar")]
        public async Task<IActionResult> DesactivarCliente(int id)
        {
            if (id <= 0)
                return BadRequest("El ID del cliente debe ser mayor que cero.");

            var filasActualizadas = await _context.Clientes
                .Where(c => c.Id == id && c.Activo)
                .ExecuteUpdateAsync(cambios =>
                    cambios.SetProperty(c => c.Activo, false));

            if (filasActualizadas == 0)
            {
                var cliente = await _context.Clientes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (cliente == null)
                    return NotFound("Cliente no encontrado.");

                return Conflict("El cliente ya está desactivado.");
            }

            return Ok(new
            {
                mensaje = "Cliente desactivado correctamente.",
                clienteId = id,
                activo = false
            });
        }

        [HttpPatch("{id:int}/activar")]
        public async Task<IActionResult> ActivarCliente(int id)
        {
            if (id <= 0)
                return BadRequest("El ID del cliente debe ser mayor que cero.");

            var filasActualizadas = await _context.Clientes
                .Where(c => c.Id == id && !c.Activo)
                .ExecuteUpdateAsync(cambios =>
                    cambios.SetProperty(c => c.Activo, true));

            if (filasActualizadas == 0)
            {
                var cliente = await _context.Clientes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (cliente == null)
                    return NotFound("Cliente no encontrado.");

                return Conflict("El cliente ya está activo.");
            }

            return Ok(new
            {
                mensaje = "Cliente activado correctamente.",
                clienteId = id,
                activo = true
            });
        }


        private static bool EsCuitValido(string cuit)
        {
            if (cuit.Length != 11 ||
                cuit.Any(c => c < '0' || c > '9'))
                return false;

            int[] factores = { 5, 4, 3, 2, 7, 6, 5, 4, 3, 2 };

            int suma = 0;

            for (int i = 0; i < factores.Length; i++)
            {
                suma += (cuit[i] - '0') * factores[i];
            }

            int verificador = 11 - (suma % 11);

            if (verificador == 11)
                verificador = 0;
            else if (verificador == 10)
                verificador = 9;

            return verificador == cuit[10] - '0';
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerClientes(
            [FromQuery] bool? activo,
            [FromQuery] string? buscar)
        {
            var consulta = _context.Clientes.AsNoTracking();

            if (activo.HasValue)
            {
                consulta = consulta.Where(c => c.Activo == activo.Value);
            }

            if (!string.IsNullOrWhiteSpace(buscar))
            {
                var termino = buscar.Trim();

                consulta = consulta.Where(c =>
                    c.NombreRazonSocial.Contains(termino) ||
                    (c.NumeroDocumento != null &&
                     c.NumeroDocumento.Contains(termino)));
            }

            var clientes = await consulta
                .OrderBy(c => c.NombreRazonSocial)
                .ThenBy(c => c.Id)
                .ToListAsync();

            return Ok(clientes);
        }

    }
}

