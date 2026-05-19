using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using CsvHelper;
using CsvHelper.Configuration;
using FunctionLoadQueue.Core.DTOs;
using FunctionLoadQueue.Core.Entities;
using FunctionLoadQueue.Core.Interfaces;
using System.Globalization;
using System.Text.Json;

namespace FunctionLoadQueue.Function
{
    public class QueueWorkerService: BackgroundService
    {
        private readonly ILogger<QueueWorkerService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;
        private readonly QueueServiceClient _queueServiceClient;
        private const string QueueName = "file-processing-queue";

        public QueueWorkerService(ILogger<QueueWorkerService> logger, IConfiguration configuration, IServiceProvider serviceProvider, QueueServiceClient queueServiceClient)
        {
            _logger = logger;
            _configuration = configuration;
            _serviceProvider = serviceProvider;
            _queueServiceClient = queueServiceClient;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("El Worker Service de procesamiento ha iniciado de forma independiente.");

            // Inicializar los clientes SDK nativos
            var queueClient = _queueServiceClient.GetQueueClient(QueueName);

            // Asegurar que la cola exista en Floci-AZ
            await queueClient.CreateIfNotExistsAsync(cancellationToken: stoppingToken);

            // Bucle continuo controlado
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // 1. Consultar si hay un mensaje en la cola (Recuperamos de a 1)
                    var queueResponse = await queueClient.ReceiveMessagesAsync(maxMessages: 1, cancellationToken: stoppingToken);
                    var message = queueResponse.Value.FirstOrDefault();

                    if (message == null)
                    {
                        // Si no hay mensajes, el Worker "duerme" a voluntad el tiempo que decidas (ej. 10 segundos)
                        _logger.LogInformation("Cola vacía. Esperando próximos mensajes...");
                        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                        continue;
                    }

                    _logger.LogInformation("Mensaje encontrado en la cola. Iniciando procesamiento manual.");

                    // 2. OBTENER EL JSON DIRECTO (El SDK ya se encargó de decodificar el Base64)
                    string rawJson = message.MessageText;

                    _logger.LogInformation("JSON recibido de la cola: {RawJson}", rawJson);

                    var fileData = JsonSerializer.Deserialize<FileQueueMessage>(rawJson, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    _logger.LogInformation("Descargando CSV desde Floci-AZ: {BlobName}", fileData.BlobName);

                    // 3. Descargar el archivo CSV de forma segura
                    string connectionString = _configuration["AzureStorage:ConnectionString"]
                                            ?? throw new InvalidOperationException("La cadena de conexión de Azure Storage no está configurada.");

                    var blobOptions = new BlobClientOptions(BlobClientOptions.ServiceVersion.V2024_08_04);
                    // Instanciamos el servicio principal usando la Connection
                    var servicioClientTemporal = new BlobServiceClient(connectionString, blobOptions);

                    // Desarmamos la URL que envió Node.js de forma automática
                    var uriBuilder = new BlobUriBuilder(new Uri(fileData.BlobUrl));

                    // Obtenemos el container y el blob client
                    var containerClient = servicioClientTemporal.GetBlobContainerClient(uriBuilder.BlobContainerName);
                    var blobClient = containerClient.GetBlobClient(uriBuilder.BlobName);

                    if (!await blobClient.ExistsAsync(stoppingToken))
                    {
                        _logger.LogError("El archivo CSV especificado no existe en el storage.");
                        await queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt, stoppingToken);
                        continue;
                    }

                    // Descargar todo el contenido del blob directamente a memoria
                    var downloadResponse = await blobClient.DownloadContentAsync(stoppingToken);
                    string csvRawText = downloadResponse.Value.Content.ToString();

                    // Convertir el texto plano a un StringReader para CsvHelper
                    using var reader = new StringReader(csvRawText);

                    var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
                    {
                        HasHeaderRecord = true,
                        MissingFieldFound = null,
                        HeaderValidated = null
                    };

                    using var csv = new CsvReader(reader, csvConfig);
                    var facturas = new List<Factura>();
                    await csv.ReadAsync();
                    csv.ReadHeader();

                    while (await csv.ReadAsync())
                    {
                        facturas.Add(new Factura
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
                        });
                    }

                    // 4. Inyectar dinámicamente el repositorio (Scoped) dentro del Worker (Singleton)
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var fileRepository = scope.ServiceProvider.GetRequiredService<IFileRepository>();
                        await fileRepository.SaveInvoicesAndMarkAsProcessedAsync(fileData.Id, facturas);
                    }

                    _logger.LogInformation("Guardado exitoso. Procesadas {Count} facturas en SQL Server.", facturas.Count);

                    // 5. Eliminar el mensaje de la cola de Floci-AZ tras procesarlo con éxito
                    await queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt, stoppingToken);
                    _logger.LogInformation("Mensaje eliminado de la cola de forma segura.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error crítico procesando ciclo del Worker.");
                    // Esperar un momento antes de reintentar si la BD o Floci se caen
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
        }
    }
}
