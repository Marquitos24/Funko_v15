using System.ComponentModel.DataAnnotations;
using Funkos.idGenerators;

namespace Funkos.GraphQL.Inputs;

public class CreateCategoriaInput
{
    [Required (ErrorMessage =  "El nombre de la categoria es obligatoria")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "El nombre de la categoria no puede superar los 200 caracteres y debe ser superior a 1")]
    public required string Name { get; set; }
    
    [Required (ErrorMessage =  "La descripción de la categoria es obligatoria")]
    [StringLength(300, MinimumLength = 1, ErrorMessage = "La descripción de la categoria no puede superar los 300 caracteres y debe ser superior a 1")]
    public required string Descripcion  { get; set; }
}

public class UpdateCategoriaInput
{
    [IdYoutube]
    public string? IdY { get; set; }
    
    [StringLength(200, MinimumLength = 1, ErrorMessage = "El nombre de la categoria no puede superar los 200 caracteres y debe ser superior a 1")]
    public string? Name { get; set; } = string.Empty;
    
    [StringLength(300, MinimumLength = 1, ErrorMessage = "La descripción de la categoria no puede superar los 300 caracteres y debe ser superior a 1")]
    public string? Descripcion  { get; set; }
}