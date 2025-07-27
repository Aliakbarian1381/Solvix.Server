using System.ComponentModel.DataAnnotations;

namespace Solvix.Server.Application.DTOs
{
    public class CreateGroupDto
    {
        [Required]
        public required string Title { get; set; }

        [Required]
        public required List<long> ParticipantIds { get; set; }
    }
}
