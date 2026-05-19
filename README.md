# **Worker Carga de Facturas (.NET + Floci-AZ)**

Servicio en segundo plano (*Background Service / Hosted Service*) desarrollado en **.NET 8** utilizando **Clean Architecture**. Su propósito principal es procesar de forma asíncrona colas de mensajería para la descarga, lectura y persistencia masiva de facturas comerciales a partir de archivos CSV almacenados en el storage local.
Este componente forma parte de un ecosistema híbrido, actuando como el procesador de tareas pesadas en segundo plano que consume los eventos encolados por la API principal de Node.js.

## **Arquitectura del Proyecto**

El Worker está estructurado bajo los principios de **Clean Architecture**, garantizando el desacoplamiento de la infraestructura y facilitando la mantenibilidad:

- **Core / Domain:** Contiene las entidades de negocio (`Factura`, `ArchivoProcesado`), las interfaces de repositorios y los DTOs de mensajería (`FileQueueMessage`).
- **Infrastructure:** Implementa el acceso a datos mediante **Entity Framework Core**, la configuración del `ApplicationDbContext`, los repositorios concretos y el manejo de conversiones de tipos compatibles con Node.js.
- **Function / Worker (Root):** Orquesta el ciclo de vida del servicio a través de `QueueWorkerService` (`BackgroundService`). Maneja la inyección de dependencias, la configuración global y los clientes del SDK de Azure.

## **Flujo de Procesamiento**

1. **Escucha Activa:** El Worker monitorea la cola `file-processing-queue` en el emulador local.
2. **Consumo de Mensajes:** Al recibir un mensaje, el SDK de Azure decodifica automáticamente el cuerpo en Base64 convirtiéndolo a texto plano (JSON).
3. **Descarga del Blob:** Utilizando la URL absoluta provista por el mensaje (`BlobUrl`), el servicio se conecta a **Floci-AZ**, valida la existencia del recurso y descarga el flujo de texto del CSV directamente a memoria.
4. **Procesamiento de Datos:** Mediante `CsvHelper`, parsea las filas del archivo mapeándolas a entidades de C# de manera eficiente y controlando formatos decimales y de fecha.
5. **Persistencia e Integridad:** En una única transacción a través del repositorio, guarda la colección de facturas en la base de datos de SQL Server y marca el archivo de control como procesado.
6. **Confirmación:** Elimina el mensaje de la cola de manera segura tras completar el flujo exitosamente.

## **Tecnologías y Paquetes Clave**

- **Microsoft.EntityFrameworkCore.SqlServer:** ORM para la persistencia de datos en SQL Server.
- **Azure.Storage.Queues & Azure.Storage.Blobs:** SDK oficial de Azure para interactuar con los servicios de almacenamiento.
- **CsvHelper:** Librería de alto rendimiento para la lectura y mapeo de archivos delimitados por comas.
- **Microsoft.Extensions.Azure:** Integración nativa del SDK de Azure con el contenedor de inversión de control de .NET.

## **Configuración del Entorno (`appsettings.json`)**

El servicio requiere acceso al motor de bases de datos relacional y al emulador de Azure Storage. Asegúrate de configurar correctamente el archivo de configuración de entorno:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "ConnectionStrings": {
    "SqlConnectionString": "Server=localhost;Database=TuBaseDeDatos;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "AzureStorage": {
    "ConnectionString": "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://localhost:4577/devstoreaccount1;QueueEndpoint=http://localhost:4577/devstoreaccount1-queue;"
  }
}
