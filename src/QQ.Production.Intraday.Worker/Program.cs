using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Infrastructure.Simulator;
using QQ.Production.Intraday.Worker;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton(SeedData.Create());
builder.Services.AddSingleton<IIntradayRepository, InMemoryIntradayRepository>();
builder.Services.AddSingleton(new FakeLmaxOptions());
builder.Services.AddSingleton<IVenueExecutionGateway, FakeLmaxGateway>();
builder.Services.AddSingleton<IBrokerPositionProvider, FakeBrokerPositionProvider>();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<ProcessModelRunService>();
builder.Services.AddHostedService<Worker>();
builder.Services.AddSerilog(new LoggerConfiguration().WriteTo.Console().CreateLogger());

var host = builder.Build();
host.Run();
