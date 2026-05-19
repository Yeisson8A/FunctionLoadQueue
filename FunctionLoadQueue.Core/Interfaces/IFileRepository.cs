using FunctionLoadQueue.Core.Entities;

namespace FunctionLoadQueue.Core.Interfaces
{
    public interface IFileRepository
    {
        Task<ProcessedFile?> GetByIdAsync(Guid id);

        Task SaveInvoicesAndMarkAsProcessedAsync(Guid archivoId, List<Factura> facturas);
    }
}
