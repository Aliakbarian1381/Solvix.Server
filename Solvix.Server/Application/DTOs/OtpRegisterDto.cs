using System.ComponentModel.DataAnnotations;

namespace Solvix.Server.Application.DTOs
{
    public class OtpRegisterDto
    {
        [Required(ErrorMessage = "شماره تلفن الزامی است")]
        [RegularExpression(@"^09\d{9}$", ErrorMessage = "فرمت شماره تلفن نامعتبر است")]
        public string PhoneNumber { get; set; } = "";

        [Required(ErrorMessage = "کد تایید الزامی است")]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "کد تایید باید 6 رقمی باشد")]
        public string OtpCode { get; set; } = "";

        public string? FirstName { get; set; }
        public string? LastName { get; set; }
    }
}
