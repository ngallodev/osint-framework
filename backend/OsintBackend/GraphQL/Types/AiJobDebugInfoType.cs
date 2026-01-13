using HotChocolate.Types;
using OsintBackend.Models;

namespace OsintBackend.GraphQL.Types
{
    public class AiJobDebugInfoGraphType : ObjectType<AiJobDebugInfo>
    {
        protected override void Configure(IObjectTypeDescriptor<AiJobDebugInfo> descriptor)
        {
            descriptor.Field(d => d.PromptText).Type<StringType>();
            descriptor.Field(d => d.PromptLength).Type<IntType>();
            descriptor.Field(d => d.RequestSentAt).Type<DateTimeType>();
            descriptor.Field(d => d.ResponseReceivedAt).Type<DateTimeType>();
            descriptor.Field(d => d.OllamaMetrics).Type<OllamaDebugMetricsGraphType>();
            descriptor.Field(d => d.HttpMetrics).Type<HttpDebugMetricsGraphType>();
        }
    }

    public class OllamaDebugMetricsGraphType : ObjectType<OllamaDebugMetrics>
    {
        protected override void Configure(IObjectTypeDescriptor<OllamaDebugMetrics> descriptor)
        {
            descriptor.Field(m => m.Model).Type<StringType>();
            descriptor.Field(m => m.TotalDurationNs).Type<LongType>();
            descriptor.Field(m => m.LoadDurationNs).Type<LongType>();
            descriptor.Field(m => m.PromptEvalCount).Type<IntType>();
            descriptor.Field(m => m.PromptEvalDurationNs).Type<LongType>();
            descriptor.Field(m => m.EvalCount).Type<IntType>();
            descriptor.Field(m => m.EvalDurationNs).Type<LongType>();
            descriptor.Field(m => m.PromptTokensPerSecond).Type<FloatType>();
            descriptor.Field(m => m.ResponseTokensPerSecond).Type<FloatType>();
            descriptor.Field(m => m.DoneReason).Type<StringType>();
        }
    }

    public class HttpDebugMetricsGraphType : ObjectType<HttpDebugMetrics>
    {
        protected override void Configure(IObjectTypeDescriptor<HttpDebugMetrics> descriptor)
        {
            descriptor.Field(m => m.RequestDurationMs).Type<FloatType>();
            descriptor.Field(m => m.StatusCode).Type<IntType>();
            descriptor.Field(m => m.RequestBodySize).Type<LongType>();
            descriptor.Field(m => m.ResponseBodySize).Type<LongType>();
            descriptor.Field(m => m.EndpointUrl).Type<StringType>();
            descriptor.Field(m => m.RetryAttempts).Type<IntType>();
        }
    }
}
