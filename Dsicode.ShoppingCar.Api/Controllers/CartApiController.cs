using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Dsicode.ShoppingCart.Api.Contract;
using Dsicode.ShoppingCart.Api.Data;
using Dsicode.ShoppingCart.Api.Models.Dto;
using Dsicode.ShoppingCart.Api.Models;

namespace Dsicode.ShoppingCart.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CartApiController : ControllerBase
    {
        private ResponseDto _response;
        private IMapper _mapper;
        private readonly AppDbContext _db;
        private readonly IProductService _productService;
        private readonly ICouponService _couponService;
        private readonly ILogger<CartApiController> _logger;

        public CartApiController(AppDbContext db, IMapper mapper, IProductService productService,
            ICouponService couponService, ILogger<CartApiController> logger)
        {
            _db = db;
            _mapper = mapper;
            this._response = new ResponseDto();
            _productService = productService;
            _couponService = couponService;
            _logger = logger;
        }

        [HttpGet("GetCart/{userId}")]
        public async Task<ResponseDto> GetCart(string userId)
        {
            try
            {
                _logger.LogInformation("Getting cart for user: {UserId}", userId);

                var cartHeader = await _db.CartHeaders.FirstOrDefaultAsync(u => u.UserId == userId);
                if (cartHeader == null)
                {
                    _logger.LogInformation("No cart found for user: {UserId}", userId);
                    _response.Result = new CartDto
                    {
                        CartHeader = new CartHeaderDto { UserId = userId, CartTotal = 0, Discount = 0 },
                        CartDetailsDto = new List<CartDetailsDto>()
                    };
                    return _response;
                }

                CartDto cart = new()
                {
                    CartHeader = _mapper.Map<CartHeaderDto>(cartHeader)
                };

                cart.CartDetailsDto = _mapper.Map<IEnumerable<CartDetailsDto>>(_db.CartDetails
                    .Where(u => u.CartHeaderId == cart.CartHeader.CartHeaderId));

                // Consultamos el microservicio de productos
                IEnumerable<ProductDto> productDtos = await _productService.GetProductos();

                foreach (var item in cart.CartDetailsDto)
                {
                    item.ProductDto = productDtos.FirstOrDefault(u => u.ProductId == item.ProductId);

                    if (item.ProductDto != null)
                    {
                        // Obtenemos el costo total de los productos del carrito de compras
                        cart.CartHeader.CartTotal += (item.Count * item.ProductDto.Price);
                    }
                    else
                    {
                        _logger.LogWarning("Product not found: {ProductId} for user: {UserId}", item.ProductId, userId);
                        // Si el producto no existe, crear un ProductDto temporal
                        item.ProductDto = new ProductDto
                        {
                            ProductId = item.ProductId,
                            Name = "Producto no encontrado",
                            Price = 0,
                            Description = "Este producto ya no está disponible",
                            CategoryName = "N/A",
                            ImageUrl = ""
                        };
                        // No agregar al total si el producto no existe
                    }
                }

                // SECCIÓN MEJORADA PARA EL CÁLCULO DEL CUPÓN
                if (!string.IsNullOrEmpty(cart.CartHeader.CouponCode))
                {
                    // Buscamos si existe un cupón válido
                    CouponDto coupon = await _couponService.GetCoupon(cart.CartHeader.CouponCode);
                    if (coupon != null && cart.CartHeader.CartTotal > coupon.MinAmount)
                    {
                        double discountAmount = 0;

                        // Calcular descuento según el tipo
                        if (coupon.AmountType == "PERCENTAGE")
                        {
                            // Descuento en porcentaje
                            discountAmount = (cart.CartHeader.CartTotal * coupon.DiscountAmount) / 100;
                        }
                        else
                        {
                            // Descuento fijo (FIXED)
                            discountAmount = coupon.DiscountAmount;
                        }

                        // Asegurar que el descuento no sea mayor al total del carrito
                        discountAmount = Math.Min(discountAmount, cart.CartHeader.CartTotal);

                        // Aplicar el descuento
                        cart.CartHeader.CartTotal -= discountAmount;
                        cart.CartHeader.Discount = discountAmount;

                        _logger.LogInformation("Applied coupon {CouponCode} with discount {Discount} (Type: {Type}, Original Amount: {OriginalAmount})",
                            cart.CartHeader.CouponCode, discountAmount, coupon.AmountType, coupon.DiscountAmount);
                    }
                    else
                    {
                        _logger.LogWarning("Coupon {CouponCode} not applied - either invalid or cart total {CartTotal} below minimum {MinAmount}",
                            cart.CartHeader.CouponCode, cart.CartHeader.CartTotal, coupon?.MinAmount ?? 0);
                    }
                }

                _response.Result = cart;
                _logger.LogInformation("Cart retrieved successfully for user: {UserId} with {ItemCount} items",
                    userId, cart.CartDetailsDto.Count());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cart for user: {UserId}", userId);
                _response.IsSuccess = false;
                _response.Message = ex.Message;
            }
            return _response;
        }

        [HttpPost("CartUpsert")]
        public async Task<ResponseDto> CartUpsert(CartDto cartDto)
        {
            try
            {
                _logger.LogInformation("Upserting cart for user: {UserId}", cartDto.CartHeader.UserId);

                if (string.IsNullOrEmpty(cartDto.CartHeader.UserId))
                {
                    _response.IsSuccess = false;
                    _response.Message = "ID de usuario requerido";
                    return _response;
                }

                var cartHeaderFromDb = await _db.CartHeaders.AsNoTracking()
                    .FirstOrDefaultAsync(u => u.UserId == cartDto.CartHeader.UserId);

                if (cartHeaderFromDb == null)
                {
                    // Crear nuevo carrito
                    CartHeader cartHeader = _mapper.Map<CartHeader>(cartDto.CartHeader);
                    _db.CartHeaders.Add(cartHeader);
                    await _db.SaveChangesAsync();

                    cartDto.CartDetailsDto.First().CartHeaderId = cartHeader.CartHeaderId;
                    _db.CartDetails.Add(_mapper.Map<CartDetails>(cartDto.CartDetailsDto.First()));
                    await _db.SaveChangesAsync();

                    _logger.LogInformation("Created new cart for user: {UserId}", cartDto.CartHeader.UserId);
                }
                else
                {
                    // Carrito existente
                    var cartDetail = cartDto.CartDetailsDto.First();
                    var cartDetailsFromDb = await _db.CartDetails.AsNoTracking().FirstOrDefaultAsync(
                        u => u.ProductId == cartDetail.ProductId &&
                        u.CartHeaderId == cartHeaderFromDb.CartHeaderId);

                    if (cartDetailsFromDb == null)
                    {
                        // Nuevo producto en carrito existente
                        cartDetail.CartHeaderId = cartHeaderFromDb.CartHeaderId;
                        _db.CartDetails.Add(_mapper.Map<CartDetails>(cartDetail));
                        await _db.SaveChangesAsync();

                        _logger.LogInformation("Added new product to existing cart for user: {UserId}", cartDto.CartHeader.UserId);
                    }
                    else
                    {
                        // Actualizar producto existente
                        if (cartDetail.CartDetailsId > 0)
                        {
                            // Es una actualización de cantidad específica
                            cartDetailsFromDb = await _db.CartDetails.FirstOrDefaultAsync(
                                u => u.CartDetailsId == cartDetail.CartDetailsId);

                            if (cartDetailsFromDb != null)
                            {
                                cartDetailsFromDb.Count = cartDetail.Count;
                                _db.CartDetails.Update(cartDetailsFromDb);
                                await _db.SaveChangesAsync();

                                _logger.LogInformation("Updated quantity for cart item {CartDetailsId} for user: {UserId}",
                                    cartDetail.CartDetailsId, cartDto.CartHeader.UserId);
                            }
                        }
                        else
                        {
                            // Es agregar más del mismo producto
                            cartDetail.Count += cartDetailsFromDb.Count;
                            cartDetail.CartHeaderId = cartDetailsFromDb.CartHeaderId;
                            cartDetail.CartDetailsId = cartDetailsFromDb.CartDetailsId;
                            _db.CartDetails.Update(_mapper.Map<CartDetails>(cartDetail));
                            await _db.SaveChangesAsync();

                            _logger.LogInformation("Updated cart quantity for user: {UserId}", cartDto.CartHeader.UserId);
                        }
                    }
                }

                _response.Result = cartDto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error upserting cart for user: {UserId}", cartDto?.CartHeader?.UserId);
                _response.Message = ex.Message;
                _response.IsSuccess = false;
            }
            return _response;
        }

        [HttpPost("RemoveCart")]
        public async Task<ResponseDto> RemoveCart([FromBody] int cartDetailsId)
        {
            try
            {
                _logger.LogInformation("Removing cart item: {CartDetailsId}", cartDetailsId);

                CartDetails cartDetails = await _db.CartDetails.FirstOrDefaultAsync(u => u.CartDetailsId == cartDetailsId);
                if (cartDetails == null)
                {
                    _response.IsSuccess = false;
                    _response.Message = "Elemento del carrito no encontrado";
                    return _response;
                }

                int totalCountofCartItem = await _db.CartDetails.CountAsync(u => u.CartHeaderId == cartDetails.CartHeaderId);
                _db.CartDetails.Remove(cartDetails);

                if (totalCountofCartItem == 1)
                {
                    var cartHeaderToRemove = await _db.CartHeaders
                        .FirstOrDefaultAsync(u => u.CartHeaderId == cartDetails.CartHeaderId);

                    if (cartHeaderToRemove != null)
                    {
                        _db.CartHeaders.Remove(cartHeaderToRemove);
                        _logger.LogInformation("Removed entire cart for CartHeaderId: {CartHeaderId}", cartDetails.CartHeaderId);
                    }
                }

                await _db.SaveChangesAsync();
                _response.Result = true;
                _logger.LogInformation("Cart item removed successfully: {CartDetailsId}", cartDetailsId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cart item: {CartDetailsId}", cartDetailsId);
                _response.Message = ex.Message;
                _response.IsSuccess = false;
            }
            return _response;
        }

        [HttpPost("ApplyCoupon")]
        public async Task<ResponseDto> ApplyCoupon([FromBody] CartDto cartDto)
        {
            try
            {
                _logger.LogInformation("Applying coupon {CouponCode} for user: {UserId}",
                    cartDto.CartHeader.CouponCode, cartDto.CartHeader.UserId);

                if (string.IsNullOrEmpty(cartDto.CartHeader.CouponCode))
                {
                    _response.IsSuccess = false;
                    _response.Message = "Código de cupón requerido";
                    return _response;
                }

                var cartFromDb = await _db.CartHeaders.FirstOrDefaultAsync(u => u.UserId == cartDto.CartHeader.UserId);
                if (cartFromDb == null)
                {
                    _response.IsSuccess = false;
                    _response.Message = "Carrito no encontrado";
                    return _response;
                }

                cartFromDb.CouponCode = cartDto.CartHeader.CouponCode;
                _db.CartHeaders.Update(cartFromDb);
                await _db.SaveChangesAsync();

                _response.Result = true;
                _logger.LogInformation("Coupon applied successfully for user: {UserId}", cartDto.CartHeader.UserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying coupon for user: {UserId}", cartDto?.CartHeader?.UserId);
                _response.IsSuccess = false;
                _response.Message = ex.Message;
            }
            return _response;
        }

        [HttpPost("RemoveCoupon")]
        public async Task<ResponseDto> RemoveCoupon([FromBody] CartDto cartDto)
        {
            try
            {
                _logger.LogInformation("Removing coupon for user: {UserId}", cartDto.CartHeader.UserId);

                var cartFromDb = await _db.CartHeaders.FirstOrDefaultAsync(u => u.UserId == cartDto.CartHeader.UserId);
                if (cartFromDb == null)
                {
                    _response.IsSuccess = false;
                    _response.Message = "Carrito no encontrado";
                    return _response;
                }

                cartFromDb.CouponCode = "";
                _db.CartHeaders.Update(cartFromDb);
                await _db.SaveChangesAsync();

                _response.Result = true;
                _logger.LogInformation("Coupon removed successfully for user: {UserId}", cartDto.CartHeader.UserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing coupon for user: {UserId}", cartDto?.CartHeader?.UserId);
                _response.IsSuccess = false;
                _response.Message = ex.Message;
            }
            return _response;
        }

        [HttpGet("GetCartCount/{userId}")]
        public async Task<ResponseDto> GetCartCount(string userId)
        {
            try
            {
                _logger.LogInformation("Getting cart count for user: {UserId}", userId);

                var cartHeader = await _db.CartHeaders.FirstOrDefaultAsync(u => u.UserId == userId);
                if (cartHeader == null)
                {
                    _response.Result = 0;
                    return _response;
                }

                var count = await _db.CartDetails.CountAsync(u => u.CartHeaderId == cartHeader.CartHeaderId);
                _response.Result = count;
                _logger.LogInformation("Cart count for user {UserId}: {Count}", userId, count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cart count for user: {UserId}", userId);
                _response.IsSuccess = false;
                _response.Message = ex.Message;
            }
            return _response;
        }
    }
}