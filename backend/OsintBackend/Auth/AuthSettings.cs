using System.Collections.Generic;

namespace OsintBackend.Auth
{
    public class AuthSettings
    {
        public Auth0Settings Auth0 { get; set; } = new();
        public DevelopmentAuthSettings Development { get; set; } = new();
        public List<string> ApiKeys { get; set; } = new();
    }

    public class Auth0Settings
    {
        public bool Enabled { get; set; } = false;
        public string Domain { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;
    }

    public class DevelopmentAuthSettings
    {
        public bool EnableLocalIssuer { get; set; } = true;
        public string Issuer { get; set; } = "osint-framework";
        public string Audience { get; set; } = "osint-clients";
        public string SigningKey { get; set; } = string.Empty;
        public List<AuthUser> Users { get; set; } = new();
    }

    public class AuthUser
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = new();
    }

    public static class AuthConstants
    {
        public const string ApiKeyScheme = "ApiKey";
        public const string ApiKeyHeaderName = "X-API-Key";
    }
}
