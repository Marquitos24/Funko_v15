using Funkos.DbContext;
using Funkos.Dto.Categoria;
using Funkos.Dto.Common;
using Funkos.Dto.Funko;
using Funkos.Models;
using Funkos.Repositories;
using Funkos.Repositories.Categoria;
using Funkos.Service;
using Funkos.Service.Categoria;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Funkos.GraphQL.Queries;

/// <summary>
/// Consultas de solo LECTURA de GraphQL.
/// </summary>
public class TiendaQuery
{
    /// <summary>Obtiene productos paginados.</summary>
    /// <param name="page">Número de página (base 1, contrato GraphQL).</param>
    /// <param name="size">Elementos por página.</param>
    /// <param name="funkoRepo">Repositorio de funkos.</param>
    /// <returns>Resultado paginado de productos.</returns>
    /// Hot Chocolate necesita un IQueryable<T> (consulta preparada que aún no ha ido a la BD).
    ///Si usaramos el servicio (Devuelve List<T> o un Task<T>), Hot Chocolate NO podría concatenar los filtros,
    /// los Skip/Take de la paginación ni los OrderBy en la sentencia SQL.
    ///Para estas consultas masivas, se llama directamente al repo para que devuelva el IQueryable<T> y la tubería de GraphQL lo gestione.
    ///Cuando buscamos por ID o en lógica de negocio Si usamos el Servicio:
    ///Para operaciones puntuales (GetProducto(id)) o en las Mutations, no necesitas componer consultas dinámicas.
    /// Ahí entra en juego que el servicio encapsula la lógica, valida, mapea, maneja los objetos Resul
    /// y asegura que todo pase por la capa de negocio igual que en tus controladores REST.
    
    // Hot Chocolate construye la consulta SQL por mi en tiempo real dependiendo de lo que el cliente pida
    //SI SE PONE UN [X] ENCIMA DEL METODO NECESITAS UN IQueryable<X>
    [UsePaging(MaxPageSize = 10, DefaultPageSize = 10)]
    [UseProjection] // genera la consulta excta --> Para rendimieto
    [UseFiltering]
    [UseSorting]
    public IQueryable<Funko> GetFunkosPaged([Service] AppDbContext db)
    {
        // DbContext para que devuelva IQueryable<Funko>
        // para que Hot Chocolate pueda componer la consulta SQL dinámicamente.
        // Si el cliente pide ver la categoría de un Funko, se debe poder traer la categoría.
        return db.Funkos.Include(f => f.Categoria).AsQueryable();
    }
    
    /// <summary>Obtiene un funko por ID como DTO.</summary>
    /// <param name="id">ID del funko.</param>
    /// <param name="FunkoService">Repositorio de funkos.</param>
    /// <returns>Producto encontrado (DTO) o null.</returns>
    [Authorize(Policy = "RequireAdmin")]
    public async Task<FunkosDtoResponse?> GetFunko(
        long id,
        [Service] IFunkoService FunkoService, CancellationToken cancellationToken)
    {
        var result = await FunkoService.GetByIdAsync(id, cancellationToken);
        if (!result.IsSuccess) return null; 
        return result.Value;
    }
    
    /// <summary>Obtiene todas las categorías con soporte de paginación/filtrado de Hot Chocolate.</summary>
    [UsePaging(MaxPageSize = 50, DefaultPageSize = 10)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<Categoria> GetCategorias([Service] AppDbContext db)
    {
        return db.Categorias.Include(c => c.Funkos).AsQueryable();
    }

    /// <summary>Obtiene una categoría por ID como DTO.</summary>
    /// <param name="id">ID de la categoría.</param>
    /// <param name="categoriaRepository">Repositorio de categorías.</param>
    /// <returns>Categoría encontrada (DTO) o null.</returns>
    [Authorize(Policy = "RequireAdmin")]
    public async Task<CategoriaWithFunkosResponseDto?> GetCategoria(
        string id,
        [Service] ICategoriaService categoriaService, CancellationToken cancellationToken)
    {
        var result = await categoriaService.GetByIdAsync(id, cancellationToken);
        if (!result.IsSuccess) return null;
        return result.Value;
    }
}