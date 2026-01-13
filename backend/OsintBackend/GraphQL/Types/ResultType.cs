using System;
using HotChocolate.Types;
using OsintBackend.Models;

namespace OsintBackend.GraphQL.Types
{
    public class ResultType : ObjectType<OsintResult>
    {
        protected override void Configure(IObjectTypeDescriptor<OsintResult> descriptor)
        {
            descriptor.Description("Represents a single OSINT data point captured during an investigation.");

            descriptor.Field(r => r.Id).Type<NonNullType<IntType>>();
            descriptor.Field(r => r.ToolName).Type<NonNullType<StringType>>();
            descriptor.Field(r => r.DataType).Type<NonNullType<StringType>>();
            descriptor.Field(r => r.RawData).Type<NonNullType<StringType>>();
            descriptor.Field(r => r.Summary).Type<StringType>();
            descriptor.Field(r => r.CollectedAt).Type<NonNullType<DateTimeType>>();
            descriptor.Field(r => r.ConfidenceScore).Type<StringType>();
            descriptor.Field(r => r.OsintInvestigationId).Type<NonNullType<IntType>>();
        }
    }
}
