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
    .ConfigureResource(r => r.AddService("aspnet-core-api", serviceVersion: "1.0.0"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation(o => { o.RecordException = true; })
        .AddHttpClientInstrumentation()
        .AddSqlClientInstrumentation(o =>
        {
            o.SetDbStatementForText = true;
            o.RecordException = true;
        })
        // важно: OTLP на коллектор
        .AddOtlpExporter(o =>
        {
            o.Endpoint = new Uri("http://otel-collector:4317");
            o.Protocol = OtlpExportProtocol.Grpc;
        })
    )
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddMeter("Microsoft.AspNetCore.Hosting", "Microsoft.AspNetCore.Server.Kestrel")
            // Ключевой момент: включаем exemplars на нужных гистограммах
            .AddView("http.server.duration", new ExplicitBucketHistogramConfiguration
            {
                Boundaries = new double[] { 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10 },
                ExemplarFilter = new TraceBasedExemplarFilter()
            })
            .AddView("http.client.duration", new ExplicitBucketHistogramConfiguration
            {
                Boundaries = new double[] { 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10 },
                ExemplarFilter = new TraceBasedExemplarFilter()
            })
            // Экспорт метрик в OTLP -> Коллектор
            .AddOtlpExporter(o =>
            {
                o.Endpoint = new Uri("http://otel-collector:4317");
                o.Protocol = OtlpExportProtocol.Grpc;
            });
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