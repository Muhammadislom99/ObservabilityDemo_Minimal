using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
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

// Настройка OpenTelemetry
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService("aspnet-core-api", "1.0.0"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation(options =>
        {
            // Записываем подробную информацию о HTTP запросах
            options.RecordException = true;
            options.EnrichWithHttpRequest = (activity, request) =>
            {
                activity.SetTag("http.request.header.user_agent", request.Headers.UserAgent.ToString());
            };
            options.EnrichWithHttpResponse = (activity, response) =>
            {
                activity.SetTag("http.response.status_code", response.StatusCode);
            };
        })
        .AddEntityFrameworkCoreInstrumentation(options =>
        {
            // Включаем детальное логирование SQL запросов
            options.SetDbStatementForText = true;
            options.SetDbStatementForStoredProcedure = true;
            options.EnrichWithIDbCommand = (activity, command) =>
            {
                // Добавляем дополнительную информацию о SQL запросах
                activity.SetTag("db.statement", command.CommandText);
                activity.SetTag("db.operation", command.CommandType.ToString());
            };
        })
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri(Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
                                       ?? "http://localhost:4317");
        }))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddProcessInstrumentation()
        // Добавляем exemplars для связи с трейсами
        .AddView("http_request_duration", new ExplicitBucketHistogramConfiguration
        {
            Boundaries = new double[] { 0.01, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10 }
        })
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri(Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
                                       ?? "http://localhost:4317");
        }));

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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
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