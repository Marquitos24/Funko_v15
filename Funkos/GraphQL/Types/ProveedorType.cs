using Funkos.Models;

namespace Funkos.GraphQL.Types;

public class ProveedorType: ObjectType<Proveedor>
{
    protected override void Configure(IObjectTypeDescriptor<Proveedor> descriptor)
    {
        descriptor.Name("Proveedor");
        descriptor.Description("Proveedores de los Funkos de la tienda");
        
        descriptor.Field(c => c.Id)
            .Type<NonNullType<StringType>>()
            .Description("ID unico del proveedor");
        
        descriptor.Field(c => c.Name)
            .Type<NonNullType<StringType>>()
            .Description("Nombre del proveedor");
        
        descriptor.Field(c => c.Descripcion)
            .Type<NonNullType<StringType>>()
            .Description("Descripción del proveedor");
        
        descriptor.Field(c => c.CreateAt)
            .Type<NonNullType<DateTimeType>>()
            .Description("Fecha y hora de creación del proveedor");
        
        descriptor.Field(c => c.UpdateAt)
            .Type<NonNullType<DateTimeType>>()
            .Description("Fecha y hora de la actualización del proveedor");
        
        descriptor.Field(f => f.IsDeleted)
            .Type<NonNullType<BooleanType>>()
            .Description("Marcador de eliminacion del proveedor");
        
        descriptor.Field(f => f.DeleteAt)
            .Type<NonNullType<DateTimeType>>()
            .Description("Fecha y hora de la eliminación del proveedor");
    }
}