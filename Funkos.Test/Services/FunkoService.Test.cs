using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Funkos.Dto;
using Funkos.Dto.Funko;
using Funkos.Mappers;
using Funkos.Models;
using Funkos.RealTime.Notificaciones;
using Funkos.Repositories;
using Funkos.Service;
using Funkos.Service.Storage;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Microsoft.Extensions.Logging;

namespace Funkos.Test.Services;

[TestFixture]
public class FunkoServiceTest
{
    private Mock<IFunkoRepository> _mockFunkoRepository = null!;
    private Mock<IValidator<FunkoCreateDto>> _mockValidatorCreate = null!;
    private Mock<IValidator<FunkoUpdateDto>> _mockValidatorUpdate = null!;
    private Mock<IValidator<FunkoPatchDto>> _mockValidatorUpdatePatch = null!;
    private Mock<ILogger<FunkoService>> _mockLogger = null!;
    private Mock<IStorageService> _mockStorage = null!;
    private Mock<IHubContext<NotificacionesHub>> _mockHubContext = null!;
    private Mock<IHubClients> _mockHubClients = null!;
    private Mock<IClientProxy> _mockClientProxy = null!;
    
    private FunkoService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _mockFunkoRepository = new Mock<IFunkoRepository>();
        _mockValidatorCreate = new Mock<IValidator<FunkoCreateDto>>();
        _mockValidatorUpdate = new Mock<IValidator<FunkoUpdateDto>>();
        _mockValidatorUpdatePatch = new Mock<IValidator<FunkoPatchDto>>();
        _mockLogger = new Mock<ILogger<FunkoService>>();
        _mockStorage = new Mock<IStorageService>();
        _mockHubContext = new Mock<IHubContext<NotificacionesHub>>();
        _mockHubClients = new Mock<IHubClients>();
        _mockClientProxy = new Mock<IClientProxy>();

        _mockHubContext.Setup(h => h.Clients).Returns(_mockHubClients.Object);
        _mockHubClients.Setup(c => c.All).Returns(_mockClientProxy.Object);
        _mockHubClients.Setup(c => c.Group(It.IsAny<string>())).Returns(_mockClientProxy.Object);

        _service = new FunkoService(
            _mockFunkoRepository.Object,
            _mockValidatorCreate.Object,
            _mockValidatorUpdate.Object,
            _mockValidatorUpdatePatch.Object,
            _mockLogger.Object,
            _mockStorage.Object,
            _mockHubContext.Object
        );
    }

    [Test]
    public async Task FindAllPagedAsync_Devuelve_SuccessConPagedResultDeFunkos()
    {
        // Arrange
        var filtro = new FunkoFilterDto { Page = 0, Size = 10 };
        var listaFunkos = new List<Funko>
        {
            new Funko { Id = 1, Name = "Funko 1", Precio = 10.00m, Stock = 5, CategoriaId = "CAT12345678", ProveedorId = "prov12345678901234567890" }
        };

        _mockFunkoRepository
            .Setup(r => r.FindAllPagedAsync(filtro, It.IsAny<CancellationToken>()))
            .ReturnsAsync((listaFunkos, 1));

        // Act
        var resultado = await _service.FindAllPagedAsync(filtro, CancellationToken.None);

        // Assert
        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.TotalCount.Should().Be(1);
        resultado.Value.Items.Should().HaveCount(1);
    }

    [Test]
    public async Task GetByIdAsync_Devuelve_SuccessConFunkoDtoSiExiste()
    {
        // Arrange
        var funko = new Funko
        {
            Id = 1,
            Name = "Batman Test",
            Precio = 15.00m,
            Stock = 5,
            CategoriaId = "ABC12345678",
            ProveedorId = "prov12345678901234567890",
            Categoria = new Categoria { Name = "DC", Descripcion = "Test" }
        };

        _mockFunkoRepository
            .Setup(r => r.FindByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(funko);

        // Act
        var resultado = await _service.GetByIdAsync(1, CancellationToken.None);

        // Assert
        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Name.Should().Be("Batman Test");
    }

    [Test]
    public async Task GetByIdAsync_Devuelve_FailureSiFunkoNoExiste()
    {
        // Arrange
        _mockFunkoRepository
            .Setup(r => r.FindByIdAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Funko?)null);

        // Act
        var resultado = await _service.GetByIdAsync(99, CancellationToken.None);

        // Assert
        resultado.IsFailure.Should().BeTrue();
    }

    [Test]
    public async Task AddAsync_Devuelve_FailureSiValidacionFalla()
    {
        // Arrange
        var dto = new FunkoCreateDto
        {
            Name = "Funko Test",
            Precio = 10.00m,
            CategoriaId = "ABC12345678",
            ProveedorId = "prov12345678901234567890",
            Stock = 5
        };

        _mockValidatorCreate
            .Setup(v => v.ValidateAsync(dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(new[] { new ValidationFailure("Name", "El nombre es obligatorio") }));

        // Act
        var resultado = await _service.AddAsync(dto, CancellationToken.None);

        // Assert
        resultado.IsFailure.Should().BeTrue();
    }

    [Test]
    public async Task AddAsync_Devuelve_FailureSiYaExisteFunkoConMismoNombre()
    {
        // Arrange
        var dto = new FunkoCreateDto
        {
            Name = "Duplicado",
            Precio = 12.00m,
            CategoriaId = "ABC12345678",
            ProveedorId = "prov12345678901234567890",
            Stock = 2
        };

        _mockValidatorCreate
            .Setup(v => v.ValidateAsync(dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _mockFunkoRepository
            .Setup(r => r.ExistsByNameAsync("Duplicado", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var resultado = await _service.AddAsync(dto, CancellationToken.None);

        // Assert
        resultado.IsFailure.Should().BeTrue();
    }

    [Test]
    public async Task AddAsync_Devuelve_SuccessConFunkoCreadoYPorDefectoImagen()
    {
        // Arrange
        var dto = new FunkoCreateDto
        {
            Name = "Nuevo Funko",
            Precio = 20.00m,
            CategoriaId = "ABC12345678",
            ProveedorId = "prov12345678901234567890",
            Stock = 10
        };

        _mockValidatorCreate
            .Setup(v => v.ValidateAsync(dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _mockFunkoRepository
            .Setup(r => r.ExistsByNameAsync(dto.Name, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Devolvemos una nueva instancia con el ID simulado (evitando mutar el parámetro de entrada 'f')
        _mockFunkoRepository
            .Setup(r => r.CreateAsync(It.IsAny<Funko>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Funko f, CancellationToken _) => new Funko
            {
                Id = 1, // Simulamos el ID autoincremental generado por la base de datos
                Name = f.Name,
                Precio = f.Precio,
                CategoriaId = f.CategoriaId,
                ProveedorId = f.ProveedorId,
                Stock = f.Stock,
                Imagen = f.Imagen,
                CreateAt = f.CreateAt
            });

        // Act
        var resultado = await _service.AddAsync(dto, CancellationToken.None);

        // Assert
        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Name.Should().Be("Nuevo Funko");
        resultado.Value.image.Should().Be("/uploads/funkos/default.png");
    }

    [Test]
    public async Task DeleteAsync_Devuelve_SuccessAlEliminarCorrectamente()
    {
        // Arrange
        var funkoExistente = new Funko
        {
            Id = 1,
            Name = "Funko a borrar",
            Precio = 15.00m,
            Stock = 1,
            CategoriaId = "ABC12345678",
            ProveedorId = "prov12345678901234567890",
            Imagen = "default.png"
        };

        _mockFunkoRepository
            .Setup(r => r.FindByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(funkoExistente);

        _mockFunkoRepository
            .Setup(r => r.DeleteAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var resultado = await _service.DeleteAsync(1, CancellationToken.None);

        // Assert
        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().BeTrue();
    }
    
    [Test]
    public async Task UpdatePartialAsync_Devuelve_SuccessAlActualizarParcialmenteCorrectamente()
    {
        // Arrange
        var funkoId = 1L;
        var dto = new FunkoPatchDto
        {
            Name = "Funko Actualizado Parcial",
            Precio = 25.00m,
            Stock = 8
        };

        var funkoExistente = new Funko
        {
            Id = funkoId,
            Name = "Funko Original",
            Precio = 15.00m,
            Stock = 5,
            CategoriaId = "ABC12345678",
            ProveedorId = "prov12345678901234567890",
            Imagen = "/uploads/funkos/default.png"
        };
        
        _mockValidatorUpdatePatch
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<FunkoPatchDto>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        // Configuramos el repositorio para encontrar el Funko
        _mockFunkoRepository
            .Setup(r => r.FindByIdAsync(funkoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(funkoExistente);

        // Configuramos el repositorio para actualizar correctamente devolviendo la entidad actualizada
        _mockFunkoRepository
            .Setup(r => r.UpdateAsync(funkoId, It.IsAny<Funko>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long id, Funko f, CancellationToken _) => f);

        // Act (Asumiendo que inyectas el validador de patch en el servicio o usas el mock correspondiente)
        // Si tu servicio usa un campo separado para el patch, recuerda inicializarlo en el SetUp.
        var resultado = await _service.UpdatePartialAsync(funkoId, dto, CancellationToken.None);

        // Assert
        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Name.Should().Be("Funko Actualizado Parcial");
        resultado.Value.Precio.Should().Be(25.00m);
        resultado.Value.Stock.Should().Be(8);
        
        // Verificamos que se llamó al repositorio para buscar y actualizar
        _mockFunkoRepository.Verify(r => r.FindByIdAsync(funkoId, It.IsAny<CancellationToken>()), Times.Once);
        _mockFunkoRepository.Verify(r => r.UpdateAsync(funkoId, It.IsAny<Funko>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}