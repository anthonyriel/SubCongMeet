using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using SubcongMeet.Data;
using SubcongMeet.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SubcongMeet.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CronController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<CronController> _logger;

        public CronController(ApplicationDbContext context, IConfiguration configuration, ILogger<CronController> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Public endpoint to receive cron-job.org HTTP pings
        /// Route: GET /api/cron/execute or POST /api/cron/execute
        /// </summary>
        [HttpGet("execute")]
        [HttpPost("execute")]
        public async Task<IActionResult> Execute([FromQuery] string? secret)
        {
            var stopwatch = Stopwatch.StartNew();

            // Validate Secret Header or Query Param
            string expectedSecret = _configuration["CronJobOrg:CronSecret"] ?? "locatu-cron-secret-2026";
            string? providedHeaderSecret = Request.Headers["X-Cron-Secret"].FirstOrDefault();
            string? providedAuthHeader = Request.Headers["Authorization"].FirstOrDefault();
            
            bool isAuthorized = false;
            if (!string.IsNullOrEmpty(providedHeaderSecret) && providedHeaderSecret.Equals(expectedSecret, StringComparison.Ordinal))
            {
                isAuthorized = true;
            }
            else if (!string.IsNullOrEmpty(secret) && secret.Equals(expectedSecret, StringComparison.Ordinal))
            {
                isAuthorized = true;
            }
            else if (!string.IsNullOrEmpty(providedAuthHeader) && providedAuthHeader.Equals($"Bearer {expectedSecret}", StringComparison.OrdinalIgnoreCase))
            {
                isAuthorized = true;
            }

            string callerIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            string userAgent = Request.Headers["User-Agent"].ToString();

            if (!isAuthorized)
            {
                stopwatch.Stop();
                _logger.LogWarning("Unauthorized cron execution attempt from IP {Ip}. UserAgent: {UA}", callerIp, userAgent);

                var unauthorizedLog = new CronExecutionLog
                {
                    ExecutedAt = DateTime.UtcNow,
                    TaskName = "Subcong: Unauthorized Trigger Attempt",
                    Status = "Failed",
                    StatusCode = 401,
                    ExecutionTimeMs = stopwatch.Elapsed.TotalMilliseconds,
                    Details = "Request rejected due to missing or invalid secret token.",
                    CallerIp = callerIp,
                    UserAgent = userAgent
                };

                try
                {
                    _context.CronExecutionLogs.Add(unauthorizedLog);
                    await _context.SaveChangesAsync();
                }
                catch { /* Ignore log save error on unauthorized failure */ }

                return Unauthorized(new
                {
                    success = false,
                    message = "Unauthorized: Invalid or missing X-Cron-Secret token.",
                    timestamp = DateTime.UtcNow
                });
            }

            var taskSummary = new List<string>();
            bool isOverallSuccess = true;

            // Task 1: Recalculate Medal Tallies
            try
            {
                var currentTallies = await _context.MedalTallies.ToListAsync();
                _context.MedalTallies.RemoveRange(currentTallies);

                var teams = await _context.Teams.ToListAsync();
                var completedEvents = await _context.Events.Where(e => e.Status == "Completed").ToListAsync();

                foreach (var team in teams)
                {
                    var tally = new MedalTally
                    {
                        TeamId = team.Id,
                        Gold = completedEvents.Count(e => e.GoldTeamId == team.Id),
                        Silver = completedEvents.Count(e => e.SilverTeamId == team.Id),
                        Bronze = completedEvents.Count(e => e.BronzeTeamId == team.Id)
                    };
                    _context.MedalTallies.Add(tally);
                }
                await _context.SaveChangesAsync();
                taskSummary.Add($"MedalTalliesSync: Updated rankings for {teams.Count} schools based on {completedEvents.Count} finished events.");
            }
            catch (Exception ex)
            {
                isOverallSuccess = false;
                taskSummary.Add($"MedalTalliesSync Error: {ex.Message}");
                _logger.LogError(ex, "Cron Task MedalTalliesSync failed");
            }

            // Task 2: System Health Check & Database Status
            try
            {
                var totalEvents = await _context.Events.CountAsync();
                var totalQualifiers = await _context.EventQualifiers.CountAsync();
                taskSummary.Add($"SystemHealthCheck: Database connection active. Recorded {totalEvents} total events and {totalQualifiers} qualifiers.");
            }
            catch (Exception ex)
            {
                isOverallSuccess = false;
                taskSummary.Add($"SystemHealthCheck Error: {ex.Message}");
            }

            // Task 3: Maintenance Cleanup (Delete logs older than 5 days)
            try
            {
                var cutoff = DateTime.UtcNow.AddDays(-5);
                var oldLogs = await _context.CronExecutionLogs.Where(l => l.ExecutedAt < cutoff).ToListAsync();
                if (oldLogs.Any())
                {
                    _context.CronExecutionLogs.RemoveRange(oldLogs);
                    await _context.SaveChangesAsync();
                }
                taskSummary.Add($"LogCleanup: Purged {oldLogs.Count} execution log entries older than 5 days.");
            }
            catch (Exception ex)
            {
                taskSummary.Add($"LogCleanup Warning: {ex.Message}");
            }

            stopwatch.Stop();
            double durationMs = Math.Round(stopwatch.Elapsed.TotalMilliseconds, 2);

            // Log execution to DB
            var execLog = new CronExecutionLog
            {
                ExecutedAt = DateTime.UtcNow,
                TaskName = "Subcong: Automated Cron Trigger",
                Status = isOverallSuccess ? "Success" : "Warning",
                StatusCode = 200,
                ExecutionTimeMs = durationMs,
                Details = string.Join(" | ", taskSummary),
                CallerIp = callerIp,
                UserAgent = string.IsNullOrWhiteSpace(userAgent) ? "cron-job.org Bot" : userAgent
            };

            try
            {
                _context.CronExecutionLogs.Add(execLog);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist CronExecutionLog entry");
            }

            return Ok(new
            {
                success = isOverallSuccess,
                executedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'"),
                executionTimeMs = durationMs,
                tasksExecuted = taskSummary,
                message = isOverallSuccess ? "All cron tasks executed successfully." : "Cron tasks completed with warnings."
            });
        }

        /// <summary>
        /// Simple ping endpoint to keep app warm or verify endpoint availability
        /// Route: GET /api/cron/ping
        /// </summary>
        [HttpGet("ping")]
        public IActionResult Ping()
        {
            return Ok(new
            {
                status = "Online",
                service = "Subcong Meet Cron Webhook",
                timestamp = DateTime.UtcNow,
                cronJobOrgCompatible = true
            });
        }
    }
}