using Dsicode.ShoppingCart.Api.Contract;
using Dsicode.ShoppingCart.Api.Models.Dto;
using Newtonsoft.Json;

namespace Dsicode.ShoppingCart.Api.Services
{
    public class CouponService : ICouponService
    {
        private readonly IHttpClientFactory _clientFactory;
        private readonly ILogger<CouponService> _logger;

        public CouponService(IHttpClientFactory clientFactory, ILogger<CouponService> logger)
        {
            _clientFactory = clientFactory;
            _logger = logger;
        }

        public async Task<CouponDto> GetCoupon(string couponCode)
        {
            try
            {
                _logger.LogInformation("Getting coupon: {CouponCode}", couponCode);

                if (string.IsNullOrEmpty(couponCode))
                {
                    _logger.LogWarning("Coupon code is null or empty");
                    return new CouponDto();
                }

                var client = _clientFactory.CreateClient("Coupon");
                var response = await client.GetAsync($"/api/Cupon/GetByCode/{couponCode}");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get coupon {CouponCode}. Status: {StatusCode}",
                        couponCode, response.StatusCode);
                    return new CouponDto();
                }

                var apiContent = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<ResponseDto>(apiContent);

                if (result != null && result.IsSuccess && result.Result != null)
                {
                    var coupon = JsonConvert.DeserializeObject<CouponDto>(Convert.ToString(result.Result));
                    _logger.LogInformation("Coupon {CouponCode} retrieved successfully", couponCode);
                    return coupon ?? new CouponDto();
                }

                _logger.LogWarning("Coupon {CouponCode} not found or invalid", couponCode);
                return new CouponDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting coupon: {CouponCode}", couponCode);
                return new CouponDto();
            }
        }
    }
}
