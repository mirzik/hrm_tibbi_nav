using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using TibbiNav.Application.Employees;
using TibbiNav.Application.Recruitment;
using TibbiNav.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// --- DB (раздел 83: PostgreSQL) ---
builder.Services.AddDbContext<TibbiNavDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// --- Application services ---
builder.Services.AddScoped<EmployeeCodeGenerator>();
builder.Services.AddScoped<HireCandidateService>();

// --- Auth: OAuth2/OIDC-ready, раздел 79. В dev — bearer JWT от Keycloak/аналога. ---
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.Authority = builder.Configuration["Auth:Authority"];
        opt.Audience = builder.Configuration["Auth:Audience"];
        opt.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    });
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
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "tibbi-nav-hrm-api" }));

app.Run();
