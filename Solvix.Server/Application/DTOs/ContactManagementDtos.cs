using System.ComponentModel.DataAnnotations;

namespace Solvix.Server.Application.DTOs
{

    public class BlockContactDto
    {
        [Required]
        public bool IsBlocked { get; set; }
    }

    public class UpdateDisplayNameDto
    {
        [MaxLength(100)]
        public string? DisplayName { get; set; }
    }

    public class ContactSearchDto
    {
        [Required]
        [MinLength(2)]
        [MaxLength(50)]
        public string Query { get; set; } = string.Empty;

        [Range(1, 100)]
        public int Limit { get; set; } = 20;
    }

    public class ContactFilterDto
    {
        public bool? IsFavorite { get; set; }
        public bool? IsBlocked { get; set; }
        public bool? HasChat { get; set; }
        public string? SortBy { get; set; } = "name"; // name, lastInteraction, dateAdded
        public string? SortDirection { get; set; } = "asc"; // asc, desc
    }
}
