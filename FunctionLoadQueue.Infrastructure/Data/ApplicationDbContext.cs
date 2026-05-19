using FunctionLoadQueue.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace FunctionLoadQueue.Infrastructure.Data
{
    public class ApplicationDbContext: DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<ProcessedFile> ArchivosProcesados => Set<ProcessedFile>();
        public DbSet<Factura> Facturas => Set<Factura>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Mapeo de la tabla ArchivosProcesados (creada por Node.js)
            modelBuilder.Entity<ProcessedFile>(entity =>
            {
                entity.ToTable("ArchivosProcesados");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasConversion(v => v.ToString(), v => Guid.Parse(v)).HasColumnType("varchar(36)");
                entity.Property(e => e.NombreOriginal).HasMaxLength(255).IsRequired();
                entity.Property(e => e.NombreBlob).HasMaxLength(255).IsRequired();
                entity.Property(e => e.UrlBlob).HasMaxLength(2048).IsRequired();
                entity.Property(e => e.TamanioBytes).HasColumnType("bigint");
                entity.Property(e => e.Descripcion).HasMaxLength(500);
                entity.Property(e => e.FechaCargaAPI).HasColumnType("datetimeoffset");
                entity.Property(e => e.FechaProcesadoFn).HasColumnType("datetimeoffset");
                entity.Property(e => e.Estado).HasMaxLength(50).IsRequired();
            });

            // Mapeo de la nueva tabla Facturas
            modelBuilder.Entity<Factura>(entity =>
            {
                entity.ToTable("Facturas");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasDefaultValueSql("NEWID()").HasColumnType("uniqueidentifier");
                entity.Property(e => e.ArchivoProcesadoId).HasConversion(v => v.ToString(), v => Guid.Parse(v));
                entity.Property(e => e.NumeroFactura).HasMaxLength(50).IsRequired();
                entity.Property(e => e.NitCliente).HasMaxLength(20).IsRequired();
                entity.Property(e => e.NombreCliente).HasMaxLength(150).IsRequired();
                entity.Property(e => e.ValorTotal).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Iva).HasColumnType("decimal(18,2)");
                entity.Property(e => e.FechaEmision).HasColumnType("datetime");
                entity.Property(e => e.FechaCreacion).HasColumnType("datetimeoffset");

                // Configuración de la relación 1 a Muchos (Un archivo tiene muchas facturas)
                entity.HasOne(d => d.ArchivoProcesado)
                      .WithMany()
                      .HasForeignKey(d => d.ArchivoProcesadoId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
