namespace FunctionLoadQueue.Core.Entities
{
    public class ProcessedFile
    {
        public Guid Id { get; set; }
        public string NombreOriginal { get; set; } = string.Empty;
        public string NombreBlob { get; set; } = string.Empty;
        public string UrlBlob { get; set; } = string.Empty;
        public long TamanioBytes { get; set; }
        public string? Descripcion { get; set; }
        public DateTimeOffset FechaCargaAPI { get; set; }
        public DateTimeOffset? FechaProcesadoFn { get; set; }
        public string Estado { get; set; } = "En Cola";
    }
}
