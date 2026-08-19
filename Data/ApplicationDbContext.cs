using Microsoft.EntityFrameworkCore;
using SubcongMeet.Models;

namespace SubcongMeet.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Team> Teams { get; set; }
        public DbSet<Event> Events { get; set; }
        public DbSet<MedalTally> MedalTallies { get; set; }
        public DbSet<Coordinator> Coordinators { get; set; }
        public DbSet<EventQualifier> EventQualifiers { get; set; } // Added
        public DbSet<CronExecutionLog> CronExecutionLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Team>(entity =>
            {
                entity.ToTable("subcong_teams");
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Name).HasColumnName("name");
                entity.Property(e => e.Acronym).HasColumnName("acronym");
                entity.Property(e => e.Division).HasColumnName("division");
            });

            modelBuilder.Entity<Event>(entity =>
            {
                entity.ToTable("subcong_events");
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Title).HasColumnName("title");
                entity.Property(e => e.SportName).HasColumnName("sport_name");
                entity.Property(e => e.SportCategory).HasColumnName("sport_category");
                entity.Property(e => e.Division).HasColumnName("division");
                entity.Property(e => e.Status).HasColumnName("status");
                entity.Property(e => e.GoldTeamId).HasColumnName("goldteamid");
                entity.Property(e => e.SilverTeamId).HasColumnName("silverteamid");
                entity.Property(e => e.BronzeTeamId).HasColumnName("bronzeteamid");
                entity.Property(e => e.UpdatedAt).HasColumnName("updatedat");
                entity.Property(e => e.CoordinatorId).HasColumnName("coordinatorid");
                entity.Property(e => e.EliminationType).HasColumnName("eliminationtype");
                entity.Property(e => e.TeamAId).HasColumnName("teamaid");
                entity.Property(e => e.TeamBId).HasColumnName("teambid");
                entity.Property(e => e.CreatedAt).HasColumnName("createdat");
                entity.Property(e => e.GoldWinnerName).HasColumnName("GoldWinnerName");
                entity.Property(e => e.SilverWinnerName).HasColumnName("SilverWinnerName");
                entity.Property(e => e.BronzeWinnerName).HasColumnName("BronzeWinnerName");
                entity.Property(e => e.schoolGold).HasColumnName("schoolGold");
                entity.Property(e => e.schoolSilver).HasColumnName("schoolSilver");
                entity.Property(e => e.schoolBronze).HasColumnName("schoolBronze");
                entity.Property(e => e.LastUpdatedBy).HasColumnName("LastUpdatedBy");
            });

            modelBuilder.Entity<MedalTally>(entity =>
            {
                entity.ToTable("subcong_medaltally");
                entity.HasKey(e => e.TeamId);
                entity.Property(e => e.TeamId).HasColumnName("teamid");
                entity.Property(e => e.Gold).HasColumnName("gold");
                entity.Property(e => e.Silver).HasColumnName("silver");
                entity.Property(e => e.Bronze).HasColumnName("bronze");
            });

            modelBuilder.Entity<Coordinator>(entity =>
            {
                entity.ToTable("subcong_coordinators");
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Username).HasColumnName("username");
                entity.Property(e => e.Password).HasColumnName("passwordhash");
                entity.Property(e => e.FullName).HasColumnName("fullname");
                entity.Property(e => e.IsAdmin).HasColumnName("is_admin");
            });

            // New Mapping for EventQualifier
            modelBuilder.Entity<EventQualifier>(entity =>
            {
                entity.ToTable("subcong_qualifiers");
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.EventId).HasColumnName("event_id");
                entity.Property(e => e.ParticipantName).HasColumnName("participant_name");
                entity.Property(e => e.Role).HasColumnName("role");
                entity.Property(e => e.SchoolName).HasColumnName("school_name");
                entity.Property(e => e.School).HasColumnName("school");
                entity.Property(e => e.Gender).HasColumnName("gender");
                entity.Property(e => e.TshirtSize).HasColumnName("tshirt_size");
                entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            });

            modelBuilder.Entity<CronExecutionLog>(entity =>
            {
                entity.ToTable("dist_cron_execution_logs");
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.ExecutedAt).HasColumnName("executed_at");
                entity.Property(e => e.TaskName).HasColumnName("task_name");
                entity.Property(e => e.Status).HasColumnName("status");
                entity.Property(e => e.StatusCode).HasColumnName("status_code");
                entity.Property(e => e.ExecutionTimeMs).HasColumnName("execution_time_ms");
                entity.Property(e => e.Details).HasColumnName("details");
                entity.Property(e => e.CallerIp).HasColumnName("caller_ip");
                entity.Property(e => e.UserAgent).HasColumnName("user_agent");
            });
        }
    }
}