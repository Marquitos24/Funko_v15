using Funkos.Dto;
using Funkos.Dto.Categoria;
using Funkos.Dto.Funko;
using Funkos.GraphQL.Inputs;
using Funkos.Service;
using Funkos.Service.Categoria;
using HotChocolate.Subscriptions;
using Microsoft.AspNetCore.Authorization;

namespace Funkos.GraphQL.Mutations;

public class TiendaMutation
{
    /// <summary>
    /// Crea un nuevo funko
    /// </summary>
    [Authorize(Policy = "RequireAdmin")]
    public async Task<FunkosDtoResponse> CrearProducto(
        CreateFunkoInput input,
        [Service] IFunkoService service,
        [Service] ITopicEventSender eventSender, CancellationToken cancellationToken)
    {
        IFormFile? image = null;
        
        if (input.Image is not null && input.Image.Length > 0)
        {
            var stream = input.Image.OpenReadStream();

            image = new FormFile(
                stream,
                0,
                input.Image.Length.Value,
                "Image",
                input.Image.Name
            );
        }
        
        var dto = new FunkoCreateDto
        {
            Name = input.Name,
            Precio = input.Precio,
            Stock = input.Stock,
            CategoriaId = input.CategoriaId,
            ProveedorId = input.ProveedorId,
            Image = image
        };

        var funko = await service.AddAsync(dto, cancellationToken);

        // Notificar a suscriptores
        await eventSender.SendAsync("FunkoCreado", funko);

        if (!funko.IsSuccess)
        {
            throw new GraphQLException(funko.Error.Message);
        }

        return funko.Value;
    }
    
    /// <summary>
    /// Actualiza un funko existente
    /// </summary>
    [Authorize(Policy = "RequireAdmin")]
    public async Task<FunkosDtoResponse?> ActualizarFunko(
        long id,
        UpdateFunkoInput input,
        [Service] IFunkoService service,
        [Service] ITopicEventSender eventSender, CancellationToken cancellationToken)
    {
        IFormFile? image = null;
        
        if (input.Image is not null && input.Image.Length > 0)
        {
            var stream = input.Image.OpenReadStream();

            image = new FormFile(
                stream,
                0,
                input.Image.Length.Value,
                "Image",
                input.Image.Name
            );
        }
        
        var dto = new FunkoPatchDto
        {
            Name = input.Name,
            Precio = input.Precio,
            Stock = input.Stock,
            CategoriaId = input.CategoriaId,
            ProveedorId = input.ProveedorId,
            Image =  image
        };

        var funkoUpdate = await service.UpdatePartialAsync(id, dto, cancellationToken);
       
        if (!funkoUpdate.IsSuccess)
        {
            throw new GraphQLException(funkoUpdate.Error.Message);
        }
        
        await eventSender.SendAsync("FunkoActualizado", funkoUpdate);

        return funkoUpdate.Value;
    }
    
    /// <summary>
    /// Elimina un funko (borrado logico)
    /// </summary>
    [Authorize(Policy = "RequireAdmin")]
    public async Task<bool> EliminarFunko(
        long id,
        [Service] IFunkoService service,
        [Service] ITopicEventSender eventSender, CancellationToken cancellationToken)
    {
        var eliminado = await service.DeleteAsync(id, cancellationToken);
        await eventSender.SendAsync("ProductoEliminado", id);
        if (eliminado.IsFailure)
        {
            throw new GraphQLException(eliminado.Error.Message);
        }

        return eliminado.Value;
    }
    
    /// <summary>
    /// Crea un nueva categoria
    /// </summary>
    [Authorize(Policy = "RequireAdmin")]
    public async Task<CategoriaDtoResponse> CrearCategoria(
        CreateCategoriaInput input,
        [Service] ICategoriaService service,
        [Service] ITopicEventSender eventSender, CancellationToken cancellationToken)
    {
        var dto = new CategoriaCreateDto
        {
            Name = input.Name,
            Descripcion = input.Descripcion,
        };

        var cat = await service.AddAsync(dto, cancellationToken);

        // Notificar a suscriptores
        await eventSender.SendAsync("CategoriaCreado", cat);

        if (!cat.IsSuccess)
        {
            throw new GraphQLException(cat.Error.Message);
        }

        return cat.Value;
    }
    
    /// <summary>
    /// Actualiza un funko existente
    /// </summary>
    [Authorize(Policy = "RequireAdmin")]
    public async Task<CategoriaDtoResponse?> ActualizarCategoria(
        string id,
        UpdateCategoriaInput input,
        [Service] ICategoriaService service,
        [Service] ITopicEventSender eventSender, CancellationToken cancellationToken)
    {
        var dto = new CategoriaUpdateDto
        {
            Name = input.Name,
            Descripcion = input.Descripcion
        };

        var catUpdate = await service.UpdateAsync(id, dto, cancellationToken);
       
        await eventSender.SendAsync("CategoriaActualizada", catUpdate);
        

        if (!catUpdate.IsSuccess)
        {
            throw new GraphQLException(catUpdate.Error.Message);
        }

        return catUpdate.Value;
    }
    
    /// <summary>
    /// Elimina una categoria (borrado fisico)
    /// </summary>
    [Authorize(Policy = "RequireAdmin")]
    public async Task<bool> EliminarCategoria(
        string id,
        [Service] ICategoriaService service,
        [Service] ITopicEventSender eventSender, CancellationToken cancellationToken)
    {
        var eliminado = await service.DeleteAsync(id, cancellationToken);
        await eventSender.SendAsync("CategoriaEliminada", id);
        if (eliminado.IsFailure)
        {
            throw new GraphQLException(eliminado.Error.Message);
        }

        return eliminado.Value;
    }
}