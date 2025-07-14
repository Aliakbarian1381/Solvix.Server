using System.ComponentModel.DataAnnotations;

namespace Solvix.Server.Application.DTOs
{
    public class ContactImportResult
    {
        public int ImportedCount { get; set; }
        public int DuplicateCount { get; set; }
        public int ErrorCount { get; set; }
    }

    public class ImportContactItem
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }

        [Required]
        public string PhoneNumber { get; set; } = string.Empty;

        public string? Email { get; set; }
        public string? DisplayName { get; set; }
        public bool IsFavorite { get; set; } = false;
    }
}
