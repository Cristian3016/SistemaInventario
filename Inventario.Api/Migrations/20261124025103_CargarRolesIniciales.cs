using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Inventario.Api.Migrations
{
    /// <inheritdoc />
    public partial class CargarRolesIniciales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "Activo", "Descripcion", "FechaCreacion", "Nombre" },
                values: new object[,]
                {
                    { 1, true, "Acceso completo a todas las funcionalidades del sistema.", new DateTime(2026, 11, 23, 0, 0, 0, 0, DateTimeKind.Utc), "Administrador" },
                    { 2, true, "Gestion de ventas y clientes.", new DateTime(2026, 11, 23, 0, 0, 0, 0, DateTimeKind.Utc), "Vendedor" },
                    { 3, true, "Gestion de productos, stock, compras y proveedores.", new DateTime(2026, 11, 23, 0, 0, 0, 0, DateTimeKind.Utc), "Encargado de deposito" },
                    { 4, true, "Gestion de caja, cobros y pagos.", new DateTime(2026, 11, 23, 0, 0, 0, 0, DateTimeKind.Utc), "Cajero" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 4);
        }
    }
}
