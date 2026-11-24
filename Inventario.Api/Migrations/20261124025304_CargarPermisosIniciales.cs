using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Inventario.Api.Migrations
{
    /// <inheritdoc />
    public partial class CargarPermisosIniciales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Permisos",
                columns: new[] { "Id", "Activo", "Codigo", "Descripcion", "FechaCreacion", "Nombre" },
                values: new object[,]
                {
                    { 1, true, "PRODUCTOS_VER", "Permite consultar los productos del sistema.", new DateTime(2026, 11, 23, 0, 0, 0, 0, DateTimeKind.Utc), "Consultar productos" },
                    { 2, true, "PRODUCTOS_GESTIONAR", "Permite crear, actualizar y cambiar el estado de productos.", new DateTime(2026, 11, 23, 0, 0, 0, 0, DateTimeKind.Utc), "Gestionar productos" },
                    { 3, true, "CATEGORIAS_GESTIONAR", "Permite gestionar categorias y subcategorias.", new DateTime(2026, 11, 23, 0, 0, 0, 0, DateTimeKind.Utc), "Gestionar categorias" },
                    { 4, true, "UNIDADES_GESTIONAR", "Permite gestionar las unidades de medida.", new DateTime(2026, 11, 23, 0, 0, 0, 0, DateTimeKind.Utc), "Gestionar unidades de medida" },
                    { 5, true, "MARCAS_GESTIONAR", "Permite gestionar las marcas de los productos.", new DateTime(2026, 11, 23, 0, 0, 0, 0, DateTimeKind.Utc), "Gestionar marcas" },
                    { 6, true, "PROVEEDORES_GESTIONAR", "Permite gestionar proveedores y sus productos asociados.", new DateTime(2026, 11, 23, 0, 0, 0, 0, DateTimeKind.Utc), "Gestionar proveedores" },
                    { 7, true, "STOCK_GESTIONAR", "Permite gestionar el stock y sus movimientos.", new DateTime(2026, 11, 23, 0, 0, 0, 0, DateTimeKind.Utc), "Gestionar stock" },
                    { 8, true, "COMPRAS_GESTIONAR", "Permite registrar y gestionar compras.", new DateTime(2026, 11, 23, 0, 0, 0, 0, DateTimeKind.Utc), "Gestionar compras" },
                    { 9, true, "CLIENTES_GESTIONAR", "Permite gestionar los clientes del sistema.", new DateTime(2026, 11, 23, 0, 0, 0, 0, DateTimeKind.Utc), "Gestionar clientes" },
                    { 10, true, "VENTAS_GESTIONAR", "Permite registrar y gestionar ventas.", new DateTime(2026, 11, 23, 0, 0, 0, 0, DateTimeKind.Utc), "Gestionar ventas" },
                    { 11, true, "CAJA_GESTIONAR", "Permite abrir, cerrar y gestionar movimientos de caja.", new DateTime(2026, 11, 23, 0, 0, 0, 0, DateTimeKind.Utc), "Gestionar caja" },
                    { 12, true, "COBROS_REGISTRAR", "Permite registrar pagos asociados a ventas.", new DateTime(2026, 11, 23, 0, 0, 0, 0, DateTimeKind.Utc), "Registrar cobros" },
                    { 13, true, "METODOS_PAGO_GESTIONAR", "Permite administrar los metodos de pago disponibles.", new DateTime(2026, 11, 23, 0, 0, 0, 0, DateTimeKind.Utc), "Gestionar metodos de pago" },
                    { 14, true, "REPORTES_VER", "Permite consultar los reportes generales del sistema.", new DateTime(2026, 11, 23, 0, 0, 0, 0, DateTimeKind.Utc), "Consultar reportes" },
                    { 15, true, "USUARIOS_GESTIONAR", "Permite administrar los usuarios del sistema.", new DateTime(2026, 11, 23, 0, 0, 0, 0, DateTimeKind.Utc), "Gestionar usuarios" },
                    { 16, true, "ROLES_GESTIONAR", "Permite administrar roles y asignaciones de usuarios.", new DateTime(2026, 11, 23, 0, 0, 0, 0, DateTimeKind.Utc), "Gestionar roles" },
                    { 17, true, "PERMISOS_GESTIONAR", "Permite administrar las asignaciones de permisos a roles.", new DateTime(2026, 11, 23, 0, 0, 0, 0, DateTimeKind.Utc), "Gestionar permisos" },
                    { 18, true, "AUDITORIA_VER", "Permite consultar el historial de operaciones del sistema.", new DateTime(2026, 11, 23, 0, 0, 0, 0, DateTimeKind.Utc), "Consultar auditoria" },
                    { 19, true, "VENTAS_VER", "Permite consultar ventas sin modificarlas.", new DateTime(2026, 11, 23, 0, 0, 0, 0, DateTimeKind.Utc), "Consultar ventas" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Permisos",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Permisos",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Permisos",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Permisos",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Permisos",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Permisos",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "Permisos",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "Permisos",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "Permisos",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "Permisos",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "Permisos",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "Permisos",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "Permisos",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "Permisos",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "Permisos",
                keyColumn: "Id",
                keyValue: 15);

            migrationBuilder.DeleteData(
                table: "Permisos",
                keyColumn: "Id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                table: "Permisos",
                keyColumn: "Id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "Permisos",
                keyColumn: "Id",
                keyValue: 18);

            migrationBuilder.DeleteData(
                table: "Permisos",
                keyColumn: "Id",
                keyValue: 19);
        }
    }
}
