using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using WasmClient;
using WasmClient.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Services registration
builder.Services.AddSingleton<SettingsService>();
builder.Services.AddSingleton<BattleHistoryService>();
builder.Services.AddSingleton<IConnectionFactory, ConnectionFactory>();
builder.Services.AddSingleton<BattleSessionManager>();
builder.Services.AddSingleton<RealtimeUpdateService>();
builder.Services.AddSingleton<FieldPerformanceMonitor>();

// Logging
builder.Services.AddLogging();

await builder.Build().RunAsync();
