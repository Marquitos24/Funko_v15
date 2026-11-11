using Funkos.Models;

namespace Funkos.GraphQL.Subscriptions;

public class Subscription
{
    /// <summary>
    /// Notifica cuando se crea un nuevo funko
    /// </summary>
    [Subscribe]
    [Topic(nameof(FunkoCreado))]
    public Funko FunkoCreado([EventMessage] Funko funko)
    {
        return funko;
    }

    /// <summary>
    /// Notifica cuando se actualiza un funko
    /// </summary>
    [Subscribe]
    [Topic(nameof(FunkoActualizado))]
    public Funko FunkoActualizado([EventMessage] Funko funko)
    {
        return funko;
    }

    /// <summary>
    /// Notifica cuando se elimina un funko
    /// </summary>
    [Subscribe]
    [Topic(nameof(FunkoEliminado))]
    public long FunkoEliminado([EventMessage] long id)
    {
        return id;
    }
}