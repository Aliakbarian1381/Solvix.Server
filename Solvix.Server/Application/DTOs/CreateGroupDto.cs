using System.ComponentModel.DataAnnotations;

namespace Solvix.Server.Application.DTOs
{
    public class CreateGroupDto
    {
        [Required]
        public string Title { get; set; }
        [Required]
        public List<long> ParticipantIds { get; set; }
    }
}
