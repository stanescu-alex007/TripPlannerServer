using Microsoft.AspNetCore.HttpOverrides;
using System.Text.Json;
using System.Text.Json.Serialization;
using TripPlanner.BusinessLogic.DependencyInjection;
using TripPlanner.Infrastructure.DependencyInjection;
using TripPlanner.WEB;
using TripPlanner.WEB.DependencyInjection;
using TripPlanner.WEB.Middleware;

var builder = WebApplication.CreateBuilder(args);

const string CorsPolicy = "AngularFrontend";

// Validated up front so a bad signing key fails at startup, not on first login.
var authSettings = BusinessLogicServiceCollectionExtensions.BuildAuthSettings(builder.Configuration);

// ── Services ──────────────────────────────────────────────────────────────────

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? ["http://localhost:4200"];

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()); // needed for the HttpOnly refresh cookie
});

builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        opts.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        // Model-binding failures must use the same {code, message} envelope as
        // everything else, instead of the framework's ValidationProblemDetails.
        options.InvalidModelStateResponseFactory = ApiValidation.BuildResponse;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerWithAuth();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddEmailSending(builder.Configuration);
builder.Services.AddJwtAuthentication(authSettings);
builder.Services.AddBusinessLogic(builder.Configuration);
builder.Services.AddAuthRateLimiting();

builder.Services.AddHostedService<ExpiredTokenCleanupService>();

// Behind a reverse proxy every request appears to come from the proxy. Without this
// the rate limiter partitions all callers into one bucket, so they throttle each
// other, and every session is recorded against the proxy's address.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    // Cleared because the defaults only trust loopback; the real proxy addresses
    // belong in configuration for the environment that has them.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// ── Pipeline ──────────────────────────────────────────────────────────────────

var app = builder.Build();

// Must run before anything that reads the client's address — rate limiting, logging,
// and the IP recorded on a session.
app.UseForwardedHeaders();

// Then the catch-all: turns any unhandled exception into {code, message} and keeps
// stack traces out of responses.
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// HTTPS redirection is deliberately not enabled in development: the launch profile only
// binds HTTP, so redirecting would send the SPA to a port that is not listening.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseCors(CorsPolicy);

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
