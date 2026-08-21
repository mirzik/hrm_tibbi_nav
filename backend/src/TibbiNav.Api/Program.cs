using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using TibbiNav.Api.Authentication;
using TibbiNav.Api.Middleware;
using TibbiNav.Application.Authorization;
using TibbiNav.Application.BulkImport;
using TibbiNav.Application.Employees;
using TibbiNav.Application.Onboarding;
using TibbiNav.Application.Recruitment;
using TibbiNav.Infrastructure;
using TibbiNav.Infrastructure.Seed;

var builder = WebApplication.CreateBuilder(args);

// --- DB (раздел 83: PostgreSQL) ---
builder.Services.AddDbContext<TibbiNavDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// --- Application services ---
builder.Services.AddScoped<EmployeeCodeGenerator>();
builder.Services.AddScoped<HireCandidateService>();

// --- Onboarding (раздел 25, 31-32) ---
builder.Services.AddScoped<OnboardingChecklistTemplateSelector>();
builder.Services.AddScoped<OnboardingChecklistService>();

// --- Bulk Import (раздел 63) ---
builder.Services.AddScoped<IBulkImportDefinition, StaffingScheduleImportDefinition>();
builder.Services.AddScoped<IBulkImportDefinition, EmployeeImportDefinition>();
builder.Services.AddScoped<BulkImportService>();
builder.Services.AddScoped<BulkImportTemplateGenerator>();

// --- RBAC (раздел 65): резолвер Permission + носитель ScopeContext на запрос ---
builder.Services.AddScoped<IScopeContextResolver, ScopeContextResolver>();
builder.Services.AddScoped<IScopeContextAccessor, ScopeContextAccessor>();

// --- Auth: OAuth2/OIDC-ready, раздел 79. В проде — bearer JWT от Keycloak/аналога.
// В Development, пока Keycloak не поднят (roadmap-пункт 10 в README), схема по
// умолчанию — DevHeaderAuthenticationHandler (заголовок X-Dev-User), чтобы можно
// было руками проверить ScopeFilterMiddleware/RBAC без реального IdP. ---
var authBuilder = builder.Services.AddAuthentication(builder.Environment.IsDevelopment()
    ? DevHeaderAuthenticationHandler.SchemeName
    : JwtBearerDefaults.AuthenticationScheme);

authBuilder.AddJwtBearer(opt =>
{
    opt.Authority = builder.Configuration["Auth:Authority"];
    opt.Audience = builder.Configuration["Auth:Audience"];
    opt.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
});

if (builder.Environment.IsDevelopment())
{
    authBuilder.AddScheme<AuthenticationSchemeOptions, DevHeaderAuthenticationHandler>(
        DevHeaderAuthenticationHandler.SchemeName, _ => { });
}

builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(); // раздел 82: OpenAPI/Swagger обязателен

builder.Services.AddCors(opt => opt.AddDefaultPolicy(p =>
    p.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
     .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<ScopeFilterMiddleware>(); // раздел 65: RBAC enforcement, после Auth*, до MapControllers
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "tibbi-nav-hrm-api" }));

// Демо-данные для локальной проверки RBAC (см. DevSeedData) — только Development,
// идемпотентно, не блокирует запуск API, если БД ещё не готова.
if (app.Environment.IsDevelopment())
{
    try
    {
        using var scope = app.Services.CreateScope();
        var seedDb = scope.ServiceProvider.GetRequiredService<TibbiNavDbContext>();
        await DevSeedData.SeedAsync(seedDb);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Dev-сидирование пропущено (БД недоступна или миграции не применены)");
    }
}

app.Run();
