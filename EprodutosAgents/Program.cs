using EprodutosAgents.Agents;
using EprodutosAgents.Configuration;
using EprodutosAgents.Data;
using EprodutosAgents.Security;
using EprodutosAgents.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);
var accessProfileOptions = AccessProfileOptions.FromConfiguration(builder.Configuration);

builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services.AddSingleton(static serviceProvider =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    return MongoOptions.FromConfiguration(configuration);
});
builder.Services.AddSingleton(accessProfileOptions);

builder.Services.AddSingleton<EprodutosMongoContext>();
builder.Services.AddSingleton<IProductsAgent, ProductsAgent>();
builder.Services.AddSingleton<IStocksAgent, StocksAgent>();
builder.Services.AddSingleton<ICatalogAgent, CatalogAgent>();

var accessProfile = accessProfileOptions.Profile;
var toolTypes = accessProfile == AccessProfile.Employee
    ? [typeof(ProductTools), typeof(EmployeeStockTools), typeof(EmployeeCatalogTools)]
    : new[] { typeof(ProductTools), typeof(CustomerStockTools), typeof(CustomerCatalogTools) };

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools(toolTypes);

await builder.Build().RunAsync();
