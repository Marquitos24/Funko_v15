using FluentAssertions;
using Funkos.Errors;
using Microsoft.AspNetCore.Mvc;

namespace Funkos.Test.Errors;

[TestFixture]
public class DomainErrorsExtensionTest
{
    [Test]
    public void NotFoundError_Reotna_404_FormId_ErrorIdentificador()
    {
        //Arange
        var error = NotFoundError.FromId(5, "Funko");
        
        // Act
        var result = DomainErrorsExtension.ToHttpResult<object>(error);
        
        // Assert
        var actionResult = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        actionResult.StatusCode.Should().Be(404);
        actionResult.Value.Should().BeEquivalentTo(new { message = "Funko con ID 5 no encontrado" });
    }
    
    [Test]
    public void NotFoundError_Reotna_404_ErrorListaVacia()
    {
        //Arange
        var error = NotFoundError.ListaVacia("Funkos");
        
        // Act
        var result = DomainErrorsExtension.ToHttpResult<object>(error);
        
        // Assert
        var actionResult = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        actionResult.StatusCode.Should().Be(404);
        actionResult.Value.Should().BeEquivalentTo(new { message = "No se han encontrado Funkos" });
    }
    

    
    [Test]
    public void ValidationError_Retorna_400_ErrorCreate()
    {
        //Arange
        var error = ValidationError.Create("Los datos proporcionados no son válidos.");

        // Act
        var result = DomainErrorsExtension.ToHttpResult<object>(error);

        // Assert
        var actionResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        actionResult.StatusCode.Should().Be(400);
        actionResult.Value.Should().BeEquivalentTo(new { message = "Los datos proporcionados no son válidos." });

    }
    
    [Test]
    public void ValidationError_Retorna_400_ErrorWithFieldErrors()
    {
        //Arange
        var fieldErrors = new Dictionary<string, string[]>
        {
            { "Nombre", new[] { "El nombre es obligatorio.", "El nombre debe tener al menos 3 caracteres." } },
            { "Precio", new[] { "El precio debe ser mayor que cero." } }
        };

        var error = ValidationError.WithFieldErrors(fieldErrors);

        // Act
        var result = DomainErrorsExtension.ToHttpResult<object>(error);

        // Assert
        // 1. Verificamos que sea un BadRequestObjectResult (código HTTP 400)
        var actionResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        actionResult.StatusCode.Should().Be(400);

        // 2. Verificamos que el objeto anónimo devuelto contenga el mensaje y los errores correctos
        actionResult.Value.Should().BeEquivalentTo(new 
        { 
            message = "Errores de validación", 
            errors = fieldErrors 
        });
    }
    
    [Test]
    public void ConflictError_Retorna_409_ErrorDuplicacion()
    {
        //Arange
        var error = ConflictError.Duplicate("Funko", "Carlos");

        // Act
        var result = DomainErrorsExtension.ToHttpResult<object>(error);

        // Assert
        var actionResult = result.Result.Should().BeOfType<ConflictObjectResult>().Subject;
        actionResult.StatusCode.Should().Be(409);
        actionResult.Value.Should().BeEquivalentTo(new { message = "Ya existe un Funko con el nombre 'Carlos'" });

    }
    
    [Test]
    public void ForbiddenError_Retorna_403_ErrorAutenticacion()
    {
        //Arange
        var error = new ForbiddenError("Contraseña no valida");

        // Act
        var result = DomainErrorsExtension.ToHttpResult<object>(error);

        // Assert
        var actionResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        actionResult.StatusCode.Should().Be(403);
        actionResult.Value.Should().BeEquivalentTo(new { message = "Contraseña no valida" });
    }
    
    [Test]
    public void BusinessRuleError_Retorna_422_ErrorReglasNegocio()
    {
        //Arange
        var error = new BusinessRuleError("Error de negocio");

        // Act
        var result = DomainErrorsExtension.ToHttpResult<object>(error);

        // Assert
        var actionResult = result.Result.Should().BeOfType<UnprocessableEntityObjectResult>().Subject;
        actionResult.StatusCode.Should().Be(422);
        actionResult.Value.Should().BeEquivalentTo(new { message = "Error de negocio" });
    }
    
    [Test]
    public void InternalError_Retorna_500_ErrorServidor()
    {
        //Arange
        var error = new InternalError("");

        // Act
        var result = DomainErrorsExtension.ToHttpResult<object>(error);

        // Assert
        var actionResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        actionResult.StatusCode.Should().Be(500);
        actionResult.Value.Should().BeEquivalentTo(new { message = "Error interno del servidor" });
    }
}