namespace FunctionLoadQueue.Core.Entities
{
    public class Factura
    {
        public Guid Id { get; set; }

        // Relación con el archivo CSV de origen
        public Guid ArchivoProcesadoId { get; set; }
        public ProcessedFile? ArchivoProcesado { get; set; }

        // Datos propios de la factura
        public string NumeroFactura { get; set; } = string.Empty;
        public string NitCliente { get; set; } = string.Empty;
        public string NombreCliente { get; set; } = string.Empty;
        public decimal ValorTotal { get; set; }
        public decimal Iva { get; set; }
        public DateTime FechaEmision { get; set; }

        // Metadatos de auditoría interna de la función
        public DateTimeOffset FechaCreacion { get; set; } = DateTimeOffset.UtcNow;
    }
}
