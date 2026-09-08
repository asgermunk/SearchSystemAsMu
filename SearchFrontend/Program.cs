using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SearchFrontend;
using SearchFrontend.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// where the search API lives - see wwwroot/appsettings.json
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5222/";

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });
builder.Services.AddScoped<SearchApiClient>();

await builder.Build().RunAsync();
