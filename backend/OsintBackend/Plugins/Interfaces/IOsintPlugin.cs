using OsintBackend.Models;

namespace OsintBackend.Plugins.Interfaces
{
    public interface IOsintPlugin
    {
        string ToolName { get; }
        bool IsEnabled { get; }
        Task<OsintResult> ExecuteAsync(string target, string investigationType, CancellationToken cancellationToken);
        bool SupportsOperation(string operationType);
    }
}
