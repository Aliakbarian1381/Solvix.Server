using System.ComponentModel.DataAnnotations;

namespace Solvix.Server.Application.DTOs
{
    public class OtpRequestDto
    {
        [Required(ErrorMessage = "شماره تلفن الزامی است")]
        [RegularExpression(@"^09\d{9}$", ErrorMessage = "فرمت شماره تلفن نامعتبر است (مثال: 09123456789)")]
        public string PhoneNumber { get; set; } = "";
    }
}
