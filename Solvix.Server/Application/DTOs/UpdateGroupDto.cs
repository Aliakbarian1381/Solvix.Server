using System.ComponentModel.DataAnnotations;

namespace Solvix.Server.Application.DTOs
{
    public class UpdateGroupDto
    {
        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string Title { get; set; } = "";

        [StringLength(500)]
        public string? Description { get; set; }

        public string? GroupImageUrl { get; set; }
    }
}
