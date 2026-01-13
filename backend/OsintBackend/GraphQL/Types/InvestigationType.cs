using System;
using HotChocolate.Types;
using OsintBackend.Models;

namespace OsintBackend.GraphQL.Types
{
    public class InvestigationType : ObjectType<OsintInvestigation>
    {
        protected override void Configure(IObjectTypeDescriptor<OsintInvestigation> descriptor)
        {
            descriptor.Description("Represents an OSINT investigation and its collected results.");

            descriptor.Field(i => i.Id).Type<NonNullType<IntType>>();
            descriptor.Field(i => i.Target).Type<NonNullType<StringType>>();
            descriptor.Field(i => i.InvestigationType).Type<NonNullType<StringType>>();
            descriptor.Field(i => i.RequestedAt).Type<NonNullType<DateTimeType>>();
            descriptor.Field(i => i.RequestedBy).Type<StringType>();
            descriptor.Field(i => i.Status).Type<NonNullType<EnumType<InvestigationStatus>>>();
            descriptor.Field(i => i.Results)
                .Type<ListType<NonNullType<ResultType>>>()
                .Description("Results associated with this investigation.");
        }
    }
}
