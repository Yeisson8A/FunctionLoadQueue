using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using FunctionLoadQueue.Core.Interfaces;
using FunctionLoadQueue.Function;
using FunctionLoadQueue.Infrastructure.Data;
using FunctionLoadQueue.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Azure;
using System.Net;

var builder = Host.CreateApplicationBuilder(args);
// Agregar referencia a la cadena de conexión de la base de datos
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    string? connectionString = builder.Configuration.GetConnectionString("SqlConnectionString") ?? string.Empty;
    options.UseSqlServer(connectionString);
});
// Registrar clientes de Azure (Floci-AZ)
string? azureStorageString = builder.Configuration["AzureStorage:ConnectionString"] ?? string.Empty;
builder.Services.AddSingleton(x => new BlobServiceClient(azureStorageString, new BlobClientOptions(BlobClientOptions.ServiceVersion.V2024_08_04)));
builder.Services.AddSingleton(x => new QueueServiceClient(azureStorageString, new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 }));

// Agregar dependencia de repositorio
builder.Services.AddScoped<IFileRepository, FileRepository>();
builder.Services.AddHostedService<QueueWorkerService>();

var host = builder.Build();
host.Run();
