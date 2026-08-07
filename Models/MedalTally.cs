using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SubcongMeet.Models
{
    [Table("dist_medaltally")]
    public class MedalTally
    {
        [Key]
        [ForeignKey("Team")]
        public int TeamId { get; set; }
        
        public int Gold { get; set; } = 0;
        
        public int Silver { get; set; } = 0;
        
        public int Bronze { get; set; } = 0;

        // Navigation property for Entity Framework Core
        public virtual Team? Team { get; set; }
    }
}