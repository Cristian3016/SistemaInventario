
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

        public DbSet<Cliente> Clientes { get; set; }

        public DbSet<Venta> Ventas { get; set; }

        public DbSet<DetalleVenta> DetallesVenta { get; set; }

        public DbSet<MetodoPago> MetodosPago { get; set; }

        public DbSet<Caja> Cajas { get; set; }

        public DbSet<PagoVenta> PagosVenta { get; set; }

        public DbSet<MovimientoCaja> MovimientosCaja { get; set; }

        public DbSet<Usuario> Usuarios { get; set; }

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

            modelBuilder.Entity<Cliente>(entity =>
            {
                entity.HasKey(c => c.Id);

                entity.Property(c => c.NombreRazonSocial)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(c => c.TipoDocumento)
                    .HasMaxLength(10);

                entity.Property(c => c.NumeroDocumento)
                    .HasMaxLength(20);

                entity.Property(c => c.Email)
                    .HasMaxLength(254);

                entity.Property(c => c.Telefono)
                    .HasMaxLength(30);

                entity.Property(c => c.Direccion)
                    .HasMaxLength(250);

                entity.Property(c => c.Activo)
                    .HasDefaultValue(true);

                entity.HasIndex(c => new
                {
                    c.TipoDocumento,
                    c.NumeroDocumento
                })
                .IsUnique()
                .HasFilter("[TipoDocumento] IS NOT NULL AND [NumeroDocumento] IS NOT NULL");
            });

            modelBuilder.Entity<Venta>(entity =>
            {
                entity.HasKey(v => v.Id);

                entity.Property(v => v.NumeroComprobante)
                    .HasMaxLength(100);

                entity.Property(v => v.Estado)
                    .IsRequired()
                    .HasMaxLength(20);

                entity.Property(v => v.Total)
                    .HasPrecision(18, 2);

                entity.HasOne(v => v.Cliente)
                    .WithMany()
                    .HasForeignKey(v => v.ClienteId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<DetalleVenta>(entity =>
            {
                entity.HasKey(d => d.Id);

                entity.Property(d => d.Cantidad)
                    .HasPrecision(18, 3);

                entity.Property(d => d.PrecioUnitario)
                    .HasPrecision(18, 2);

                entity.Property(d => d.Subtotal)
                    .HasPrecision(18, 2);

                entity.HasOne(d => d.Venta)
                    .WithMany()
                    .HasForeignKey(d => d.VentaId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(d => d.Producto)
                    .WithMany()
                    .HasForeignKey(d => d.ProductoId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(d => new { d.VentaId, d.ProductoId })
                    .IsUnique();
            });

            modelBuilder.Entity<MetodoPago>(entity =>
            {
                entity.Property(m => m.Nombre)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(m => m.Descripcion)
                    .HasMaxLength(250);

                entity.HasIndex(m => m.Nombre)
                    .IsUnique();
            });

            modelBuilder.Entity<Caja>(entity =>
            {
                entity.Property(c => c.MontoInicial)
                    .HasPrecision(18, 2);

                entity.Property(c => c.MontoEsperado)
                    .HasPrecision(18, 2);

                entity.Property(c => c.MontoReal)
                    .HasPrecision(18, 2);

                entity.Property(c => c.Diferencia)
                    .HasPrecision(18, 2);

                entity.Property(c => c.Estado)
                    .IsRequired()
                    .HasMaxLength(20);

                entity.Property(c => c.Observaciones)
                    .HasMaxLength(500);

                entity.HasIndex(c => c.Estado)
                    .IsUnique()
                    .HasFilter("[Estado] = N'Abierta'");
            });

            modelBuilder.Entity<PagoVenta>(entity =>
            {
                entity.Property(p => p.Monto)
                    .HasPrecision(18, 2);

                entity.Property(p => p.ReferenciaOperacion)
                    .HasMaxLength(150);

                entity.HasOne(p => p.Venta)
                    .WithMany()
                    .HasForeignKey(p => p.VentaId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(p => p.MetodoPago)
                    .WithMany()
                    .HasForeignKey(p => p.MetodoPagoId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(p => p.Caja)
                    .WithMany()
                    .HasForeignKey(p => p.CajaId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<MovimientoCaja>(entity =>
            {
                entity.Property(m => m.Monto)
                    .HasPrecision(18, 2);

                entity.Property(m => m.Concepto)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(m => m.Observacion)
                    .HasMaxLength(500);

                entity.HasOne(m => m.Caja)
                    .WithMany()
                    .HasForeignKey(m => m.CajaId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Usuario>(entity =>
            {
                entity.Property(u => u.Nombre)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(u => u.Apellido)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(u => u.NombreUsuario)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(u => u.Email)
                    .IsRequired()
                    .HasMaxLength(254);

                entity.Property(u => u.PasswordHash)
                    .IsRequired()
                    .HasMaxLength(500);

                entity.Property(u => u.Activo)
                    .HasDefaultValue(true);

                entity.HasIndex(u => u.NombreUsuario)
                    .IsUnique();

                entity.HasIndex(u => u.Email)
                    .IsUnique();
            });
        }
    }
}
