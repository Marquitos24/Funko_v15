using CSharpFunctionalExtensions;
using Funkos.Dto;
using Funkos.Dto.Common;
using Funkos.Dto.Funko;
using Funkos.Errors;
using Funkos.Models;

namespace Funkos.Service;

public interface IFunkoService
{
    //Task<Result<List<FunkosConCategoriaDtoResponse>, DomainErrors>> GetAllAsync(CancellationToken cancellationToken);

    Task<Result<PagedResult<FunkosDtoResponse>, DomainErrors>> FindAllPagedAsync(FunkoFilterDto dto,
        CancellationToken cancellationToken);
    Task<Result<FunkosDtoResponse, DomainErrors>> GetByIdAsync(long id, CancellationToken cancellationToken);
    Task<Result<FunkosDtoResponse, DomainErrors>> AddAsync(FunkoCreateDto newFunko, CancellationToken cancellationToken);
    Task<Result<FunkosDtoResponse, DomainErrors>> UpdateAsync(long id, FunkoUpdateDto updatedFunko, CancellationToken cancellationToken);

    Task<Result<FunkosDtoResponse, DomainErrors>> UpdatePartialAsync(long id, FunkoPatchDto updatedFunko,
        CancellationToken cancellationToken);
    Task<Result<bool, DomainErrors>> DeleteAsync(long id, CancellationToken cancellationToken);
}