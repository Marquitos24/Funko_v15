using CSharpFunctionalExtensions;
using FluentValidation;
using Funkos.Dto;
using Funkos.Dto.Common;
using Funkos.Dto.Funko;
using Funkos.Errors;
using Funkos.Mappers;
using Funkos.Models;
using Funkos.RealTime.Notificaciones;
using Funkos.Repositories;
using Funkos.Service.Storage;
using IOPath = System.IO.Path;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Funkos.Service;

public class FunkoService(IFunkoRepository repository, IValidator<FunkoCreateDto> _validatorCreateAsync, IValidator<FunkoUpdateDto> _validatorUpdateAsync, IValidator<FunkoPatchDto> _validatorUpdatePatchAsync, ILogger<FunkoService> logger, IStorageService storage,  IHubContext<NotificacionesHub> hubContext) : IFunkoService
{
    /*
     * public async Task<Result<List<FunkosConCategoriaDtoResponse>, DomainErrors>>  GetAllAsync(CancellationToken cancellationToken)
    {
        List<Funko> funkos = await repository.FindAllAsync(cancellationToken);
       // IEnumerable<FunkoQueryDto> funkosDto = service.GetAll();
        List<FunkosConCategoriaDtoResponse> funkoResponse = funkos.Select(f => FunkoMapper.ToListDtoResponse(f)).ToList();
        return Result.Success<List<FunkosConCategoriaDtoResponse>, DomainErrors>(funkoResponse);
    }
     */
    
    public async Task<Result<PagedResult<FunkosDtoResponse>, DomainErrors>> FindAllPagedAsync(FunkoFilterDto dto, CancellationToken cancellationToken)
    {
        logger.LogInformation("Obteniendo Funkos paginados - Página: {Page}, Tamaño: {Size}", dto.Page, dto.Size);

        // 1. Obtenemos la tupla del repo (FunkoFilterDto)
        var (funkos, totalCount) = await repository.FindAllPagedAsync(dto, cancellationToken);
        
        // 2. Mapeamos las entidades a tu FunkosDtoResponse real
        var dtos = funkos.ToDtoList();

        var pagedResult = new PagedResult<FunkosDtoResponse>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = dto.Page + 1,
            PageSize = dto.Size
        };

        return Result.Success<PagedResult<FunkosDtoResponse>, DomainErrors>(pagedResult);
    }
    
    public async Task<Result<FunkosDtoResponse, DomainErrors>> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        logger.LogInformation("Buscando Funko {Id}", id);
        if (id <= 0) return Result.Failure<FunkosDtoResponse, DomainErrors>(FunkoError.NotFound(id));
        Funko? funko = await repository.FindByIdAsync(id, cancellationToken); 
        logger.LogWarning("Funko {Id} NO encontrado ", id);
        if(funko == null) {logger.LogWarning("Funko {Id} NO encontrado ", id); return Result.Failure<FunkosDtoResponse, DomainErrors>(FunkoError.NotFound());} 
       
        FunkosDtoResponse funkoResponse =  FunkoMapper.ToDtoResponse(funko);
        return Result.Success<FunkosDtoResponse, DomainErrors>(funkoResponse);
    }
    
    public async Task<Result<FunkosDtoResponse, DomainErrors>> AddAsync(FunkoCreateDto newFunko, CancellationToken cancellationToken)
    {
        // Si la validación falla, ModelState.IsValid será false automáticamente
        var validationResult = await _validatorCreateAsync.ValidateAsync(newFunko);
        if (!validationResult.IsValid)
        {
            logger.LogInformation("Hay errores de validación");
            // Si falla, mapeamos los errores de FluentValidation a tu sistema de DomainErrors
            var errorsDescription = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result.Failure<FunkosDtoResponse, DomainErrors>(FunkoError.BadRequest(errorsDescription));
        }
        
        var model = FunkoMapper.ToEntityCreate(newFunko);
        if (model == null) return Result.Failure<FunkosDtoResponse, DomainErrors>(FunkoError.NotFound()); logger.LogWarning("Funko NO creado ");
        
        //Comprobamos que el funko nuevo NO tiene un nombre igual a otro Funko
        bool yaExiste = await repository.ExistsByNameAsync(newFunko.Name, null, cancellationToken);
        if (yaExiste) return Result.Failure<FunkosDtoResponse, DomainErrors>(FunkoError.Duplicate(model.Name)); logger.LogWarning("Ya existe el funko");
        
        // --- GESTIÓN DE LA IMAGEN EN CREACIÓN ---
        if (newFunko.Image is not null && newFunko.Image.Length > 0)
        {
            var storageResult = await storage.StoreAsync(newFunko.Image, "funkos", cancellationToken);
            
            if (storageResult.IsFailure)
            {
                logger.LogWarning("Error al guardar la imagen del Funko");
                return Result.Failure<FunkosDtoResponse, DomainErrors>(storageResult.Error);
            }
            // Extraemos el nombre del archivo y generamos la URL
            var fileName = storageResult.Value;
            // Obtenemos su URL pública relativa y se la asignamos al modelo
            model.Imagen = storage.GetUrl(fileName, "funkos");
        }
        else
        {
            model.Imagen = "/uploads/funkos/default.png"; // Imagen por defecto si no se sube ninguna
        }
        
        Funko funko = await repository.CreateAsync(model, cancellationToken);
        FunkosDtoResponse funkoResponse = FunkoMapper.ToDtoResponse(funko);
        logger.LogInformation("Funko Creado correctamente");
        
        // Notificar a todos los usuarios
        await hubContext.Clients.All.SendAsync("FunkoCreado", new 
        { 
            funko.Id, 
            funko.Name,
            Mensaje = $"Se ha creado el Funko: {funko.Id} ({funko.Name})"
        });
        
        return Result.Success<FunkosDtoResponse, DomainErrors>(funkoResponse);
    }
    
    public async Task<Result<FunkosDtoResponse, DomainErrors>> UpdateAsync(long id, FunkoUpdateDto updatedFunko, CancellationToken cancellationToken)
    {
        // Si la validación falla, ModelState.IsValid será false automáticamente
        var context = new ValidationContext<FunkoUpdateDto>(updatedFunko);
        context.RootContextData["FunkoId"] = id;
        var validationResult = await _validatorUpdateAsync.ValidateAsync(context);
        if (!validationResult.IsValid)
        {
            logger.LogInformation("Hay errores de validación");
            // Si falla, mapeamos los errores de FluentValidation a tu sistema de DomainErrors
            var errorsDescription = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result.Failure<FunkosDtoResponse, DomainErrors>(FunkoError.BadRequest(errorsDescription));
        }
        
        //Buscamos el funko por Id y comprobamos si existe, si no salta error
        var funkoId = await repository.FindByIdAsync(id, cancellationToken);
        if (id <= 0 || funkoId is null) { logger.LogWarning("No se encontro el Funko {id}", id); return Result.Failure<FunkosDtoResponse, DomainErrors>(FunkoError.NotFound(id));}
        
        //Convertimos el RequestDTO al Modelo y validamos, si es null salta error
        var funkoModel = FunkoMapper.ToEntityUpdate(updatedFunko);
        if (funkoModel is null) {logger.LogWarning("No guardo el Funko {id}", id); return Result.Failure<FunkosDtoResponse, DomainErrors>(FunkoError.NotFound());} 
        //Comprobamos que el Categoria actualizado NO tiene un nombre igual a otro Categoria
     
        // --- GESTIÓN DE LA IMAGEN EN ACTUALIZACIÓN ---
        if (updatedFunko.Image is not null && updatedFunko.Image.Length > 0)
        {
            var storageResult = await storage.StoreAsync(updatedFunko.Image, "funkos", cancellationToken);
            
            if (storageResult.IsFailure)
            {
                logger.LogWarning("Error al guardar la imagen del Funko");
                return Result.Failure<FunkosDtoResponse, DomainErrors>(storageResult.Error);
            }
            // Guardamos la nueva imagen
            var fileName = storageResult.Value;
            funkoModel.Imagen = storage.GetUrl(fileName, "funkos");

            // Borramos la imagen vieja del disco si no es la por defecto
            if (!string.IsNullOrEmpty(funkoId.Imagen) && !funkoId.Imagen.EndsWith("default.png"))
            {
                var oldFileName = IOPath.GetFileName(funkoId.Imagen);
                await storage.DeleteAsync(oldFileName, "funkos", cancellationToken);
            }
        }
        else
        {
            // Si no se adjunta nueva imagen en el Update, mantenemos la que ya tenía guardada
            funkoModel.Imagen = funkoId.Imagen;
            if(funkoId.Imagen is null){funkoModel.Imagen = "/uploads/funkos/default.png";}
        }
        
       
       try
       {
           //Actualizamos Funko y comprobamos que no sea null, si lo es salta error
           Funko? funko = await repository.UpdateAsync(id, funkoModel, cancellationToken);
           if (funko == null) return Result.Failure<FunkosDtoResponse, DomainErrors>(FunkoError.NotFound());

           //Transformamos a ResponseDTO
           FunkosDtoResponse funkoResponse = FunkoMapper.ToDtoResponse(funko);
           logger.LogInformation("Funko {id} actualizdo correctamente", id);
           
           // Notificar a admins
           await hubContext.Clients.Group("admins").SendAsync("FunkoActualizado", new 
           { 
               Id = id,
               Mensaje = $"El Funko con ID {id} ha sido actualizado." 
           });
           
           //Devolvemos
           return Result.Success<FunkosDtoResponse, DomainErrors>(funkoResponse);
       }
       catch (DbUpdateConcurrencyException ex)
       {
           logger.LogWarning(ex, "Conflicto de concurrencia al actualizar el Funko {id}", id);
           var entry = ex.Entries.First();
           var databaseValues = await entry.GetDatabaseValuesAsync(cancellationToken);

           if (databaseValues is null)
           {
               return Result.Failure<FunkosDtoResponse, DomainErrors>(CategoriaError.NotFound(id));
           }
           
           return Result.Failure<FunkosDtoResponse, DomainErrors>(FunkoError.Concurrency("El Funko ",id, "modificado "));
       }
    }

    public async Task<Result<bool, DomainErrors>> DeleteAsync(long id, CancellationToken cancellationToken)
    {
        //Comprobamos que el Id existe, si no salta error
        var funkoId = await repository.FindByIdAsync(id, cancellationToken);
        if (id <= 0 || funkoId is null) return Result.Failure<bool, DomainErrors>(FunkoError.NotFound(id));
        
        try
        {
            var deleted = await repository.DeleteAsync(id, cancellationToken);
        
            if (!deleted) return Result.Failure<bool, DomainErrors>(FunkoError.NotFound(funkoId.Name));
            // Imagen: borramos archivo
            if (!string.IsNullOrEmpty(funkoId.Imagen) && !funkoId.Imagen.EndsWith("default.png"))
            {
                var fileName = IOPath.GetFileName(funkoId.Imagen);
                await storage.DeleteAsync(fileName, "funkos", cancellationToken);
            }
            logger.LogWarning("Funko {id} eliminado correctamente", id);
            
            // Notificar a admins
            await hubContext.Clients.Group("admins").SendAsync("FunkoEliminado", new 
            { 
                Id = id,
                Mensaje = $"El Funko con ID {id} ha sido eliminado." 
            });

            
            return Result.Success<bool, DomainErrors>(true);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogWarning(ex, "Conflicto de concurrencia al actualizar el Funko {id}", id);
            var entry = ex.Entries.First();
            var databaseValues = await entry.GetDatabaseValuesAsync(cancellationToken);

            if (databaseValues is null)
            {
                return Result.Failure<bool, DomainErrors>(CategoriaError.NotFound(id));
            }
           
            return Result.Failure<bool, DomainErrors>(FunkoError.Concurrency("El Funko ",id, "eliminado "));
        }
    }
    
     public async Task<Result<FunkosDtoResponse, DomainErrors>> UpdatePartialAsync(long id, FunkoPatchDto updatedFunko, CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            logger.LogWarning("ID de Funko inválido: {id}", id);
            return Result.Failure<FunkosDtoResponse, DomainErrors>(FunkoError.NotFound(id));
        }
        
        // Si la validación falla, ModelState.IsValid será false automáticamente
        var context = new ValidationContext<FunkoPatchDto>(updatedFunko);
        context.RootContextData["FunkoId"] = id;
        var validationResult = await _validatorUpdatePatchAsync.ValidateAsync(context);
        if (!validationResult.IsValid)
        {
            logger.LogInformation("Hay errores de validación");
            // Si falla, mapeamos los errores de FluentValidation a tu sistema de DomainErrors
            var errorsDescription = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result.Failure<FunkosDtoResponse, DomainErrors>(FunkoError.BadRequest(errorsDescription));
        }
        
        //Buscamos el funko por Id y comprobamos si existe, si no salta error
        var funkoId = await repository.FindByIdAsync(id, cancellationToken);
        if (funkoId is null) { logger.LogWarning("No se encontro el Funko {id}", id); return Result.Failure<FunkosDtoResponse, DomainErrors>(FunkoError.NotFound(id));}
        
        // --- GESTIÓN DE LA IMAGEN EN ACTUALIZACIÓN ---
        if (updatedFunko.Image is not null && updatedFunko.Image.Length > 0)
        {
            var storageResult = await storage.StoreAsync(updatedFunko.Image, "funkos", cancellationToken);
            
            if (storageResult.IsFailure)
            {
                logger.LogWarning("Error al guardar la imagen del Funko");
                return Result.Failure<FunkosDtoResponse, DomainErrors>(storageResult.Error);
            }
            // Guardamos la nueva imagen
            var fileName = storageResult.Value;
            var oldImage = funkoId.Imagen;

            funkoId.Imagen = storage.GetUrl(fileName, "funkos");

            // Borramos la imagen vieja del disco si no es la por defecto
            if (!string.IsNullOrEmpty(oldImage) && !oldImage.EndsWith("default.png"))
            {
                var oldFileName = IOPath.GetFileName(oldImage);
                await storage.DeleteAsync(oldFileName, "funkos", cancellationToken);
            }
        }
        
        AplicarPatch(funkoId, updatedFunko);
       
       try
       {
           //Actualizamos Funko y comprobamos que no sea null, si lo es salta error
           var updatedfunko = await repository.UpdateAsync(id, funkoId, cancellationToken);
           if (updatedfunko == null) return Result.Failure<FunkosDtoResponse, DomainErrors>(FunkoError.NotFound());

           //Transformamos a ResponseDTO
           var funkoResponse = FunkoMapper.ToDtoResponse(updatedfunko);
           logger.LogInformation("Funko {id} actualizdo correctamente", id);
           
           //Devolvemos
           return Result.Success<FunkosDtoResponse, DomainErrors>(funkoResponse);
       }
       catch (DbUpdateConcurrencyException ex)
       {
           logger.LogWarning(ex, "Conflicto de concurrencia al actualizar el Funko {id}", id);
           var entry = ex.Entries.First();
           var databaseValues = await entry.GetDatabaseValuesAsync(cancellationToken);

           if (databaseValues is null)
           {
               return Result.Failure<FunkosDtoResponse, DomainErrors>(CategoriaError.NotFound(id));
           }
           
           return Result.Failure<FunkosDtoResponse, DomainErrors>(FunkoError.Concurrency("El Funko ",id, "modificado "));
       }
    }
    /// <summary>
    /// Aplica el patch condicional: cada campo solo se toca si el DTO trae valor
    /// (la semantica de PATCH). Metodo puro sin estado: extraido de
    /// UpdatePartialAsync para dejar el flujo de ese metodo lineal.
    /// </summary>
    private static void AplicarPatch(Funko funko, FunkoPatchDto dto) {
        if (!string.IsNullOrWhiteSpace(dto.Name))
            funko.Name = dto.Name;

        if (dto.Precio.HasValue && dto.Precio.Value > 0)
            funko.Precio = dto.Precio.Value;

        if (dto.Stock.HasValue)
            funko.Stock = dto.Stock.Value;

        if (!string.IsNullOrWhiteSpace(dto.CategoriaId))
            funko.CategoriaId = dto.CategoriaId;

        if (!string.IsNullOrWhiteSpace(dto.ProveedorId))
            funko.ProveedorId = dto.ProveedorId;
    }

    
}