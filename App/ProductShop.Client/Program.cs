using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ProductShop.Client;
using ProductShop.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddSingleton<AuthState>();

// API er address wwwroot/appsettings.json theke ashe.
// Khali thakle (online / publish kora hole) je site theke app khulche, sheta-i API.
// AuthHandler protiti request e login token lagay.
var apiBaseUrl = builder.Configuration["ApiBaseUrl"];
if (string.IsNullOrWhiteSpace(apiBaseUrl)) apiBaseUrl = builder.HostEnvironment.BaseAddress;
builder.Services.AddScoped(sp => new HttpClient(new AuthHandler(sp.GetRequiredService<AuthState>()) { InnerHandler = new HttpClientHandler() })
{
    BaseAddress = new Uri(apiBaseUrl)
});
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<CatalogService>();
builder.Services.AddScoped<StockService>();
builder.Services.AddScoped<SaleService>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<RoleService>();
builder.Services.AddScoped<StoreService>();
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<WebOrderService>();
builder.Services.AddScoped<FinanceService>();

await builder.Build().RunAsync();
