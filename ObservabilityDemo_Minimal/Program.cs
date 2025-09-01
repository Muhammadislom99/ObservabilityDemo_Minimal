using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle

// Добавляем контроллеры
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Настройка Entity Framework
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure OpenTelemetry with Exemplars support
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService("aspnet-core-api"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation(options =>
        {
            options.RecordException = true;
            options.EnrichWithHttpRequest = (activity, request) =>
            {
                activity.SetTag("http.request_content_length", request.ContentLength);
                activity.SetTag("http.scheme", request.Scheme);
                activity.SetTag("http.host", request.Host.ToString());
            };
        })
        .AddEntityFrameworkCoreInstrumentation(options =>
        {
            options.SetDbStatementForText = true;
            options.SetDbStatementForStoredProcedure = true;
        })
        .AddOtlpExporter(options => { options.Endpoint = new Uri("http://otel-collector:4317"); }))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter("Microsoft.AspNetCore.Hosting")
        .AddMeter("Microsoft.AspNetCore.Server.Kestrel")
        .AddMeter("System.Net.Http")
        .AddOtlpExporter(options => { options.Endpoint = new Uri("http://otel-collector:4317"); }));

// Настройка логирования с OpenTelemetry
builder.Logging.ClearProviders();
builder.Logging.AddOpenTelemetry(options =>
{
    options.SetResourceBuilder(ResourceBuilder.CreateDefault()
        .AddService("aspnet-core-api", "1.0.0"));

    options.AddOtlpExporter(otlpOptions =>
    {
        otlpOptions.Endpoint = new Uri(Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
                                       ?? "http://localhost:4317");
    });

    // Включаем корреляцию с трейсами
    options.IncludeScopes = true;
    options.IncludeFormattedMessage = true;
});

// Добавляем ActivitySource для кастомного трейсинга
builder.Services.AddSingleton(new ActivitySource("aspnet-core-api"));


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();


// Инициализация базы данных
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    context.Database.EnsureCreated();
}


app.UseRouting();
app.MapControllers();

app.Run();

// Entity Framework DbContext
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Product> Products { get; set; } = null!;
}

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}