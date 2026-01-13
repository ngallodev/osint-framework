using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using OsintBackend.Models;

namespace OsintBackend.Services
{
    public static class AiJobErrorClassifier
    {
        public static AiJobErrorInfo FromException(Exception exception)
        {
            var errorInfo = new AiJobErrorInfo
            {
                Message = exception.Message,
                Code = "unexpected_error",
                Details = exception.InnerException?.Message ?? exception.Message,
                IsRetryable = false,
                OccurredAt = DateTime.UtcNow,
                Metadata = new Dictionary<string, string>()
            };

            switch (exception)
            {
                case OllamaServiceException ollamaEx:
                    errorInfo.Message = ollamaEx.Message;
                    errorInfo.Code = ollamaEx.ErrorCode ?? "ollama_error";
                    errorInfo.IsRetryable = ollamaEx.IsRetryable;
                    errorInfo.Details = ollamaEx.InnerException?.Message ?? ollamaEx.Message;
                    if (ollamaEx.Metadata is { Count: > 0 })
                    {
                        errorInfo.Metadata = new Dictionary<string, string>(ollamaEx.Metadata);
                    }
                    break;

                case TaskCanceledException timeoutEx:
                    errorInfo.Message = "The request to the AI service timed out.";
                    errorInfo.Code = "ollama_timeout";
                    errorInfo.IsRetryable = true;
                    errorInfo.Details = timeoutEx.Message;
                    break;

                case HttpRequestException httpEx:
                    errorInfo.Message = "Network error while contacting the AI service.";
                    errorInfo.Code = "ollama_network";
                    errorInfo.IsRetryable = true;
                    errorInfo.Details = httpEx.Message;
                    if (httpEx.StatusCode.HasValue)
                    {
                        errorInfo.Metadata ??= new Dictionary<string, string>();
                        errorInfo.Metadata["statusCode"] = ((int)httpEx.StatusCode.Value).ToString();
                        errorInfo.Metadata["statusName"] = httpEx.StatusCode.Value.ToString();
                    }
                    break;

                case JsonException jsonEx:
                    errorInfo.Message = "Unable to parse the AI service response.";
                    errorInfo.Code = "ollama_parse_error";
                    errorInfo.IsRetryable = false;
                    errorInfo.Details = jsonEx.Message;
                    break;
            }

            if (errorInfo.Metadata is { Count: 0 })
            {
                errorInfo.Metadata = null;
            }

            return errorInfo;
        }
    }
}
