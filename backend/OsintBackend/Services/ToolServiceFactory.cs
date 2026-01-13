using System;
using Microsoft.Extensions.Logging;

namespace OsintBackend.Services
{
    /// <summary>
    /// Factory for resolving external tool services by name
    /// Supports registration of multiple tool implementations
    /// </summary>
    public interface IToolServiceFactory
    {
        /// <summary>
        /// Resolves a tool service by name
        /// </summary>
        IExternalToolService? ResolveService(string toolName);

        /// <summary>
        /// Registers a tool service
        /// </summary>
        void Register(string toolName, IExternalToolService service);

        /// <summary>
        /// Gets all registered tool names
        /// </summary>
        IEnumerable<string> GetAvailableTools();
    }

    /// <summary>
    /// Default implementation of tool service factory
    /// </summary>
    public class ToolServiceFactory : IToolServiceFactory
    {
        private readonly Dictionary<string, IExternalToolService> _services = new();
        private readonly ILogger<ToolServiceFactory> _logger;

        public ToolServiceFactory(ILogger<ToolServiceFactory> logger)
        {
            _logger = logger;
        }

        public IExternalToolService? ResolveService(string toolName)
        {
            if (string.IsNullOrWhiteSpace(toolName))
            {
                _logger.LogWarning("Tool name is empty");
                return null;
            }

            var normalized = toolName.ToLowerInvariant();
            if (_services.TryGetValue(normalized, out var service))
            {
                return service;
            }

            _logger.LogWarning("Tool service '{ToolName}' not found. Available: {AvailableTools}",
                toolName, string.Join(", ", _services.Keys));
            return null;
        }

        public void Register(string toolName, IExternalToolService service)
        {
            if (string.IsNullOrWhiteSpace(toolName) || service == null)
            {
                throw new ArgumentException("Tool name and service cannot be null");
            }

            var normalized = toolName.ToLowerInvariant();
            _services[normalized] = service;
            _logger.LogInformation("Registered tool service: {ToolName}", toolName);
        }

        public IEnumerable<string> GetAvailableTools() => _services.Keys;
    }
}
