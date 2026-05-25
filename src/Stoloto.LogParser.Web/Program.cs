using Stoloto.LogParser.Core.Parsers;
using Stoloto.LogParser.Core.Sources;
using Stoloto.LogParser.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<LogParserFactory>();
builder.Services.AddSingleton<LocalLogSource>();
builder.Services.AddSingleton<SettingsService>();
builder.Services.AddScoped<LogQueryService>();
builder.Services.AddSingleton<LiveLogService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<LiveLogService>());
builder.Services.AddControllers();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();

app.Run();
