using System.ComponentModel.DataAnnotations;

namespace Solvix.Server.Application.DTOs
{
    public class SendMessageDto
    {
        [Required]
        public Guid ChatId { get; set; }

        [Required]
        [MinLength(1, ErrorMessage = "پیام نمی‌تواند خالی باشد")]
        [MaxLength(4000, ErrorMessage = "پیام بیش از حد طولانی است")]
        public string Content { get; set; } = "";
    }
}
