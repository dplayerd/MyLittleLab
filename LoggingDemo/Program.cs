//using LoggingDemo.Logging;

//var builder = WebApplication.CreateBuilder(args);

//builder.Logging.ClearProviders();
//builder.Logging.AddConsole();
//builder.Logging.AddProvider(new MemoryLogProvider());

//builder.Services.AddControllers();

//var app = builder.Build();

//app.MapControllers();

//app.Run();


using LoggingDemo.Logging;
using LoggingDemo.Services;
using System.Text.Json.Serialization;


var builder = WebApplication.CreateBuilder(args);

// Logging
builder.Logging.ClearProviders();

// Add Console
builder.Logging.AddConsole();
builder.Logging.AddProvider(new MemoryLogProvider());

// Add File provider (自訂)
var logFilePath = Path.Combine(builder.Environment.ContentRootPath, "logs", $"app_{DateTime.Today.ToString("yyyyMMdd")}.log");
builder.Logging.AddProvider(new FileLogProvider(logFilePath));

// Provider-specific filters:
// - ConsoleProvider 只接受 Trace 或 Debug
builder.Logging.AddFilter<Microsoft.Extensions.Logging.Console.ConsoleLoggerProvider>(
    (category, level) => level == LogLevel.Trace || level == LogLevel.Debug);

// - File provider 接受 Information 以上（含 Information, Warning, Error, Critical）
builder.Logging.AddFilter<FileLogProvider>(
    (category, level) => level >= LogLevel.Information);


// Add services
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());    //加入 JSON 序列化
});

// ⭐ 加入 Swagger 服務
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Logging Demo API", Version = "v1" });
});


// 註冊各 Service
builder.Services.AddScoped<FakeBusinessService>();





var app = builder.Build();

// ⭐ 啟用 Swagger UI（建議開發環境使用）
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();
