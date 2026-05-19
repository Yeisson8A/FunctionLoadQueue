using FunctionLoadQueue.Core.Interfaces;
using FunctionLoadQueue.Infrastructure.Data;
using FunctionLoadQueue.Infrastructure.Repositories;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // Obtener la cadena de conexión de las configuraciones
        string? connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");

        // Inyectar el DbContext apuntando a SQL Server
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString));

        // Inyectar el Repositorio de Datos
        services.AddScoped<IFileRepository, FileRepository>();
    })
    .Build();

// Bloque de inicialización automática de tablas
using (var scope = host.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        await context.Database.EnsureCreatedAsync();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error al inicializar las tablas: {ex.Message}");
    }
}

host.Run();
