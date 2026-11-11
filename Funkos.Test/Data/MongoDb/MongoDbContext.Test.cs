using FluentAssertions;
using Funkos.Models;
using Funkos.Mongo;
using MongoDB.Bson;
using MongoDB.Driver;
using Testcontainers.MongoDb;
using MongoClient = MongoDB.Driver.MongoClient;

namespace Funkos.Test.Data.MongoDb;

[TestFixture]
public class MongoDbContextTest
{
    private MongoDbContainer _mongoContainer = null!;
    private string _connectionString = null!;
    private MongoDbSettings _settings = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        // 1. Levantamos un contenedor real de MongoDB usando Testcontainers
        _mongoContainer = new MongoDbBuilder()
            .WithImage("mongo:7.0")
            .Build();

        await _mongoContainer.StartAsync();
        _connectionString = _mongoContainer.GetConnectionString();

        _settings = new MongoDbSettings
        {
            ConnectionString = _connectionString,
            DatabaseName = "funkos_mongo_test_db"
        };
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        // Destruimos el contenedor al finalizar todas las pruebas
        await _mongoContainer.DisposeAsync();
    }

    private MongoDbContext CreateContext()
    {
        var client = new MongoClient(_connectionString);
        return new MongoDbContext(client, _settings);
    }

    [Test]
    public async Task Debe_Crear_Proveedor_Y_Guardar_Correctamente()
    {
        // Arrange
        var context = CreateContext();
        
        // Limpiamos la colección antes de la prueba
        await context.Proveedores.DeleteManyAsync(MongoDB.Driver.Builders<Proveedor>.Filter.Empty);

        var proveedor = new Proveedor
        {
            Name = "Distribuciones Funko Global",
            Descripcion = "Proveedor oficial de figuras coleccionables.",
            CreateAt = DateTime.UtcNow,
            IsDeleted = false,
            Version = 1
        };

        // Act
        await context.Proveedores.InsertOneAsync(proveedor);

        // Assert
        proveedor.Id.Should().NotBe(ObjectId.Empty);

        var fetched = await context.Proveedores
            .Find(p => p.Id == proveedor.Id)
            .FirstOrDefaultAsync();

        fetched.Should().NotBeNull();
        fetched.Name.Should().Be("Distribuciones Funko Global");
        fetched.Descripcion.Should().Be("Proveedor oficial de figuras coleccionables.");
        fetched.IsDeleted.Should().BeFalse();
    }

    [Test]
    public async Task Debe_Respetar_Indice_Unico_En_El_Nombre_Del_Proveedor()
    {
        // Arrange
        var context = CreateContext();
        await context.Proveedores.DeleteManyAsync(Builders<Proveedor>.Filter.Empty);

        var proveedor1 = new Proveedor
        {
            Name = "Importaciones Marvel S.A.",
            Descripcion = "Primer proveedor",
            CreateAt = DateTime.UtcNow
        };

        var proveedor2 = new Proveedor
        {
            Name = "Importaciones Marvel S.A.", // Mismo nombre para forzar fallo de índice único
            Descripcion = "Segundo proveedor duplicado",
            CreateAt = DateTime.UtcNow
        };

        await context.Proveedores.InsertOneAsync(proveedor1);

        // Act
        Func<Task> act = async () => await context.Proveedores.InsertOneAsync(proveedor2);

        // Assert
        // MongoDB lanza MongoCommandException o MongoWriteException cuando se viola un índice único (código de error 11000)
        await act.Should().ThrowAsync<MongoWriteException>()
            .Where(ex => ex.WriteError.Category == ServerErrorCategory.DuplicateKey);
    }

    [Test]
    public async Task Debe_Permitir_Actualizacion_Y_Control_De_Borrado_Logico()
    {
        // Arrange
        var context = CreateContext();
        await context.Proveedores.DeleteManyAsync(Builders<Proveedor>.Filter.Empty);

        var proveedor = new Proveedor
        {
            Name = "Coleccionables Anime Shop",
            Descripcion = "Proveedor temporal",
            CreateAt = DateTime.UtcNow,
            IsDeleted = false
        };

        await context.Proveedores.InsertOneAsync(proveedor);

        // Act - Simulamos borrado lógico y actualización
        var updateDefinition = Builders<Proveedor>.Update
            .Set(p => p.IsDeleted, true)
            .Set(p => p.DeleteAt, DateTime.UtcNow)
            .Set(p => p.UpdateAt, DateTime.UtcNow)
            .Set(p => p.Version, proveedor.Version + 1);

        var result = await context.Proveedores
            .UpdateOneAsync(p => p.Id == proveedor.Id, updateDefinition);

        // Assert
        result.ModifiedCount.Should().Be(1);

        var updatedProveedor = await context.Proveedores
            .Find(p => p.Id == proveedor.Id)
            .FirstOrDefaultAsync();

        updatedProveedor.Should().NotBeNull();
        updatedProveedor!.IsDeleted.Should().BeTrue();
        updatedProveedor.DeleteAt.Should().NotBeNull();
        updatedProveedor.Version.Should().Be(2);
    }
}