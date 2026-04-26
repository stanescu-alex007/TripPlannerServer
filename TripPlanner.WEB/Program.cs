using TripPlanner.BusinessLogic.DependencyInjection;
using TripPlanner.Infrastructure.DependencyInjection;
using TripPlanner.WEB.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

const string AngularOrigin = "AngularFrontend";

// ── Services ──────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy(AngularOrigin, policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // required for HttpOnly cookie refresh tokens
    });
});

builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        // Serialize enums as camelCase strings and keep property names camelCase
        opts.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        opts.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerWithAuth();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddBusinessLogic();

// ── Pipeline ──────────────────────────────────────────────────────────────
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors(AngularOrigin);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
