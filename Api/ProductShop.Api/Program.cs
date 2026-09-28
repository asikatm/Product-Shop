using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ProductShop.Api.Data;
using ProductShop.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<PermissionService>();
builder.Services.AddScoped<SaleWriter>();

// Website theke order: ek IP theke minute e 5 tar beshi na (spam atkate).
// Cloudflare tunnel er pechone ashol IP "CF-Connecting-IP" header e thake.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("store-orders", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Request.Headers["CF-Connecting-IP"].FirstOrDefault() ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1) }));
});

// Login token (JWT) diye API protect kora
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = TokenService.SigningKey(builder.Configuration),
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role
        };
    });

// [AllowAnonymous] chara shob endpoint e login lagbe
builder.Services.AddAuthorization(options =>
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

// Non-nullable string (jemon InvoiceNo) ke automatic "required" dhorbe na
builder.Services.AddControllers(options =>
{
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
    options.Filters.Add<ActiveUserFilter>();
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Swagger e "Authorize" button diye token deya jabe
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "api/auth/login theke pawa token ta din"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

// Blazor client alada port e chole, tai CORS allow korte hobe
var clientUrl = builder.Configuration["ClientUrl"] ?? "http://localhost:5100";
builder.Services.AddCors(options =>
{
    options.AddPolicy("BlazorClient", policy =>
        policy.WithOrigins(clientUrl)
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

// Pending migration thakle automatic apply hobe (database na thakle toiri hobe)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    await DbSeeder.SeedAsync(db, scope.ServiceProvider.GetRequiredService<IPasswordHasher<AppUser>>());
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Product er chobi "uploads" folder e thake, login chara dekha jabe
var uploadsRoot = UploadPaths.Root(app.Configuration, app.Environment);
Directory.CreateDirectory(uploadsRoot);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsRoot),
    RequestPath = "/uploads"
});

// App (Blazor) er file: online e API ar App ek link e chole
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseCors("BlazorClient");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// API chara onno shob URL (jemon /login, /products) e App khulbe; login lage na
app.MapFallbackToFile("index.html").AllowAnonymous();

app.Run();
