using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SubcongMeet.Models
{
    [Table("dist_teams")]
    public class Team
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        public string Acronym { get; set; } = string.Empty;

        [Required]
        public string Division { get; set; } = "Elementary";
    }
}