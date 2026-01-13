using HotChocolate.Types;
using OsintBackend.Models;

namespace OsintBackend.GraphQL.Types
{
    public class AiJobGraphType : ObjectType<AiJob>
    {
        protected override void Configure(IObjectTypeDescriptor<AiJob> descriptor)
        {
            descriptor.Description("Represents an AI processing job queued for Ollama.");

            descriptor.Field(j => j.Id).Type<NonNullType<IntType>>();
            descriptor.Field(j => j.OsintInvestigationId).Type<NonNullType<IntType>>();
            descriptor.Field(j => j.JobType).Type<NonNullType<StringType>>();
            descriptor.Field(j => j.Status).Type<NonNullType<EnumType<AiJobStatus>>>();
            descriptor.Field(j => j.Model).Type<StringType>();
            descriptor.Field(j => j.CreatedAt).Type<NonNullType<DateTimeType>>();
            descriptor.Field(j => j.StartedAt).Type<DateTimeType>();
            descriptor.Field(j => j.CompletedAt).Type<DateTimeType>();
            descriptor.Field(j => j.AttemptCount).Type<NonNullType<IntType>>();
            descriptor.Field(j => j.Debug).Type<NonNullType<BooleanType>>();
            descriptor.Field(j => j.ResultFormat).Type<NonNullType<StringType>>();
            descriptor.Field(j => j.StructuredResult).Type<AiJobStructuredResultGraphType>();
            descriptor.Field(j => j.WorkerHost).Type<StringType>();
            descriptor.Field(j => j.LastAttemptStartedAt).Type<DateTimeType>();
            descriptor.Field(j => j.LastAttemptCompletedAt).Type<DateTimeType>();
            descriptor.Field(j => j.LastDurationMilliseconds).Type<FloatType>();
            descriptor.Field(j => j.LastError).Type<StringType>();
            descriptor.Field(j => j.Result).Type<StringType>();
            descriptor.Field(j => j.Error).Type<StringType>();
            descriptor.Field(j => j.ErrorInfo).Type<AiJobErrorInfoGraphType>();
            descriptor.Field(j => j.DebugInfo).Type<AiJobDebugInfoGraphType>();
            descriptor.Field(j => j.Investigation).Ignore();
            descriptor.Field(j => j.Prompt).Ignore();
        }
    }
}
