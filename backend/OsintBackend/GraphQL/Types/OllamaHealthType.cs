using HotChocolate.Types;
using OsintBackend.Models;

namespace OsintBackend.GraphQL.Types
{
    public class OllamaHealthType : ObjectType<OllamaHealth>
    {
        protected override void Configure(IObjectTypeDescriptor<OllamaHealth> descriptor)
        {
            descriptor.Description("Represents connectivity status of the configured Ollama endpoint.");

            descriptor.Field(h => h.BaseUrl).Type<NonNullType<StringType>>();
            descriptor.Field(h => h.IsAvailable).Type<NonNullType<BooleanType>>();
            descriptor.Field(h => h.StatusMessage).Type<StringType>();
            descriptor.Field(h => h.LatencyMilliseconds).Type<NonNullType<FloatType>>();
            descriptor.Field(h => h.Models).Type<ListType<NonNullType<StringType>>>();
            descriptor.Field(h => h.CheckedAt).Type<NonNullType<DateTimeType>>();
        }
    }
}
