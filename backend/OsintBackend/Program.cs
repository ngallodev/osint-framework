using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using HotChocolate.Types;
using OsintBackend.Auth;
using OsintBackend.Data;
using OsintBackend.Services;
using OsintBackend.Plugins.Interfaces;
using OsintBackend.Plugins.Implementations;
using OsintBackend.GraphQL;
using OsintBackend.GraphQL.Mutations;
using OsintBackend.GraphQL.Queries;
using OsintBackend.GraphQL.Types;
using OsintBackend.Models;

var builder = WebApplication.CreateBuilder(args);

// Load .local configuration files (for local development with secrets)
// Pattern: appsettings.json -> appsettings.local.json (overrides)
//          appsettings.Development.json -> appsettings.local.Development.json (overrides)
var env = builder.Environment.EnvironmentName;
var basePath = builder.Environment.ContentRootPath;

// Add base appsettings files
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{env}.json", optional: true, reloadOnChange: true);

// Add .local overrides (these files should NOT be in git)
var localSettingsFile = System.IO.Path.Combine(basePath, "appsettings.local.json");
if (System.IO.File.Exists(localSettingsFile))
{
    builder.Configuration.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);
}

var localEnvSettingsFile = System.IO.Path.Combine(basePath, $"appsettings.local.{env}.json");
if (System.IO.File.Exists(localEnvSettingsFile))
{
    builder.Configuration.AddJsonFile($"appsettings.local.{env}.json", optional: true, reloadOnChange: true);
}

// Add environment variables (highest priority)
builder.Configuration.AddEnvironmentVariables();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database
builder.Services.AddDbContext<OsintDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        new MySqlServerVersion(new Version(8, 0, 0))
    ));

// Configuration
builder.Services.Configure<OllamaSettings>(
    builder.Configuration.GetSection("Ollama"));
builder.Services.Configure<SpiderFootConfig>(
    builder.Configuration.GetSection("Tools:SpiderFoot"));
builder.Services.Configure<SherlockConfig>(
    builder.Configuration.GetSection("Tools:Sherlock"));
builder.Services.Configure<AiJobQueueOptions>(
    builder.Configuration.GetSection("AiJobQueue"));
builder.Services.Configure<AuthSettings>(
    builder.Configuration.GetSection("Auth"));

var authSettings = new AuthSettings();
builder.Configuration.GetSection("Auth").Bind(authSettings);
authSettings.Auth0 ??= new Auth0Settings();
authSettings.Development ??= new DevelopmentAuthSettings();

var auth0Settings = authSettings.Auth0;
var developmentSettings = authSettings.Development;

var useAuth0 = auth0Settings.Enabled &&
               !string.IsNullOrWhiteSpace(auth0Settings.Domain) &&
               !string.IsNullOrWhiteSpace(auth0Settings.Audience);
var useLocalIssuer = !useAuth0 &&
                     developmentSettings.EnableLocalIssuer &&
                     !string.IsNullOrWhiteSpace(developmentSettings.SigningKey);

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        if (useAuth0)
        {
            var authority = auth0Settings.Domain.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? auth0Settings.Domain
                : $"https://{auth0Settings.Domain}";

            options.Authority = authority.TrimEnd('/');
            options.Audience = auth0Settings.Audience;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                NameClaimType = ClaimTypes.NameIdentifier,
                RoleClaimType = ClaimTypes.Role,
                ValidateIssuer = true,
                ValidIssuer = options.Authority
            };
        }
        else if (useLocalIssuer)
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = developmentSettings.Issuer,
                ValidateAudience = true,
                ValidAudience = developmentSettings.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(developmentSettings.SigningKey)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(2),
                NameClaimType = ClaimTypes.NameIdentifier,
                RoleClaimType = ClaimTypes.Role
            };
            options.RequireHttpsMetadata = false;
        }
        else
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateIssuerSigningKey = false,
                ValidateLifetime = false
            };
        }
    })
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(AuthConstants.ApiKeyScheme, _ => { });

builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new AuthorizationPolicyBuilder(
            JwtBearerDefaults.AuthenticationScheme,
            AuthConstants.ApiKeyScheme)
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var key = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 60,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 10
        });
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// HTTP Client configuration for Ollama service
Action<HttpClient> configureHttpClient = client =>
{
    var baseUrl = builder.Configuration["Ollama:BaseUrl"] ?? "http://ollama:11434";
    client.BaseAddress = new Uri(baseUrl);
    var timeoutSeconds = builder.Configuration.GetValue<int?>("Ollama:TimeoutSeconds") ?? 300;
    client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
};

// Register Ollama service based on configuration
// Configuration option: "Ollama:ServiceType" = "remote" | "local" (default: "local")
var serviceType = builder.Configuration["Ollama:ServiceType"] ?? "local";

if (serviceType.Equals("remote", StringComparison.OrdinalIgnoreCase))
{
    // Remote service: aggressive retry logic for unreliable network connections
    builder.Services.AddHttpClient<RemoteOllamaService>(configureHttpClient);
    builder.Services.AddScoped<IOllamaService>(sp => sp.GetRequiredService<RemoteOllamaService>());
}
else
{
    // Local service: standard error handling for local Ollama instance
    builder.Services.AddHttpClient<OllamaService>(configureHttpClient);
    builder.Services.AddScoped<IOllamaService>(sp => sp.GetRequiredService<OllamaService>());
}

// HTTP Client for SpiderFoot
builder.Services.AddHttpClient<SpiderFootClient>((sp, client) =>
{
    var config = sp.GetRequiredService<IOptionsMonitor<SpiderFootConfig>>().CurrentValue;
    var baseUrl = config.Url ?? "http://localhost:5001";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);
});

// External Tool Services
builder.Services.AddScoped<SpiderFootService>();
builder.Services.AddScoped<SherlockService>();
builder.Services.AddScoped<IToolServiceFactory, ToolServiceFactory>();

// Register tool services with factory
builder.Services.AddScoped(sp =>
{
    var factory = sp.GetRequiredService<IToolServiceFactory>();
    var spiderfoot = sp.GetRequiredService<SpiderFootService>();
    var sherlock = sp.GetRequiredService<SherlockService>();

    factory.Register(spiderfoot.ToolName, spiderfoot);
    factory.Register(sherlock.ToolName, sherlock);

    return factory;
});

builder.Services.AddScoped<ToolOrchestrationService>();

// Services
builder.Services.AddScoped<AiAnalysisPlugin>();
builder.Services.AddScoped<IAiJobQueueService, AiJobQueueService>();

// Background workers
builder.Services.AddHostedService<AiJobBackgroundService>();

// Plugins
builder.Services.AddScoped<IOsintPlugin, AiAnalysisPlugin>();
// Add other plugins here (SpiderFoot, Sherlock, etc.)

// GraphQL
builder.Services
    .AddGraphQLServer()
    .AddAuthorization()
    .AddQueryType<Query>()
    .AddMutationType<Mutation>()
    .AddTypeExtension<InvestigationQueries>()
    .AddTypeExtension<ResultQueries>()
    .AddTypeExtension<ToolQueries>()
    .AddTypeExtension<AiJobQueries>()
    .AddTypeExtension<OllamaQueries>()
    .AddTypeExtension<InvestigationMutations>()
    .AddTypeExtension<ResultMutations>()
    .AddTypeExtension<AiMutations>()
    .AddTypeExtension<ToolMutations>()
    .AddTypeExtension<AiJobMutations>()
    .AddType<InvestigationType>()
    .AddType<ResultType>()
    .AddType<AiJobGraphType>()
    .AddType<AiJobStructuredResultGraphType>()
    .AddType<AiJobStructuredResultSectionGraphType>()
    .AddType<AiJobErrorInfoGraphType>()
    .AddType<AiJobDebugInfoGraphType>()
    .AddType<OllamaDebugMetricsGraphType>()
    .AddType<HttpDebugMetricsGraphType>()
    .AddType<OllamaHealthType>()
    .AddType<EnumType<AiJobStatus>>()
    .AddFiltering()
    .AddSorting()
    .AddProjections();

var app = builder.Build();

// Apply database migrations automatically on startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<OsintDbContext>();
    var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
    var logger = loggerFactory.CreateLogger("Startup");
    try
    {
        dbContext.Database.Migrate();
        logger.LogInformation("Database migrations applied successfully.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error applying database migrations");
        throw;
    }
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
}

app.Use(async (context, next) =>
{
    context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
    context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
    context.Response.Headers.TryAdd("Referrer-Policy", "no-referrer");
    context.Response.Headers.TryAdd("X-XSS-Protection", "0");
    await next();
});

app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGraphQL();

app.Run();
