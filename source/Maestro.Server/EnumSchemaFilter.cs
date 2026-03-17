using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Maestro.Server;

/// <summary>
/// Schema filter that converts enum schemas to use string values.
/// </summary>
public class EnumSchemaFilter : ISchemaFilter
{
    /// <inheritdoc />
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (!context.Type.IsEnum)
            return;

        schema.Enum.Clear();
        foreach (var name in Enum.GetNames(context.Type))
        {
            schema.Enum.Add(new OpenApiString(name));
        }

        schema.Type = "string";
    }
}
