using Azure.Storage.Blobs;
using CsvHelper;
using CsvHelper.Configuration;
using FunctionLoadQueue.Core.DTOs;
using FunctionLoadQueue.Core.Entities;
using FunctionLoadQueue.Core.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace FunctionLoadQueue.Presentation
{
    public class InvoiceProcessorFunction
    {
        private readonly ILogger<InvoiceProcessorFunction> _logger;
        private readonly IFileRepository _fileRepository;

        public InvoiceProcessorFunction(ILogger<InvoiceProcessorFunction> logger, IFileRepository fileRepository)
        {
            _logger = logger;
            _fileRepository = fileRepository;
        }

        [Function("InvoiceProcessorFunction")]
        public async Task Run(
        [QueueTrigger("file-processing-queue", Connection = "AzureWebJobsStorage")] string queueMessage)
        {
            _logger.LogInformation("Mensaje recibido desde Floci-AZ Queue.");

            try
            {
                // 1. Deserializar el payload JSON enviado por la API de Node.js
                var fileData = JsonSerializer.Deserialize<FileQueueMessage>(queueMessage, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (fileData == null)
                {
                    _logger.LogError("El mensaje de la cola no pudo ser parseado al contrato FileQueueMessage.");
                    return;
                }

                _logger.LogInformation("Procesando archivo CSV: {FileName} (ID: {Id})", fileData.FileName, fileData.Id);

                // 2. Conectarse al Blob Storage de Floci-AZ para descargar el archivo físico
                string connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage")!;
                var blobServiceClient = new BlobServiceClient(connectionString);
                var containerClient = blobServiceClient.GetBlobContainerClient("uploads");
                var blobClient = containerClient.GetBlobClient(fileData.BlobName);

                if (!await blobClient.ExistsAsync())
                {
                    _logger.LogError("El blob {BlobName} no existe en el contenedor de Floci-AZ.", fileData.BlobName);
                    return;
                }

                // 3. Descargar el archivo CSV a memoria
                var downloadResult = await blobClient.DownloadStreamingAsync();
                using var reader = new StreamReader(downloadResult.Value.Content, Encoding.UTF8);

                // Configurar CsvHelper para leer el archivo (asumiendo cabeceras estándar)
                var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true,
                    MissingFieldFound = null, // Ignorar campos faltantes para evitar caídas
                    HeaderValidated = null
                };

                using var csv = new CsvReader(reader, csvConfig);

                // 4. Mapear dinámicamente las filas del CSV a entidades Factura de C#
                var facturas = new List<Factura>();
                await csv.ReadAsync();
                csv.ReadHeader();

                while (await csv.ReadAsync())
                {
                    var factura = new Factura
                    {
                        Id = Guid.NewGuid(),
                        ArchivoProcesadoId = fileData.Id,
                        NumeroFactura = csv.GetField<string>("NumeroFactura") ?? string.Empty,
                        NitCliente = csv.GetField<string>("NitCliente") ?? string.Empty,
                        NombreCliente = csv.GetField<string>("NombreCliente") ?? string.Empty,
                        ValorTotal = csv.GetField<decimal>("ValorTotal"),
                        Iva = csv.GetField<decimal>("Iva"),
                        FechaEmision = csv.GetField<DateTime>("FechaEmision"),
                        FechaCreacion = DateTimeOffset.UtcNow
                    };
                    facturas.Add(factura);
                }

                _logger.LogInformation("Se extrajeron con éxito {Count} facturas del archivo CSV.", facturas.Count);

                // 5. Persistir de forma transaccional en SQL Server usando el repositorio
                await _fileRepository.SaveInvoicesAndMarkAsProcessedAsync(fileData.Id, facturas);

                _logger.LogInformation("Procesamiento completado. Estado del archivo actualizado a 'Procesado' en la BD.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error crítico al procesar las facturas del archivo de la cola.");
                throw; // Re-lanzar permite que la función reintente o mande el mensaje a la queue de "poison" si falla repetidamente
            }
        }
    }
}
