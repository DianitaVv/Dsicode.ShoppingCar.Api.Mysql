using System.ComponentModel.DataAnnotations;

namespace Dsicode.ShoppingCart.Api.Models.Dto
{
    public class CouponDto
    {
        public int CouponId { get; set; }

        [Required(ErrorMessage = "El código del cupón es requerido")]
        [StringLength(20, ErrorMessage = "El código no puede exceder 20 caracteres")]
        public string CouponCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "El monto de descuento es requerido")]
        [Range(0.01, double.MaxValue, ErrorMessage = "El descuento debe ser mayor a 0")]
        public double DiscountAmount { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "El monto mínimo no puede ser negativo")]
        public int MinAmount { get; set; }

        [Required(ErrorMessage = "El tipo de descuento es requerido")]
        public string AmountType { get; set; } = "PERCENTAGE"; // PERCENTAGE o FIXED

        [Range(1, int.MaxValue, ErrorMessage = "El límite de uso debe ser mayor a 0")]
        public int LimitUse { get; set; } = 1;

        [Required(ErrorMessage = "La fecha de inicio es requerida")]
        public DateTime DateInit { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "La fecha de fin es requerida")]
        public DateTime DateEnd { get; set; } = DateTime.Now.AddMonths(1);

        public string Category { get; set; } = "GENERAL";

        public bool StateCoupon { get; set; } = true;

        // Propiedades adicionales para la vista
        public string FormattedDiscount => AmountType == "PERCENTAGE"
            ? $"{DiscountAmount}%"
            : $"${DiscountAmount:F2}";

        public bool IsActive => StateCoupon && DateTime.Now >= DateInit && DateTime.Now <= DateEnd;

        public string StatusText => IsActive ? "Activo" : "Inactivo";

        public string StatusClass => IsActive ? "text-success" : "text-danger";

        public string FormattedDateInit => DateInit.ToString("dd/MM/yyyy HH:mm");
        public string FormattedDateEnd => DateEnd.ToString("dd/MM/yyyy HH:mm");
    }
}
