using System;
using System.ComponentModel.DataAnnotations;

namespace SubcongMeet.Models
{
    public class EventQualifier
    {
        public Guid Id { get; set; }
        
        [Required]
        public long EventId { get; set; } // Matches BIGINT in Supabase
        
        [Required]
        public string ParticipantName { get; set; } = string.Empty;
        
        public string? Role { get; set; } = "Athlete";
        
        public string? SchoolName { get; set; }
        public string? School { get; set; }
        public string? Gender { get; set; }
        public string? TshirtSize { get; set; }
        
        public Guid? UpdatedBy { get; set; } // The ID of the Admin/Coordinator
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}