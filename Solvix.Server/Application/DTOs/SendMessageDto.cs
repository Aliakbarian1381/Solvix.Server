using System.ComponentModel.DataAnnotations;

namespace Solvix.Server.Application.DTOs
{
    public class SendMessageDto
    {
        public Guid ChatId { get; set; }
        public string Content { get; set; } = "";
        public string? CorrelationId { get; set; }
    }
}
