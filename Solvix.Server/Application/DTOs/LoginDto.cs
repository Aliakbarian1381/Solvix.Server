using System.ComponentModel.DataAnnotations;

namespace Solvix.Server.Application.DTOs
{
    public class LoginDto
    {
        public string PhoneNumber { get; set; } = "";
        public string Password { get; set; } = "";
        public string? FcmToken { get; set; }
    }
}
