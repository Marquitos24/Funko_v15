using Funkos.Models;
using Funkos.Repositories;
using NUnit.Framework;
using FluentAssertions;
using Funkos.DbContext;
using Funkos.Dto.Funko;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Funkos.Test.Repository;

[TestFixture]
public class FunkoRepositoryTest : IDisposable
{
    private PostgreSqlContainer _postgresContainer = null!;
    private string _connectionString = null!;
    private AppDbContext _context = null!;
    private FunkoRepository _repository = null!;
    private string _categoriaIdValida = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("funkos_repo_test_db")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        await _postgresContainer.StartAsync();
        _connectionString = _postgresContainer.GetConnectionString();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _postgresContainer.DisposeAsync();
    }

    [SetUp]
    public async Task SetUp()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_connectionString)
            .Options;

        _context = new AppDbContext(options);
        await _context.Database.EnsureDeletedAsync();
        await _context.Database.EnsureCreatedAsync();

        _repository = new FunkoRepository(_context);

        var categoria = new Categoria
        {
            Name = "Coleccionables Base",
            Descripcion = "Categoría estándar para pruebas"
        };
        _context.Categorias.Add(categoria);
        await _context.SaveChangesAsync();
        _categoriaIdValida = categoria.IdY;
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DisposeAsync();
    }

    [Test]
    public async Task FindAllAsync_Devuelve_ListaDeFunkos()
    {
        // Arrange
        var funko = new Funko
        {
            Name = "Iron Man Test",
            Precio = 15.00m,
            Stock = 10,
            CategoriaId = _categoriaIdValida,
            ProveedorId = "prov12345678901234567890"
        };
        _context.Funkos.Add(funko);
        await _context.SaveChangesAsync();

        // Act
        var resultado = await _repository.FindAllAsync();

        // Assert
        resultado.Should().NotBeNull();
        resultado.Should().HaveCount(1);
        resultado[0].Name.Should().Be("Iron Man Test");
        resultado[0].Categoria.Should().NotBeNull();
    }

    [Test]
    public async Task FindAllPagedAsync_Devuelve_FunkosPaginadosYTotalCount()
    {
        // Arrange
        for (int i = 1; i <= 5; i++)
        {
            _context.Funkos.Add(new Funko
            {
                Name = $"Funko {i}",
                Precio = 10.00m * i,
                Stock = i,
                CategoriaId = _categoriaIdValida,
                ProveedorId = "prov12345678901234567890"
            });
        }
        await _context.SaveChangesAsync();

        var filtro = new FunkoFilterDto
        {
            Page = 0,
            Size = 2,
            SortBy = "precio",
            Direction = "asc"
        };

        // Act
        var (items, totalCount) = await _repository.FindAllPagedAsync(filtro);

        // Assert
        totalCount.Should().Be(5);
        items.Should().HaveCount(2);
        items[0].Name.Should().Be("Funko 1");
        items[1].Name.Should().Be("Funko 2");
    }

    [Test]
    public async Task FindByIdAsync_Devuelve_FunkoSiExiste()
    {
        // Arrange
        var funko = new Funko
        {
            Name = "Spider-Man Test",
            Precio = 20.00m,
            Stock = 5,
            CategoriaId = _categoriaIdValida,
            ProveedorId = "prov12345678901234567890"
        };
        _context.Funkos.Add(funko);
        await _context.SaveChangesAsync();

        // Act
        var resultado = await _repository.FindByIdAsync(funko.Id);

        // Assert
        resultado.Should().NotBeNull();
        resultado!.Name.Should().Be("Spider-Man Test");
    }

    [Test]
    public async Task FindByIdAsync_Devuelve_NuloSiNoExiste()
    {
        // Act
        var resultado = await _repository.FindByIdAsync(99999L);

        // Assert
        resultado.Should().BeNull();
    }

    [Test]
    public async Task CreateAsync_Devuelve_FunkoCreadoYValido()
    {
        // Arrange
        var nuevoFunko = new Funko
        {
            Name = "Batman New",
            Precio = 18.50m,
            Stock = 8,
            CategoriaId = _categoriaIdValida,
            ProveedorId = "prov12345678901234567890",
            Imagen = "batman.png"
        };

        // Act
        var resultado = await _repository.CreateAsync(nuevoFunko);

        // Assert
        resultado.Should().NotBeNull();
        resultado.Id.Should().BeGreaterThan(0);
        resultado.Name.Should().Be("Batman New");
        resultado.CreateAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Test]
    public async Task UpdateAsync_Devuelve_FunkoModificado()
    {
        // Arrange
        var funko = new Funko
        {
            Name = "Thor Old",
            Precio = 12.00m,
            Stock = 3,
            CategoriaId = _categoriaIdValida,
            ProveedorId = "prov12345678901234567890"
        };
        _context.Funkos.Add(funko);
        await _context.SaveChangesAsync();

        var datosActualizados = new Funko
        {
            Name = "Thor Updated",
            Precio = 25.00m,
            Stock = 7,
            CategoriaId = _categoriaIdValida,
            ProveedorId = "prov12345678901234567890",
            Imagen = "thor_new.png"
        };

        // Act
        var resultado = await _repository.UpdateAsync(funko.Id, datosActualizados);

        // Assert
        resultado.Should().NotBeNull();
        resultado!.Name.Should().Be("Thor Updated");
        resultado.Precio.Should().Be(25.00m);
        resultado.Stock.Should().Be(7);
        resultado.Version.Should().Be(2);
        resultado.UpdateAt.Should().NotBeNull();
    }

    [Test]
    public async Task UpdateAsync_Devuelve_NuloSiFunkoNoExiste()
    {
        // Arrange
        var datosActualizados = new Funko
        {
            Name = "Inexistente",
            Precio = 10.00m,
            Stock = 1,
            CategoriaId = _categoriaIdValida,
            ProveedorId = "prov12345678901234567890"
        };

        // Act
        var resultado = await _repository.UpdateAsync(9999L, datosActualizados);

        // Assert
        resultado.Should().BeNull();
    }

    [Test]
    public async Task DeleteAsync_Devuelve_TrueYMarcaBorradoLogico()
    {
        // Arrange
        var funko = new Funko
        {
            Name = "Hulk Borrable",
            Precio = 14.00m,
            Stock = 4,
            CategoriaId = _categoriaIdValida,
            ProveedorId = "prov12345678901234567890"
        };
        _context.Funkos.Add(funko);
        await _context.SaveChangesAsync();

        // Act
        var exito = await _repository.DeleteAsync(funko.Id);

        // Assert
        exito.Should().BeTrue();

        var busqueda = await _repository.FindByIdAsync(funko.Id);
        busqueda.Should().BeNull();
    }

    [Test]
    public async Task DeleteAsync_Devuelve_FalseSiFunkoNoExiste()
    {
        // Act
        var exito = await _repository.DeleteAsync(8888L);

        // Assert
        exito.Should().BeFalse();
    }

    [Test]
    public async Task ExistsByNameAsync_Devuelve_TrueSiNombreExiste()
    {
        // Arrange
        var funko = new Funko
        {
            Name = "Unique Funko",
            Precio = 10.00m,
            Stock = 2,
            CategoriaId = _categoriaIdValida,
            ProveedorId = "prov12345678901234567890"
        };
        _context.Funkos.Add(funko);
        await _context.SaveChangesAsync();

        // Act
        var existe = await _repository.ExistsByNameAsync("Unique Funko");

        // Assert
        existe.Should().BeTrue();
    }

    [Test]
    public async Task ExistsByNameAsync_Devuelve_FalseSiNombreNoExiste()
    {
        // Act
        var existe = await _repository.ExistsByNameAsync("Nombre Inexistente");

        // Assert
        existe.Should().BeFalse();
    }

    [Test]
    public async Task ExistsFunkosCAsync_Devuelve_TrueSiExistenFunkosConCategoria()
    {
        // Arrange
        var funko = new Funko
        {
            Name = "Funko Categoria",
            Precio = 10.00m,
            Stock = 2,
            CategoriaId = _categoriaIdValida,
            ProveedorId = "prov12345678901234567890"
        };
        _context.Funkos.Add(funko);
        await _context.SaveChangesAsync();

        // Act
        var existe = await _repository.ExistsFunkosCAsync(_categoriaIdValida);

        // Assert
        existe.Should().BeTrue();
    }

    [Test]
    public async Task ExistsFunkosPAsync_Devuelve_ConteoCorrectoDeProveedor()
    {
        // Arrange
        string proveedorId = "prov12345678901234567890";
        _context.Funkos.Add(new Funko
        {
            Name = "Funko Prov 1",
            Precio = 10.00m,
            Stock = 1,
            CategoriaId = _categoriaIdValida,
            ProveedorId = proveedorId,
            IsDeleted = false
        });
        _context.Funkos.Add(new Funko
        {
            Name = "Funko Prov 2",
            Precio = 12.00m,
            Stock = 1,
            CategoriaId = _categoriaIdValida,
            ProveedorId = proveedorId,
            IsDeleted = true 
        });
        await _context.SaveChangesAsync();

        // Act
        var conteo = await _repository.ExistsFunkosPAsync(proveedorId);

        // Assert
        conteo.Should().Be(1);
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }
}