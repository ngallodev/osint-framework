using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OsintBackend.Auth;

namespace OsintBackend.Controllers
{
    [ApiController]
    [Route("auth")]
    public class AuthController : ControllerBase
    {
        private readonly AuthSettings _settings;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IOptions<AuthSettings> options, ILogger<AuthController> logger)
        {
            _settings = options.Value;
            _logger = logger;
        }

        [HttpPost("token")]
        [AllowAnonymous]
        public ActionResult<TokenResponse> IssueToken([FromBody] TokenRequest request)
        {
            if (_settings.Development is null || !_settings.Development.EnableLocalIssuer)
            {
                return NotFound();
            }

            if (_settings.Development.Users is null || !_settings.Development.Users.Any())
            {
                _logger.LogWarning("Token requested but no users are configured.");
                return Unauthorized("Authentication is not configured.");
            }

            var user = _settings.Development.Users.FirstOrDefault(u =>
                u.Username.Equals(request.Username, StringComparison.OrdinalIgnoreCase) &&
                u.Password == request.Password); // NOTE: replace with hashed verification in production

            if (user is null)
            {
                _logger.LogWarning("Invalid credentials for user {Username}", request.Username);
                return Unauthorized("Invalid credentials.");
            }

            var keyBytes = Encoding.UTF8.GetBytes(_settings.Development.SigningKey ?? string.Empty);
            if (keyBytes.Length < 32)
            {
                _logger.LogError("Signing key length is insufficient. Must be at least 32 bytes.");
                return StatusCode(500, "Authentication configuration is invalid.");
            }

            var tokenHandler = new JwtSecurityTokenHandler();
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Username),
                    new Claim(ClaimTypes.Name, user.Username)
                }.Concat(user.Roles.Select(role => new Claim(ClaimTypes.Role, role)))),
                Expires = DateTime.UtcNow.AddHours(8),
                Issuer = _settings.Development.Issuer,
                Audience = _settings.Development.Audience,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            var jwt = tokenHandler.WriteToken(token);

            return Ok(new TokenResponse { AccessToken = jwt, ExpiresAt = tokenDescriptor.Expires });
        }
    }

    public class TokenRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class TokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public DateTime? ExpiresAt { get; set; }
    }
}
