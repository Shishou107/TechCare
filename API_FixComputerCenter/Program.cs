using API_FixComputerCenter;
using API_FixComputerCenter.Endpoints;
using API_FixComputerCenter.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Register TechCare Clean Architecture & Security Services
builder.Services.AddTechCareBackend(builder.Configuration);
builder.Services.AddOpenApi();

var app = builder.Build();

// 1. Exception Handling & Security Headers Middlewares (Defense-in-depth)
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();

// 2. OpenAPI / Swagger Documentation
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// 3. CORS & Rate Limiting
app.UseCors("TechCareCorsPolicy");
app.UseRateLimiter();

// 4. Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// 5. Map Modular Feature Endpoints
app.MapTechCareEndpoints();

app.Run();
