using System.ComponentModel.DataAnnotations;
using Funkos.idGenerators;

namespace Funkos.GraphQL.Inputs;

public class CreateFunkoInput : IValidatableObject
{
    [Required (ErrorMessage =  "El nombre del funko es obligatorio")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "El nombre del Funko no puede superar los 100 caracteres y debe ser superior a 1")]
    public required string Name { get; set; } = string.Empty;
    
    [Required (ErrorMessage =  "El precio del funko es obligatorio")]
    [Range(1, 10000, ErrorMessage = "El precio debe ser mayor a 0 con limite de 10000 €")]
    public required decimal Precio  { get; set; } = 0;
 
    [IdYoutube]
    public required string CategoriaId { get; set; }
    
    [RegularExpression(@"^[0-9a-fA-F]{24}$", ErrorMessage =  "Formato invalido de ID")]
    public required string ProveedorId { get; set; }
    
    [Range(1, int.MaxValue, ErrorMessage = "Inserte un stock mayor que 0")]
    public required int Stock { get; set; }
    
    [MaxLength(500, ErrorMessage = "La URL de la imagen no puede exceder 500 caracteres")]
    [Url(ErrorMessage = "Debe ser una URL válida")]
    public IFile? Image { get; set; }
    
    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (Precio <= 0 && Stock > 0)
        {
            yield return new ValidationResult(
                "El precio debe ser mayor a 0 si hay stock",
                new[] { nameof(Precio) });
        }
    }
}


public class UpdateFunkoInput : IValidatableObject
{
    public long Id { get; set; }
    
    [StringLength(100, MinimumLength = 1, ErrorMessage = "El nombre del Funko no puede superar los 100 caracteres y debe ser superior a 1")]
    public string? Name { get; set; } = string.Empty;
    
    [Required (ErrorMessage =  "El precio del funko es obligatorio")]
    [Range(1, 10000, ErrorMessage = "El precio debe ser mayor a 0 con limite de 10000 €")]
    public decimal? Precio  { get; set; } = 0;
    
    [IdYoutube]
    public string? CategoriaId { get; set; }
    
    [RegularExpression(@"^[0-9a-fA-F]{24}$", ErrorMessage =  "Formato invalido de ID")]
    public string? ProveedorId { get; set; }
    
    [Range(1, int.MaxValue, ErrorMessage = "Inserte un stock mayor que 0")]
    public int? Stock { get; set; }
    
    public IFile? Image { get; set; }
    
    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (Precio <= 0 && Stock > 0)
        {
            yield return new ValidationResult(
                "El precio debe ser mayor a 0 si hay stock",
                new[] { nameof(Precio) });
        }
    }
}