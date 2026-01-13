namespace OsintBackend.GraphQL.InputTypes
{
    public class QueueAiJobInput
    {
        public int InvestigationId { get; set; }
        public string JobType { get; set; } = Models.AiJobTypes.Analysis;
        public string? Model { get; set; }
        public string? PromptOverride { get; set; }

        /// <summary>
        /// Enable debug mode to capture detailed Ollama metrics, timing, and full prompt
        /// </summary>
        public bool Debug { get; set; } = false;
    }
}
