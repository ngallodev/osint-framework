using System.Net;

namespace OsintBackend.Services
{
    public class OllamaCompletionResult
    {
        public OllamaResponse Response { get; set; } = new();
        public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;
        public int AttemptCount { get; set; } = 1;
        public int RequestBodyByteCount { get; set; }
        public int ResponseBodyByteCount { get; set; }
        public Uri? RequestedUri { get; set; }
    }
}
