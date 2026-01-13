using System.Collections.Generic;
using HotChocolate.Types;
using OsintBackend.Models;

namespace OsintBackend.GraphQL.Types
{
    public class AiJobStructuredResultGraphType : ObjectType<AiJobStructuredResult>
    {
        protected override void Configure(IObjectTypeDescriptor<AiJobStructuredResult> descriptor)
        {
            descriptor.Field(r => r.FormatVersion).Type<NonNullType<StringType>>();
            descriptor.Field(r => r.Sections).Type<NonNullType<ListType<NonNullType<AiJobStructuredResultSectionGraphType>>>>();
            descriptor.Field("metadata")
                .Type<JsonType>()
                .Resolve(context => context.Parent<AiJobStructuredResult>().Metadata);
        }
    }

    public class AiJobStructuredResultSectionGraphType : ObjectType<AiJobStructuredResultSection>
    {
        protected override void Configure(IObjectTypeDescriptor<AiJobStructuredResultSection> descriptor)
        {
            descriptor.Field(s => s.Key).Type<NonNullType<StringType>>();
            descriptor.Field(s => s.Heading).Type<NonNullType<StringType>>();
            descriptor.Field(s => s.Content).Type<NonNullType<StringType>>();
        }
    }

    public class AiJobErrorInfoGraphType : ObjectType<AiJobErrorInfo>
    {
        protected override void Configure(IObjectTypeDescriptor<AiJobErrorInfo> descriptor)
        {
            descriptor.Field(e => e.Message).Type<NonNullType<StringType>>();
            descriptor.Field(e => e.Code).Type<StringType>();
            descriptor.Field(e => e.Details).Type<StringType>();
            descriptor.Field(e => e.IsRetryable).Type<NonNullType<BooleanType>>();
            descriptor.Field(e => e.OccurredAt).Type<NonNullType<DateTimeType>>();
            descriptor.Field("metadata")
                .Type<JsonType>()
                .Resolve(context => context.Parent<AiJobErrorInfo>().Metadata);
        }
    }
}
