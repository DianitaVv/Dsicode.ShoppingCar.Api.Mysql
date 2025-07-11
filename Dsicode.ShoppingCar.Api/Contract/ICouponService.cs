using Dsicode.ShoppingCart.Api.Models.Dto;

namespace Dsicode.ShoppingCart.Api.Contract
{
    public interface ICouponService
    {
        Task<CouponDto> GetCoupon(string couponCode);
    }
}
