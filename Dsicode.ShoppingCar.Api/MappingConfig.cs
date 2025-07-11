using AutoMapper;
using Dsicode.ShoppingCart.Api.Models.Dto;
using Dsicode.ShoppingCart.Api.Models;

namespace Dsicode.ShoppingCart.Api
{
    public class MappingConfig
    {
        public static MapperConfiguration RegisterMaps()
        {
            var mappingConfig = new MapperConfiguration(config =>
            {
                config.CreateMap<CartHeader, CartHeaderDto>().ReverseMap();
                config.CreateMap<CartDetails, CartDetailsDto>().ReverseMap();
            });
            return mappingConfig;
        }
    }
}