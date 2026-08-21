using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using TibbiNav.Api.Authentication;
using TibbiNav.Api.Middleware;
using TibbiNav.Api.Services;
using TibbiNav.Application.Authorization;
using TibbiNav.Application.BulkImport;
using TibbiNav.Application.Documents;
using TibbiNav.Application.Employees;
using TibbiNav.Application.Onboarding;
using TibbiNav.Application.Recruitment;
using TibbiNav.Application.Workflow;
using TibbiNav.Infrastructure;
using TibbiNav.Infrastructure.Seed;

// Раздел 29: активируем Community-лицензию QuestPDF один раз при старте —
// без этого генерация PDF бросает исключение при первом вызове.
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

// Регистрируем встроенный Cyrillic-шрифт (см. TibbiNav.Api.csproj) под явным
// именем семейства — без этого QuestPDF молча теряет кириллические глифы на
// хостах без системных шрифтов с поддержкой кириллицы (типично для
// минимальных Linux-контейнеров). RegisterFontWithCustomName, а не
// RegisterFont(stream) — не полагаемся на то, как конкретно шрифт называет
// себя в своей internal name table (Regular/Bold регистрируются под одним и
// тем же именем "Noto Sans", см. PdfDocumentRenderer.FontFamily).
RegisterEmbeddedFont("TibbiNav.Api.Assets.Fonts.NotoSans-Regular.ttf");
RegisterEmbeddedFont("TibbiNav.Api.Assets.Fonts.NotoSans-Bold.ttf");

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

// --- Document Generator (раздел 29): DOCX (OpenXml) + PDF (QuestPDF) из общей
// модели блоков, файлы — на локальном диске (App:Documents:StoragePath),
// до появления настоящего S3-compatible хранилища (раздел 80). ---
var documentStoragePath = builder.Configuration["Documents:StoragePath"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "document-storage");
builder.Services.AddSingleton<IDocumentFileStorage>(_ => new LocalDiskDocumentFileStorage(documentStoragePath));
builder.Services.AddScoped<DocumentPlaceholderResolver>();
builder.Services.AddSingleton<DocxDocumentRenderer>();
builder.Services.AddSingleton<PdfDocumentRenderer>();
builder.Services.AddScoped<DocumentGeneratorService>();

// --- Bulk Import (раздел 63) ---
builder.Services.AddScoped<IBulkImportDefinition, StaffingScheduleImportDefinition>();
builder.Services.AddScoped<IBulkImportDefinition, EmployeeImportDefinition>();
builder.Services.AddScoped<BulkImportService>();
builder.Services.AddScoped<BulkImportTemplateGenerator>();

// --- Workflow Engine (раздел 68): настраиваемые маршруты согласования —
// подключённые сущности-триггеры (Vacancy, LeaveRequest) регистрируются как
// IWorkflowEntityAdapter; сам движок про них ничего не хардкодит. ---
builder.Services.AddScoped<IWorkflowEntityAdapter, VacancyWorkflowAdapter>();
builder.Services.AddScoped<IWorkflowEntityAdapter, LeaveRequestWorkflowAdapter>();
builder.Services.AddScoped<WorkflowDefinitionSelector>();
builder.Services.AddScoped<IWorkflowApproverResolver, WorkflowApproverResolver>();
builder.Services.AddScoped<WorkflowEngine>();
builder.Services.AddHostedService<WorkflowEscalationHostedService>();

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

// Раздел 29: грузит шрифт из embedded resource сборки TibbiNav.Api и
// регистрирует его в QuestPDF.Drawing.FontManager под именем "Noto Sans"
// (см. PdfDocumentRenderer.FontFamily) — RegisterFontWithCustomName, а не
// RegisterFont(stream), чтобы не зависеть от того, как шрифт называет себя
// изнутри.
static void RegisterEmbeddedFont(string resourceName)
{
    using var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
        ?? throw new InvalidOperationException($"Embedded-шрифт не найден: {resourceName}");
    QuestPDF.Drawing.FontManager.RegisterFontWithCustomName("Noto Sans", stream);
}
