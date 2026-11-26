using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventario.Api.Migrations
{
    /// <inheritdoc />
    public partial class AgregarSesionesYRefreshTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SesionesUsuarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UltimaActividad = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaExpiracion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaRevocacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MotivoRevocacion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DireccionIp = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SesionesUsuarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SesionesUsuarios_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SesionUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaExpiracion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaUtilizacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FechaRevocacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MotivoRevocacion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReemplazadoPorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_RefreshTokens_ReemplazadoPorId",
                        column: x => x.ReemplazadoPorId,
                        principalTable: "RefreshTokens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_SesionesUsuarios_SesionUsuarioId",
                        column: x => x.SesionUsuarioId,
                        principalTable: "SesionesUsuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_FechaExpiracion",
                table: "RefreshTokens",
                column: "FechaExpiracion");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_ReemplazadoPorId",
                table: "RefreshTokens",
                column: "ReemplazadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_SesionUsuarioId",
                table: "RefreshTokens",
                column: "SesionUsuarioId",
                unique: true,
                filter: "[FechaUtilizacion] IS NULL AND [FechaRevocacion] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TokenHash",
                table: "RefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SesionesUsuarios_FechaExpiracion",
                table: "SesionesUsuarios",
                column: "FechaExpiracion");

            migrationBuilder.CreateIndex(
                name: "IX_SesionesUsuarios_UltimaActividad",
                table: "SesionesUsuarios",
                column: "UltimaActividad");

            migrationBuilder.CreateIndex(
                name: "IX_SesionesUsuarios_UsuarioId",
                table: "SesionesUsuarios",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_SesionesUsuarios_UsuarioId_FechaRevocacion",
                table: "SesionesUsuarios",
                columns: new[] { "UsuarioId", "FechaRevocacion" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "SesionesUsuarios");
        }
    }
}
