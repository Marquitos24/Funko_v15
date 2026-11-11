using CSharpFunctionalExtensions;
using Funkos.Cache;
using Funkos.Dto;
using Funkos.Dto.Common;
using Funkos.Dto.Funko;
using Funkos.Errors;

namespace Funkos.Service.Decorator;

public class CachedFunkoServiceDecorator(IFunkoService service, ICacheService cache, ILogger<CachedFunkoServiceDecorator> logger) : IFunkoService
{
    private static long _cacheVersion;//version 

    //Incrementa de 1 en 1 de forma atomic (no se intercala con otro incremento). ref == referencia  a la var para modificarla
    private static void InvalidatePagedCache() => Interlocked.Increment(ref _cacheVersion);
    
    public async Task<Result<PagedResult<FunkosDtoResponse>, DomainErrors>> FindAllPagedAsync(FunkoFilterDto dto, CancellationToken cancellationToken)
    {
        //lee el valorr actualizado con visibilidad entre hilos
        var version = Volatile.Read(ref _cacheVersion);
        
        var cacheKey = $"funkos:v{version}:{dto}";
        var cached = await cache.GetAsync<PagedResult<FunkosDtoResponse>>(cacheKey, cancellationToken);
        if (cached is not null) {logger.LogInformation("CACHE HIT: {CacheKey}", cacheKey); return Result.Success<PagedResult<FunkosDtoResponse>, DomainErrors>(cached);}

        var result = await service.FindAllPagedAsync( dto, cancellationToken);
        if (result.IsSuccess) await cache.SetAsync(cacheKey, result.Value, TimeSpan.FromMinutes(30), cancellationToken);
        return result;
    }

    
    public async Task<Result<FunkosDtoResponse, DomainErrors>> GetByIdAsync(long id,  CancellationToken cancellationToken)
    {
        var cacheKey = $"funko:{id}";
        var cached = await cache.GetAsync<FunkosDtoResponse>(cacheKey, cancellationToken);
        if (cached is not null) return Result.Success<FunkosDtoResponse, DomainErrors>(cached);

        var result = await service.GetByIdAsync(id, cancellationToken);
        if (result.IsSuccess) await cache.SetAsync(cacheKey, result.Value, TimeSpan.FromMinutes(30), cancellationToken);
        
        return result;
    }

    public async Task<Result<FunkosDtoResponse, DomainErrors>> AddAsync(FunkoCreateDto newFunko, CancellationToken cancellationToken)
    {
        var result = await service.AddAsync(newFunko, cancellationToken);
        if (result.IsSuccess) { logger.LogInformation("CACHE HIT: Registro GetAll Invalidado"); InvalidatePagedCache();}
        return result;
    }

    public async Task<Result<FunkosDtoResponse, DomainErrors>> UpdateAsync(long id, FunkoUpdateDto updatedFunko, CancellationToken cancellationToken)
    {
        var result = await service.UpdateAsync(id, updatedFunko, cancellationToken);
        if (result.IsSuccess)
        {
            //GetAll
            InvalidatePagedCache();
            logger.LogInformation("CACHE HIT: Registro GetAll Invalidado");
            //GetId
            await cache.RemoveAsync($"funko:{id}", cancellationToken);
            logger.LogInformation("CACHE HIT: Registro GetById Invalidado");
        }
        return result;
    }

    public async Task<Result<bool, DomainErrors>> DeleteAsync(long id, CancellationToken cancellationToken)
    {
        var result = await service.DeleteAsync(id, cancellationToken);
        if (result.IsSuccess)
        {
            //GetAll
            InvalidatePagedCache();
            logger.LogInformation("CACHE HIT: Registro GetAll Invalidado");
            //GetId
            await cache.RemoveAsync($"funko:{id}", cancellationToken);
            logger.LogInformation("CACHE HIT: Registro GetById Invalidado");
        }
        return result;
    }
    
    public async Task<Result<FunkosDtoResponse, DomainErrors>> UpdatePartialAsync(long id, FunkoPatchDto updatedFunko, CancellationToken cancellationToken)
    {
        var result = await service.UpdatePartialAsync(id, updatedFunko, cancellationToken);
        if (result.IsSuccess)
        {
            //GetAll
            InvalidatePagedCache();
            logger.LogInformation("CACHE HIT: Registro GetAll Invalidado");
            //GetId
            await cache.RemoveAsync($"funko:{id}", cancellationToken);
            logger.LogInformation("CACHE HIT: Registro GetById Invalidado");
        }
        return result;
    }

}