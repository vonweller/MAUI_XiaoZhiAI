using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using XiaoZhiSharpMAUI.Shared.Services;
using XiaoZhiSharpMAUI.Web.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Add device-specific services used by the XiaoZhiSharpMAUI.Shared project
builder.Services.AddSingleton<IFormFactor, FormFactor>();

await builder.Build().RunAsync();
