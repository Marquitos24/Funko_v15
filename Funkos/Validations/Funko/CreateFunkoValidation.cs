using FluentValidation;
using Funkos.Dto.Funko;
using Funkos.idGenerators;
using Funkos.Repositories;
using Funkos.Repositories.Categoria;
using Funkos.Repositories.Proveedor;
using IOPath = System.IO.Path;

namespace Funkos.Validations;

public class CreateFunkoValidationAsync : AbstractValidator<FunkoCreateDto>
{
    private readonly string[] _allowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];
    private readonly long _maxFileSize = 5 * 1024 * 1024; // 5 MB
    
    public CreateFunkoValidationAsync(ICategoriaRepository categoriaRepository,
        IProveedorRepository proveedorRepository, IFunkoRepository funkoRepository)
    {
        ClassLevelCascadeMode = CascadeMode.Stop;
        
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("El nombre es obligatorio")
            .MaximumLength(100)
            .WithMessage("El nombre no puede superar los 100 caracteres")
            .MustAsync(async (name, cancellation) =>
                !await funkoRepository.ExistsByNameAsync(name, null, cancellation))
            .WithMessage("El nombre del funko ya existe");

        RuleFor(x => x.Precio)
            .GreaterThanOrEqualTo(1m)
            .WithMessage("El precio debe ser como mínimo 1 €")
            .LessThan(10001)
            .WithMessage("El precio no puede superar 10000 €");

        RuleFor(x => x.Stock)
            .GreaterThanOrEqualTo(0)
            .WithMessage("El stock no puede ser negativo");

        RuleFor(x => x.CategoriaId)
            .MaximumLength(IdYoutube.Longitud)
            .Matches(IdYoutube.PatronTexto)
            .WithMessage(IdYoutube.MensajeFormato)
            .MustAsync(async (id, cancellation) =>
                await categoriaRepository.ExistsAsync(id, cancellation))
            .WithMessage("La categoría no existe");

        RuleFor(x => x.ProveedorId)
            .MustAsync(async (id, cancellation) =>
                await proveedorRepository.ExistsAsync(id, cancellation))
            .WithMessage("El proveedor no existe");
        
        //IMAGEN
        When(x => x.Image != null, () =>
        {
            RuleFor(x => x.Image!.Length)
                .LessThanOrEqualTo(_maxFileSize)
                .WithMessage($"El tamaño del archivo no puede superar los {_maxFileSize / 1024 / 1024} MB");

            RuleFor(x => x.Image!.FileName)
                .Must(fileName => 
                {
                    var ext = IOPath.GetExtension(fileName).ToLowerInvariant();
                    return Array.Exists(_allowedExtensions, e => e == ext);
                })
                .WithMessage($"Formato de archivo no válido. Extensiones permitidas: {string.Join(", ", _allowedExtensions)}");
        });
    }
}