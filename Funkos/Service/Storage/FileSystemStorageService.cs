using CSharpFunctionalExtensions;
using Funkos.Errors;
using Funkos.Exceptions;
using Funkos.Infrastructure.Storage;
using Microsoft.Extensions.Options;
using IOPath = System.IO.Path;
namespace Funkos.Service.Storage;

/// <summary>
/// Servicio de almacenamiento de archivos 
/// </summary>
public class FileSystemStorageService(IWebHostEnvironment env,
    IOptions<StorageSettings> settings,
    ILogger<FileSystemStorageService> logger) : IStorageService
{
    // Ruta absoluta y segura calculada basándonos en el entorno de ejecución y la configuración
    private readonly string _rootPath = IOPath.GetFullPath(IOPath.Combine(
            env.ContentRootPath ?? AppContext.BaseDirectory,
            settings.Value.RootPath));

    /// <summary>
    /// Inicializa la estructura de directorios necesaria en el disco al arrancar la App.
    /// </summary>
    public Task InitAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            // Definir todas las carpetas base que deben existir obligatoriamente
            var directories = new[]
            {
                _rootPath,
                IOPath.Combine(_rootPath, settings.Value.ImagesFolder),
                IOPath.Combine(_rootPath, settings.Value.DocumentsFolder),
                IOPath.Combine(_rootPath, "temp")
            };

            // Recorrer y crear cada directorio si no existe previamente
            foreach (var dir in directories)
            {
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                    logger.LogInformation("Directorio creado: {Path}", dir);
                }
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Almacena un archivo recibido desde una petición HTTP (IFormFile), aplicando validaciones de seguridad,
    /// renombrándolo de forma única y guardándolo en la carpeta.
    /// </summary>
    public async Task<Result<string, DomainErrors>> StoreAsync(IFormFile file, string? folder = null, CancellationToken cancellationToken = default)
    {
        // Validar que el archivo no sea nulo o esté vacío
        if (file is null || file.Length == 0) return Result.Failure<string, DomainErrors>(StorageError.BadRequest("El archivo es nulo o vacío"));

        // Validar que no supere el tamaño máximo permitido configurado
        if (file.Length > settings.Value.MaxFileSize) return Result.Failure<string, DomainErrors>(StorageError.BadRequest($"El archivo excede el tamaño máximo de {settings.Value.MaxFileSize / 1024 / 1024}MB"));

        // Validar la extensión física del archivo (.jpg, .png, etc.)
        var extension = IOPath.GetExtension(file.FileName).ToLowerInvariant();
        if (!settings.Value.AllowedExtensions.Contains(extension)) return Result.Failure<string, DomainErrors>(StorageError.BadRequest($"Tipo de archivo no permitido: {extension}"));

        // Validar el Content-Type declarado - tipo MIME ==> Indica qué tipo de contenido tiene un archivo o dato
        if (!string.IsNullOrEmpty(file.ContentType) && !settings.Value.AllowedContentTypes.Contains(
                file.ContentType, StringComparer.OrdinalIgnoreCase))
            return Result.Failure<string, DomainErrors>(StorageError.BadRequest($"Tipo MIME no permitido: {file.ContentType}"));

        // Generar un nombre único para evitar colisiones y calcular las rutas de destino
        var fileName = GenerateFileName(file.FileName);
        
        string folderPath;
        try
        {
            folderPath = GetFolderPath(folder);
        }
        catch (InvalidFileTypeException ex)
        {
            return Result.Failure<string, DomainErrors>(StorageError.BadRequest(ex.Message));
        }
        var filePath = IOPath.Combine(folderPath, fileName);

        // Asegurar que la carpeta de destino exista físicamente
        Directory.CreateDirectory(folderPath);

        // Copiar el flujo de datos del archivo hacia el disco de forma asíncrona
        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream, cancellationToken);
        
        logger.LogInformation("Archivo guardado: {FileName} ({Size} bytes)", fileName, file.Length);
        
        return Result.Success<string, DomainErrors>(fileName);
    }

    /// <summary>
    /// Sobrecarga para almacenar un archivo directamente desde un Stream genérico aplicando las mismas reglas de validación.
    /// </summary>
    public async Task<Result<string, DomainErrors>> StoreAsync(Stream stream, string fileName, string? folder = null, CancellationToken cancellationToken = default)
    {
        // Verificar que el stream provisto sea válido
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        
        // Validar la extensión del archivo a partir de su nombre
        var extension = IOPath.GetExtension(fileName).ToLowerInvariant();
        if (!settings.Value.AllowedExtensions.Contains(extension)) return Result.Failure<string, DomainErrors>(StorageError.BadRequest($"Tipo de archivo no permitido: {extension}"));

        // Generar nombre único y calcular ruta en disco
        var fileNameGenerated = GenerateFileName(fileName);
        string folderPath;
        try
        {
            folderPath = GetFolderPath(folder);
        }
        catch (InvalidFileTypeException ex)
        {
            return Result.Failure<string, DomainErrors>(StorageError.BadRequest(ex.Message));
        }
        var filePath = IOPath.Combine(folderPath, fileNameGenerated);

        // Crear directorio si no existe y guardar el archivo mediante copia de streams
        Directory.CreateDirectory(folderPath);

        await using var fileStream = new FileStream(filePath, FileMode.Create);
        await stream.CopyToAsync(fileStream, cancellationToken);
        
        return Result.Success<string, DomainErrors>(fileNameGenerated);
    }

    /// <summary>
    /// Carga un archivo existente desde el disco y lo devuelve en formato de lectura (Stream).
    /// </summary>
    public Task<Result<Stream, DomainErrors>> LoadAsStreamAsync(string fileName, string? folder = null, CancellationToken cancellationToken = default)
    {
        // Obtener la ruta física completa del archivo
        var filePath = GetFilePath(fileName, folder);
        
        // Lanzar excepción si el archivo no se encuentra físicamente
        if (!File.Exists(filePath)) return Task.FromResult(Result.Failure<Stream, DomainErrors>(StorageError.BadRequest($"Archivo no encontrado: {fileName}")));
        // Abrir el archivo en modo lectura y devolverlo como Stream
        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        return Task.FromResult(Result.Success<Stream, DomainErrors>(stream));
    }

    /// <summary>
    /// Obtiene la ruta física absoluta de un archivo en el disco, saneando el nombre para evitar problemas de rutas.
    /// </summary>
    public string GetFilePath(string fileName, string? folder = null)
    {
        // Path.GetFileName elimina cualquier intento de inyección de rutas maliciosas en el nombre
        return IOPath.Combine(GetFolderPath(folder), IOPath.GetFileName(fileName));
    }

    /// <summary>
    /// Construye y devuelve la URL pública relativa con la que los clientes web podrán acceder al archivo.
    /// </summary>
    public string GetUrl(string fileName, string? folder = null)
    {
        // Si hay carpeta, incluye la subcarpeta en la ruta web; si no, apunta directo a /uploads/
        var path = folder is not null ? $"/uploads/{folder}/{fileName}" : $"/uploads/{fileName}";
        
        // Normalizar barras invertidas (por compatibilidad con Windows) a barras normales de URL
        return path.Replace("\\", "/");
    }

    /// <summary>
    /// Comprueba si un archivo existe físicamente en el disco.
    /// </summary>
    public bool Exists(string fileName, string? folder = null)
    {
        var filePath = GetFilePath(fileName, folder);
        return File.Exists(filePath);
    }

    /// <summary>
    /// Elimina un archivo físico del disco del servidor si este existe.
    /// </summary>
    public async Task<bool> DeleteAsync(string fileName, string? folder = null, CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(fileName, folder);
        
        // Si el archivo no existe, no hace nada y devuelve falso
        if (!File.Exists(filePath)) return false;

        // Borrar el archivo del disco y registrar el evento en los logs
        File.Delete(filePath);
        logger.LogInformation("Archivo eliminado: {FileName}", fileName);
        
        return true;
    }

    /// <summary>
    /// Devuelve una colección con los nombres de todos los archivos contenidos dentro de una carpeta específica.
    /// </summary>
    public Task<IEnumerable<string>> ListFilesAsync(string? folder = null)
    {
        var folderPath = GetFolderPath(folder);
        
        // Si la carpeta no existe, devuelve una lista vacía
        if (!Directory.Exists(folderPath)) return Task.FromResult<IEnumerable<string>>(Array.Empty<string>());

        // Obtener todos los archivos de la ruta y extraer solo sus nombres
        var files = Directory.GetFiles(folderPath)
            .Select(IOPath.GetFileName)!;
        
        return Task.FromResult(files);
    }

    /// <summary>
    /// Método auxiliar privado para calcular y validar la ruta de la subcarpeta, previniendo ataques de Path Traversal.
    /// </summary>
    private string GetFolderPath(string? folder)
    {
        if (string.IsNullOrEmpty(folder)) return _rootPath;

        var full = IOPath.GetFullPath(IOPath.Combine(_rootPath, folder));
        
        if (!full.Equals(_rootPath, StringComparison.OrdinalIgnoreCase) &&
            !full.StartsWith(_rootPath + IOPath.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidFileTypeException($"Carpeta no permitida: {folder}");
        
        return full;
    }

    /// <summary>
    /// Genera un nombre de archivo único combinando un timestamp UTC actual y un GUID aleatorio truncado.
    /// </summary>
    private static string GenerateFileName(string originalFileName)
    {
        // Extraer la extensión original del archivo
        var extension = IOPath.GetExtension(originalFileName);
        
        // Generar un identificador único aleatorio de 16 caracteres
        var uniqueName = Guid.NewGuid().ToString("N")[..16];
        
        // Obtener la fecha y hora actual en formato compacto (ej: 20260607193000)
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        
        // Retornar nombre final combinado (ej: 20260607193000_a1b2c3d4e5f6g7h8.jpg)
        return $"{timestamp}_{uniqueName}{extension}";
    }
}