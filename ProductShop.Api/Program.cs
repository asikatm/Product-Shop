using Microsoft.EntityFrameworkCore;
using ProductShop.Api.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Non-nullable string (jemon InvoiceNo) ke automatic "required" dhorbe na
builder.Services.AddControllers(options =>
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("BlazorClient");
app.MapControllers();

app.Run();
