using Funkos.Models;

namespace Funkos.GraphQL.Types;

public class CategoriaType : ObjectType<Categoria>
{
    protected override void Configure(IObjectTypeDescriptor<Categoria> descriptor)
    {
        descriptor.Name("Categoria");
        descriptor.Description("Categorias de los Funkos de la tienda");
        
        descriptor.Field(c => c.IdY)
            .Type<NonNullType<StringType>>()
            .Description("ID unico de la categoria");
        
        descriptor.Field(c => c.Name)
            .Type<NonNullType<StringType>>()
            .Description("Nombre de la categoria");
        
        descriptor.Field(c => c.Descripcion)
            .Type<NonNullType<StringType>>()
            .Description("Descripción de la categoria");
        
        descriptor.Field(c => c.Funkos)
            .Type<NonNullType<FunkoType>>()
            .Description("Funkos asociados a la categoria");
        
        descriptor.Field(c => c.CreateAt)
            .Type<NonNullType<DateTimeType>>()
            .Description("Fecha y hora de creación de la categoria");
        
        descriptor.Field(c => c.UpdateAt)
            .Type<NonNullType<DateTimeType>>()
            .Description("Fecha y hora de la actualización de la categoria");
    }

}