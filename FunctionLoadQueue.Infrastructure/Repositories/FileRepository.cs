using FunctionLoadQueue.Core.Entities;
using FunctionLoadQueue.Core.Interfaces;
using FunctionLoadQueue.Infrastructure.Data;

namespace FunctionLoadQueue.Infrastructure.Repositories
{
    public class FileRepository : IFileRepository
    {
        private readonly ApplicationDbContext _context;

        public FileRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ProcessedFile?> GetByIdAsync(Guid id)
        {
            return await _context.ArchivosProcesados.FindAsync(id);
        }

        public async Task SaveInvoicesAndMarkAsProcessedAsync(Guid archivoId, List<Factura> facturas)
        {
            // 1. Buscar el registro del archivo cargado originalmente por la API
            var archivo = await _context.ArchivosProcesados.FindAsync(archivoId);

            if (archivo == null)
            {
                throw new KeyNotFoundException($"No se encontró ningún registro de archivo con el ID {archivoId} en la base de datos.");
            }

            // 2. Actualizar el estado del ciclo de vida y las marcas de tiempo
            archivo.Estado = "Procesado";
            archivo.FechaProcesadoFn = DateTimeOffset.UtcNow; // Captura hora local/UTC exacta del procesamiento

            // 3. Vincular y agregar la lista de facturas extraídas del CSV
            facturas.ForEach(async f => {
                f.ArchivoProcesadoId = archivoId;
                await _context.Facturas.AddAsync(f);
            });

            // 4. Guardar todos los cambios en una sola transacción en SQL Server
            await _context.SaveChangesAsync();
        }
    }
}
