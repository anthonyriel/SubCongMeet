using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SubcongMeet.Models
{
    [Table("subcong_qualifiers")] 
    public class EventQualifier
    {
        [Column("id")]
        public Guid Id { get; set; }
        
        [Required]
        [Column("event_id")]
        public long EventId { get; set; } 
        
        [Required]
        [Column("participant_name")]
        public string ParticipantName { get; set; } = string.Empty;
        
        [Column("role")]
        public string? Role { get; set; } = "Athlete";

        // 👇 THIS IS THE MAGIC FIX: 
        // C# uses "Team", but the database uses "district"
        [Column("district")]
        public string? Team { get; set; }
        
        // C# uses "School", database uses "school"
        [Column("school")]
        public string? School { get; set; }
        
        [Column("gender")]
        public string? Gender { get; set; }
        
        [Column("tshirt_size")]
        public string? TshirtSize { get; set; }
        
        [Column("updated_by")]
        public Guid? UpdatedBy { get; set; } 
        
        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}