using Dsicode.ShoppingCart.Api.Contract;
using Dsicode.ShoppingCart.Api.Models.Dto;
using Newtonsoft.Json;

namespace Dsicode.ShoppingCart.Api.Services
{
    public class ProductService : IProductService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ProductService> _logger;

        public ProductService(IHttpClientFactory httpClientFactory, ILogger<ProductService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<IEnumerable<ProductDto>> GetProductos()
        {
            try
            {
                _logger.LogInformation("Getting all products from Product microservice");

                var client = _httpClientFactory.CreateClient("Product");
                var response = await client.GetAsync("/api/Product");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to get products. Status: {StatusCode}", response.StatusCode);
                    return new List<ProductDto>();
                }

                var apiContent = await response.Content.ReadAsStringAsync();
                var resp = JsonConvert.DeserializeObject<ResponseDto>(apiContent);

                if (resp != null && resp.IsSuccess && resp.Result != null)
                {
                    var products = JsonConvert.DeserializeObject<IEnumerable<ProductDto>>(Convert.ToString(resp.Result));
                    _logger.LogInformation("Retrieved {ProductCount} products successfully", products?.Count() ?? 0);
                    return products ?? new List<ProductDto>();
                }

                _logger.LogWarning("No products found or invalid response");
                return new List<ProductDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting products from Product microservice");
                return new List<ProductDto>();
            }
        }
    }
}