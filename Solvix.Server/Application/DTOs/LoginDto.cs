using System.ComponentModel.DataAnnotations;

namespace Solvix.Server.Application.DTOs
{
    public class LoginDto
    {
        [Required(ErrorMessage = "شماره تلفن الزامی است")]
        [RegularExpression(@"^09\d{9}$", ErrorMessage = "فرمت شماره تلفن نامعتبر است")]
        public string PhoneNumber { get; set; } = "";

        [Required(ErrorMessage = "رمز عبور الزامی است")]
        public string Password { get; set; } = "";
    }
}
