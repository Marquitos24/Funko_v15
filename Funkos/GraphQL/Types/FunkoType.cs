using Funkos.Models;

namespace Funkos.GraphQL.Types;

public class FunkoType : ObjectType<Funko>
{
    protected override void Configure(IObjectTypeDescriptor<Funko> descriptor)
    {
        descriptor.Name("Funko");
        descriptor.Description("Funko de la tienda");
        
        descriptor.Field(f => f.Id)
            .Type<NonNullType<IdType>>()
            .Description("ID unico del funko");
        
        descriptor.Field(f => f.Name)
            .Type<NonNullType<StringType>>()
            .Description("Nombre del funko");
        
        descriptor.Field(f => f.Precio)
            .Type<NonNullType<DecimalType>>()
            .Description("Precio en euros");
        
        descriptor.Field(f => f.CategoriaId)
            .Type<NonNullType<StringType>>()
            .Description("ID unico de la categoria del funko");
        
        descriptor.Field(f => f.Categoria)
            .Type<NonNullType<CategoriaType>>()
            .Description("Categoria del funko");
        
        descriptor.Field(f => f.ProveedorId)
            .Type<NonNullType<StringType>>()
            .Description("ID unico del proveedor del funko");
        
        descriptor.Field(f => f.Stock)
            .Type<NonNullType<IntType>>()
            .Description("Cantidad de stock");
        
        descriptor.Field(f => f.Imagen)
            .Type<NonNullType<StringType>>()
            .Description("Imagen del funko");
        
        descriptor.Field(f => f.CreateAt)
            .Type<NonNullType<DateTimeType>>()
            .Description("Fecha y hora de creación del funko");
        
        descriptor.Field(f => f.UpdateAt)
            .Type<NonNullType<DateTimeType>>()
            .Description("Fecha y hora de la actualización del funko");
        
        descriptor.Field(f => f.IsDeleted)
            .Type<NonNullType<BooleanType>>()
            .Description("Marcador de eliminacion del funko");
        
        descriptor.Field(f => f.DeleteAt)
            .Type<NonNullType<DateTimeType>>()
            .Description("Fecha y hora de la eliminación del funko");

    }
}