using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;
using Microsoft.Extensions.Options;
using OsintBackend.Models;
using OsintBackend.Services;
using System.Diagnostics;

namespace OsintBackend.GraphQL.Queries
{
    [Authorize]
    [ExtendObjectType(OperationTypeNames.Query)]
    public class OllamaQueries
    {
        public async Task<OllamaHealth> GetOllamaHealthAsync(
            [Service] IOllamaService ollamaService,
            [Service] IOptionsMonitor<OllamaSettings> settingsMonitor,
            CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            var baseUrl = settingsMonitor.CurrentValue.BaseUrl;
            var health = new OllamaHealth
            {
                BaseUrl = baseUrl,
                CheckedAt = DateTime.UtcNow
            };

            try
            {
                var available = await ollamaService.IsServiceAvailableAsync();
                health.IsAvailable = available;

                if (available)
                {
                    health.Models = await ollamaService.GetAvailableModelsAsync();
                    health.StatusMessage = "Ollama responded successfully.";
                }
                else
                {
                    health.StatusMessage = "Ollama endpoint responded with a non-success status.";
                }
            }
            catch (Exception ex)
            {
                health.IsAvailable = false;
                health.StatusMessage = ex.Message;
            }
            finally
            {
                stopwatch.Stop();
                health.LatencyMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
            }

            return health;
        }
    }
}
