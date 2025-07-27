using System.ComponentModel.DataAnnotations;

namespace Solvix.Server.Application.DTOs
{
    public class RegisterDto
    {
        public string PhoneNumber { get; set; } = "";
        public string Password { get; set; } = "";
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? FcmToken { get; set; }
    }
}
