
using Inventario.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Api.Data
{
    public class InventarioDbContext : DbContext
    {
        public InventarioDbContext(DbContextOptions<InventarioDbContext> options)
            : base(options)
        {
        }

        public DbSet<Producto> Productos { get; set; }

        public DbSet<Categoria> Categorias { get; set; }

        public DbSet<MovimientoStock> MovimientosStock { get; set; }

        public DbSet<UnidadMedida> UnidadesMedida { get; set; }

        public DbSet<Marca> Marcas { get; set; }

        public DbSet<Proveedor> Proveedores { get; set; }

        public DbSet<ProductoProveedor> ProductoProveedores { get; set; }

        public DbSet<Compra> Compras { get; set; }

        public DbSet<DetalleCompra> DetallesCompra { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Producto>()
                .Property(p => p.Precio)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Producto>()
                .Property(p => p.Stock)
                .HasPrecision(18, 3);

            modelBuilder.Entity<Producto>()
                .Property(p => p.StockMinimo)
                .HasPrecision(18, 3);

            modelBuilder.Entity<MovimientoStock>()
                .Property(m => m.Cantidad)
                .HasPrecision(18, 3);

            modelBuilder.Entity<Producto>()
                .Property(p => p.Activo)
                .HasDefaultValue(true);

            modelBuilder.Entity<Producto>()
                .Property(p => p.CodigoSku)
                .HasMaxLength(50);

            modelBuilder.Entity<Producto>()
                .HasIndex(p => p.CodigoSku)
                .IsUnique()
                .HasFilter("[CodigoSku] IS NOT NULL");

            modelBuilder.Entity<Producto>()
                .HasOne(p => p.Categoria)
                .WithMany(c => c.Productos)
                .HasForeignKey(p => p.CategoriaId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Producto>()
                .HasOne(p => p.UnidadMedida)
                .WithMany()
                .HasForeignKey(p => p.UnidadMedidaId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MovimientoStock>()
                .HasOne(m => m.Producto)
                .WithMany()
                .HasForeignKey(m => m.ProductoId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Categoria>()
                .HasOne(c => c.CategoriaPadre)
                .WithMany(c => c.Subcategorias)
                .HasForeignKey(c => c.CategoriaPadreId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Producto>()
                .HasOne(p => p.Marca)
                .WithMany()
                .HasForeignKey(p => p.MarcaId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Proveedor>(entity =>
            {
                entity.HasKey(p => p.Id);

                entity.Property(p => p.RazonSocial)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(p => p.NombreFantasia)
                    .HasMaxLength(200);

                entity.Property(p => p.Cuit)
                    .HasMaxLength(11);

                entity.Property(p => p.Email)
                    .HasMaxLength(254);

                entity.Property(p => p.Telefono)
                    .HasMaxLength(30);

                entity.Property(p => p.Direccion)
                    .HasMaxLength(250);

                entity.Property(p => p.Contacto)
                    .HasMaxLength(150);

                entity.Property(p => p.Activo)
                    .HasDefaultValue(true);

                entity.HasIndex(p => p.Cuit)
                    .IsUnique()
                    .HasFilter("[Cuit] IS NOT NULL");
            });

            modelBuilder.Entity<ProductoProveedor>(entity =>
            {
                entity.HasKey(pp => pp.Id);

                entity.HasOne(pp => pp.Producto)
                    .WithMany()
                    .HasForeignKey(pp => pp.ProductoId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(pp => pp.Proveedor)
                    .WithMany()
                    .HasForeignKey(pp => pp.ProveedorId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(pp => new { pp.ProductoId, pp.ProveedorId })
                    .IsUnique();
            });

            modelBuilder.Entity<Compra>(entity =>
            {
                entity.HasKey(c => c.Id);

                entity.Property(c => c.NumeroComprobante)
                    .HasMaxLength(100);

                entity.Property(c => c.Estado)
                    .IsRequired()
                    .HasMaxLength(20);

                entity.Property(c => c.Total)
                    .HasPrecision(18, 2);

                entity.HasOne(c => c.Proveedor)
                    .WithMany()
                    .HasForeignKey(c => c.ProveedorId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<DetalleCompra>(entity =>
            {
                entity.HasKey(d => d.Id);

                entity.Property(d => d.Cantidad)
                    .HasPrecision(18, 3);

                entity.Property(d => d.PrecioUnitario)
                    .HasPrecision(18, 2);

                entity.Property(d => d.Subtotal)
                    .HasPrecision(18, 2);

                entity.HasOne(d => d.Compra)
                    .WithMany()
                    .HasForeignKey(d => d.CompraId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(d => d.Producto)
                    .WithMany()
                    .HasForeignKey(d => d.ProductoId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(d => new { d.CompraId, d.ProductoId })
                    .IsUnique();
            });

        }
    }
}
