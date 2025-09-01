using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ObservabilityDemo_Minimal.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ActivitySource _activitySource;

        public ProductsController(ApplicationDbContext context, ActivitySource activitySource)
        {
            _context = context;
            _activitySource = activitySource;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Product>>> GetProducts()
        {
            // Создаем кастомный span для детального трейсинга
            using var activity = _activitySource.StartActivity("GetProducts.BusinessLogic");
            activity?.SetTag("operation", "fetch_products");

            // Имитируем некоторую бизнес-логику
            await Task.Delay(50); // Имитация работы

            // EF Core автоматически создаст spans для SQL запросов
            var products = await _context.Products
                .OrderByDescending(p => p.CreatedAt)
                .Take(100)
                .ToListAsync();

            activity?.SetTag("products.count", products.Count);

            return Ok(products);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Product>> GetProduct(int id)
        {
            //  using var activity = _activitySource.StartActivity("GetProduct.BusinessLogic");
            // activity?.SetTag("product.id", id);

            var product = await _context.Products.FindAsync(id);

            if (product == null)
            {
                //    activity?.SetTag("result", "not_found");
                return NotFound();
            }

            //  activity?.SetTag("result", "found");
            return Ok(product);
        }

        [HttpPost]
        public async Task<ActionResult<Product>> CreateProduct(CreateProductRequest request)
        {
            using var activity = _activitySource.StartActivity("CreateProduct.BusinessLogic");
            activity?.SetTag("product.name", request.Name);
            activity?.SetTag("product.price", request.Price);

            // Имитируем валидацию
            await Task.Delay(20);

            var product = new Product
            {
                Name = request.Name,
                Price = request.Price
            };

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            activity?.SetTag("product.created_id", product.Id);

            return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
        }

        [HttpGet("slow")]
        public async Task<ActionResult> SlowEndpoint()
        {
            using var activity = _activitySource.StartActivity("SlowEndpoint.Processing");

            // Имитируем медленную операцию
            await Task.Delay(2000);

            // Имитируем SQL запрос
            var count = await _context.Products.CountAsync();

            activity?.SetTag("total.products", count);

            return Ok(new { message = "Slow operation completed", totalProducts = count });
        }

        [HttpGet("error")]
        public async Task<ActionResult> ErrorEndpoint()
        {
            using var activity = _activitySource.StartActivity("ErrorEndpoint.Processing");

            try
            {
                // Имитируем ошибку
                await Task.Delay(100);
                throw new InvalidOperationException("Simulated error for testing");
            }
            catch (Exception ex)
            {
                // OpenTelemetry автоматически записывает исключения в spans
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.SetTag("error.type", ex.GetType().Name);
                throw;
            }
        }
    }

    public class CreateProductRequest
    {
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }
}