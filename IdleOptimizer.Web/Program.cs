using IdleOptimizer.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using IdleOptimizer;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Register services
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddMudServices();
builder.Services.AddScoped<ILocalStorageService, LocalStorageService>();
builder.Services.AddScoped<INumberFormattingService, NumberFormattingService>();
builder.Services.AddScoped<IUpgradeTimerService, UpgradeTimerService>();
builder.Services.AddScoped<IProductionService, ProductionService>();
builder.Services.AddScoped<IValuationService, ValuationService>();
builder.Services.AddScoped<IUpgradeEvaluationService, UpgradeEvaluationService>();
builder.Services.AddScoped<ICalculationService, CalculationService>();

await builder.Build().RunAsync();