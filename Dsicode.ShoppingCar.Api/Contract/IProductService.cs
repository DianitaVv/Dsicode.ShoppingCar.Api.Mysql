using Dsicode.ShoppingCart.Api.Models.Dto;

namespace Dsicode.ShoppingCart.Api.Contract
{
    public interface IProductService
    {
        Task<IEnumerable<ProductDto>> GetProductos();

    }

}
