using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OsintBackend.Auth
{
    public class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        private readonly AuthSettings _settings;

#pragma warning disable CS0618
        public ApiKeyAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISystemClock clock,
            IOptions<AuthSettings> authOptions)
            : base(options, logger, encoder, clock)
#pragma warning restore CS0618
        {
            _settings = authOptions.Value;
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(AuthConstants.ApiKeyHeaderName, out var apiKeyHeaderValues))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var providedKey = apiKeyHeaderValues.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(providedKey))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            if (_settings.ApiKeys is null || !_settings.ApiKeys.Contains(providedKey))
            {
                return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "api-key-user"),
                new Claim(ClaimTypes.Name, "ApiKeyUser"),
                new Claim(ClaimTypes.Role, "Service")
            };

            var identity = new ClaimsIdentity(claims, AuthConstants.ApiKeyScheme);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, AuthConstants.ApiKeyScheme);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
