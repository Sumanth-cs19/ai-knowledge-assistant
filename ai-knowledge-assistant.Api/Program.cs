using System.Text;
using ai_knowledge_assistant.Api.Authorization;
using ai_knowledge_assistant.Api.Endpoints;
using ai_knowledge_assistant.Api.Health;
using ai_knowledge_assistant.Api.Middleware;
using ai_knowledge_assistant.Api.OpenApi;
using ai_knowledge_assistant.Application;
using ai_knowledge_assistant.Application.Common;
using ai_knowledge_assistant.Infrastructure;
using ai_knowledge_assistant.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using ai_knowledge_assistant.Domain.Common;
using Microsoft.OpenApi.Models;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Trace;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);
if (int.TryParse(Environment.GetEnvironmentVariable("PORT"), out var renderPort))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{renderPort}");
}

if (!builder.Environment.IsEnvironment("Test"))
{
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console());
}

var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
    ?? throw new InvalidOperationException("JWT settings are not configured.");

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "AI Knowledge Assistant API",
        Version = "v1",
        Description = "Document ingestion, semantic search, and RAG chat endpoints."
    });

    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Enter a JWT bearer token.",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id = JwtBearerDefaults.AuthenticationScheme
        }
    };

    options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, securityScheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [securityScheme] = []
    });
    options.OperationFilter<SwaggerExamplesOperationFilter>();
});
builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), tags: ["live"])
    .AddCheck<PostgreSqlHealthCheck>("postgresql", tags: ["ready"])
    .AddCheck<AIProviderConfigurationHealthCheck>("ai-provider-configuration", tags: ["ready"])
    .AddCheck<StorageHealthCheck>("upload-storage", tags: ["ready"]);
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .AddSource(Observability.ActivitySourceName)
            .AddSource("Npgsql")
            .AddAspNetCoreInstrumentation();
    });

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddRateLimiter(options =>
{
    options.OnRejected = async (context, cancellationToken) =>
    {
        var logger = context.HttpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("RateLimiting");

        logger.LogWarning(
            "Rate limit rejected request {Method} {Path}",
            context.HttpContext.Request.Method,
            context.HttpContext.Request.Path);

        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            title = "Too Many Requests",
            status = StatusCodes.Status429TooManyRequests,
            detail = "The chat rate limit has been exceeded."
        }, cancellationToken);
    };

    options.AddFixedWindowLimiter("chat", limiterOptions =>
    {
        limiterOptions.PermitLimit = builder.Configuration.GetValue("RateLimiting:Chat:PermitLimit", 20);
        limiterOptions.Window = TimeSpan.FromMinutes(builder.Configuration.GetValue("RateLimiting:Chat:WindowMinutes", 1));
        limiterOptions.QueueLimit = 0;
    });
});
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SigningKey)),
            ClockSkew = TimeSpan.Zero
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthorizationPolicies.RequireAdmin, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole(DefaultRoles.Admin);
    });

    options.AddPolicy(AuthorizationPolicies.RequireAuthenticatedUser, policy =>
    {
        policy.RequireAuthenticatedUser();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseMiddleware<CorrelationIdMiddleware>();
if (!app.Environment.IsEnvironment("Test"))
{
    app.UseSerilogRequestLogging();
}
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Api-Version"] = "v1";
    await next();
});

var swaggerEnabled = app.Environment.IsDevelopment()
    || app.Configuration.GetValue<bool>("Swagger:Enabled");

if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

var healthOptions = new HealthCheckOptions
{
    ResponseWriter = HealthResponseWriter.WriteAsync
};
var readinessOptions = new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
    ResponseWriter = HealthResponseWriter.WriteAsync
};
var livenessOptions = new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("live"),
    ResponseWriter = HealthResponseWriter.WriteAsync
};

app.MapHealthChecks("/health", healthOptions)
    .WithName("HealthCheck")
    .WithOpenApi();
app.MapHealthChecks("/health/ready", readinessOptions)
    .WithName("ReadinessCheck")
    .WithOpenApi();
app.MapHealthChecks("/health/live", livenessOptions)
    .WithName("LivenessCheck")
    .WithOpenApi();

app.MapAuthEndpoints();
app.MapDocumentEndpoints();
app.MapSearchEndpoints();
app.MapChatEndpoints();
app.MapConversationEndpoints();
app.MapAdminEndpoints();
app.MapAdminAnalyticsEndpoints();

app.MapAuthEndpoints("/api/v1/auth", "V1");
app.MapDocumentEndpoints("/api/v1/documents", "V1");
app.MapSearchEndpoints("/api/v1/search", "V1");
app.MapChatEndpoints("/api/v1/chat", "V1");
app.MapConversationEndpoints("/api/v1/conversations", "V1");
app.MapAdminEndpoints("/api/v1/admin", "V1");

try
{
    app.Run();
}
catch (Exception exception)
{
    Log.Fatal(exception, "API host terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;
