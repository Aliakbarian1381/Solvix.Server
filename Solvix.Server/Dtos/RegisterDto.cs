using System.ComponentModel.DataAnnotations;

namespace Solvix.Server.Dtos
{
    public class RegisterDto
    {
        [Required(ErrorMessage = "رمز عبور الزامی است")]
        [MinLength(8, ErrorMessage = "رمز عبور باید حداقل 8 کاراکتر باشد")]
        public string Password { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        [Required(ErrorMessage = "شماره تلفن الزامی است")]
        [RegularExpression(@"^09\d{9}$", ErrorMessage = "فرمت شماره تلفن نامعتبر است (مثال: 09123456789)")]
        public string PhoneNumber { get; set; }
    }
}
