using Dsicode.ShoppingCart.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Dsicode.ShoppingCart.Api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<CartHeader> CartHeaders { get; set; }
        public DbSet<CartDetails> CartDetails { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ✅ IMPORTANTE: Configurar prefijos para tablas de carrito
            // Esto permite usar una sola base de datos para todos los microservicios
            modelBuilder.Entity<CartHeader>().ToTable("Cart_Headers");
            modelBuilder.Entity<CartDetails>().ToTable("Cart_Details");

            // ✅ Configuraciones para CartHeader
            modelBuilder.Entity<CartHeader>(entity =>
            {
                entity.HasKey(e => e.CartHeaderId);
                entity.Property(e => e.CartHeaderId).ValueGeneratedOnAdd();

                entity.Property(e => e.UserId)
                    .IsRequired()
                    .HasMaxLength(450); // ✅ Para compatibilidad con Identity

                entity.Property(e => e.CouponCode)
                    .HasMaxLength(50);

                // ✅ Discount y CartTotal están marcados como [NotMapped] en tu modelo
                // No los configuramos aquí porque no se guardan en la base de datos

                // ✅ Índices para mejorar rendimiento
                entity.HasIndex(e => e.UserId)
                    .HasDatabaseName("IX_Cart_Headers_UserId");

                entity.HasIndex(e => e.CouponCode)
                    .HasDatabaseName("IX_Cart_Headers_CouponCode");
            });

            // ✅ Configuraciones para CartDetails
            modelBuilder.Entity<CartDetails>(entity =>
            {
                entity.HasKey(e => e.CartDetailsId);
                entity.Property(e => e.CartDetailsId).ValueGeneratedOnAdd();

                entity.Property(e => e.CartHeaderId)
                    .IsRequired();

                entity.Property(e => e.ProductId)
                    .IsRequired();

                entity.Property(e => e.Count)
                    .IsRequired()
                    .HasDefaultValue(1);

                // ✅ ProductDto está marcado como [NotMapped] en tu modelo
                // No lo configuramos aquí porque no se guarda en la base de datos

                // ✅ Configurar relación con CartHeader
                entity.HasOne(e => e.CartHeader)
                    .WithMany() // ✅ Un CartHeader puede tener muchos CartDetails
                    .HasForeignKey(e => e.CartHeaderId)
                    .OnDelete(DeleteBehavior.Cascade); // ✅ Si se elimina el header, se eliminan los details

                // ✅ Índices para mejorar rendimiento
                entity.HasIndex(e => e.CartHeaderId)
                    .HasDatabaseName("IX_Cart_Details_CartHeaderId");

                entity.HasIndex(e => e.ProductId)
                    .HasDatabaseName("IX_Cart_Details_ProductId");

                // ✅ Índice compuesto para evitar duplicados de producto en el mismo carrito
                entity.HasIndex(e => new { e.CartHeaderId, e.ProductId })
                    .IsUnique()
                    .HasDatabaseName("IX_Cart_Details_CartHeaderId_ProductId");
            });
        }
    }
}