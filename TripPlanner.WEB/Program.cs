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
builder.Services.AddHostedService<TwoFactorSecretProtectionBackfill>();
builder.Services.AddHostedService<OwnershipOfferExpiryService>();
builder.Services.AddHostedService<DeletedTripPurgeService>();

// Which proxies may speak for the client. Configured per environment; with nothing
// configured the forwarded header is ignored, so it cannot be used to fake an address.
builder.Services.AddTrustedProxies(builder.Configuration);

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

app.UseAuthentication();

// After authentication so per-user policies (contact) can see who is calling; the
// IP-keyed auth policies are unaffected by the order.
app.UseRateLimiter();

app.UseAuthorization();

app.MapControllers();

app.Run();
