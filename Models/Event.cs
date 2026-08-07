using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SubcongMeet.Models
{
    [Table("dist_events")]
    public class Event
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
        
        [Required]
        [Column("title")]
        public string Title { get; set; } = string.Empty;
        
        [Column("sport_name")]
        public string? SportName { get; set; }
        
        [Column("sport_category")]
        public string? SportCategory { get; set; }
        
        [Required]
        [Column("division")]
        public string Division { get; set; } = "Elementary"; 
        
        [Required]
        [Column("status")]
        public string Status { get; set; } = "Pending"; 

        [Column("coordinatorid")]
        public int? CoordinatorId { get; set; }

        [Column("eliminationtype")]
        public string? EliminationType { get; set; }

        [Column("teamaid")]
        public int? TeamAId { get; set; }

        [Column("teambid")]
        public int? TeamBId { get; set; }

        [Column("goldteamid")]
        public int? GoldTeamId { get; set; }

        [Column("silverteamid")]
        public int? SilverTeamId { get; set; }

        [Column("bronzeteamid")]
        public int? BronzeTeamId { get; set; }

        [Column("createdat")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updatedat")]
        public DateTime? UpdatedAt { get; set; } = DateTime.UtcNow;

        [Column("GoldWinnerName")]
        public string? GoldWinnerName { get; set; }

        [Column("SilverWinnerName")]
        public string? SilverWinnerName { get; set; }

        [Column("BronzeWinnerName")]
        public string? BronzeWinnerName { get; set; }

        [Column("schoolGold")]
        public string? schoolGold { get; set; }

        [Column("schoolSilver")]
        public string? schoolSilver { get; set; }

        [Column("schoolBronze")]
        public string? schoolBronze { get; set; }

        [Column("LastUpdatedBy")]
        public string? LastUpdatedBy { get; set; }
    }
}