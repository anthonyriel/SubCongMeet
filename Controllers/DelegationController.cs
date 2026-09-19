using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SubcongMeet.Data; 
using SubcongMeet.Models; 
using System.Security.Claims;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SubcongMeet.Controllers
{
    [Authorize(Roles = "Admin,Coordinator")]
    public class DelegationController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DelegationController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Helper method to extract the numerical CoordinatorId
        private int? GetCurrentCoordinatorId()
        {
            var claimValue = User.FindFirst("CoordinatorId")?.Value 
                ?? User.FindFirst("Id")?.Value;

            if (int.TryParse(claimValue, out int parsedId))
            {
                return parsedId;
            }
            return null;
        }

        public async Task<IActionResult> Manage(long eventId)
        {
            var currentCoordinatorId = GetCurrentCoordinatorId();
            var isAdmin = User.IsInRole("Admin");

            var eventDetails = await _context.Events.FirstOrDefaultAsync(e => e.Id == eventId);
            if (eventDetails == null) return NotFound("Event not found.");

            // Security check comparing Assigned Coordinator ID vs Session Coordinator ID
            if (!isAdmin && eventDetails.CoordinatorId != currentCoordinatorId)
            {
                return Forbid(); 
            }

            var qualifiers = await _context.EventQualifiers
                .Where(q => q.EventId == eventId)
                .OrderBy(q => q.Role != "Athlete" ? 1 : 0) // Athletes first, staff at the end
                .ThenByDescending(q => q.Gender == "W" || q.Gender == "Girls") // Girls first, then Boys
                .ThenBy(q => q.Team)
                .ThenBy(q => q.ParticipantName)
                .ToListAsync();

            ViewBag.Event = eventDetails;
            var division = eventDetails.Division?.Trim() ?? "";
            var teamsQuery = _context.Teams.AsQueryable();
            if (!string.IsNullOrEmpty(division))
            {
                var filteredTeams = await teamsQuery
                    .Where(t => t.Division.Trim().ToLower() == division.ToLower())
                    .OrderBy(t => t.Name)
                    .ToListAsync();
                
                ViewBag.Teams = filteredTeams.GroupBy(t => t.Name).Select(g => g.First()).OrderBy(t => t.Name).ToList();
            }
            else
            {
                ViewBag.Teams = await teamsQuery.GroupBy(t => t.Name).Select(g => g.First()).OrderBy(t => t.Name).ToListAsync();
            }
            
            var existingNames = qualifiers.Select(q => RecordedDelegationWinner.Identity(q.ParticipantName, q.Team)).ToHashSet();
            ViewBag.RecordedWinners = (await GetRecordedWinners(eventDetails))
                .Where(w => !existingNames.Contains(w.Key)).ToList();
            return View(qualifiers);
        }

        private async Task<List<RecordedDelegationWinner>> GetRecordedWinners(Event target)
        {
            if (string.IsNullOrWhiteSpace(target.SportName)) return new();
            var sport = target.SportName.Trim().ToLower();
            var division = target.Division.Trim().ToLower();
            var events = await _context.Events.AsNoTracking()
                .Where(e => e.Status == "Completed" && e.SportName != null
                    && e.SportName.Trim().ToLower() == sport && e.Division.Trim().ToLower() == division)
                .ToListAsync();
            var teams = await _context.Teams.AsNoTracking()
                .Where(t => t.Division.Trim().ToLower() == division).ToDictionaryAsync(t => t.Id, t => t.Name);
            return RecordedDelegationWinner.Build(events, target, teams);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddRecordedWinners(long eventId, List<string>? winnerKeys)
        {
            var target = await _context.Events.FirstOrDefaultAsync(e => e.Id == eventId);
            if (target == null) return NotFound("Event not found.");
            var coordinatorId = GetCurrentCoordinatorId();
            if (!User.IsInRole("Admin") && (coordinatorId == null || target.CoordinatorId != coordinatorId))
                return Forbid();

            var selected = (winnerKeys ?? new()).ToHashSet(StringComparer.Ordinal);
            // Re-read official results: posted names and districts are never trusted.
            var available = await GetRecordedWinners(target);
            var existing = await _context.EventQualifiers.AsNoTracking().Where(q => q.EventId == eventId).ToListAsync();
            var identities = existing.Select(q => RecordedDelegationWinner.Identity(q.ParticipantName, q.Team)).ToHashSet();
            int added = 0;
            foreach (var winner in available.Where(w => selected.Contains(w.Key)))
            {
                if (!identities.Add(winner.Key)) continue;
                _context.EventQualifiers.Add(new EventQualifier { Id = Guid.NewGuid(), EventId = eventId,
                    ParticipantName = winner.Name, Team = winner.District, Role = "Athlete",
                    Gender = winner.Gender, UpdatedAt = DateTime.UtcNow });
                added++;
            }
            if (added > 0)
            {
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"{added} recorded winner(s) added as athletes. Existing participants were skipped.";
            }
            else TempData["ErrorMessage"] = "No new participants added. Select available winners; existing participants and unavailable results are skipped.";
            return RedirectToAction(nameof(Manage), new { eventId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveQualifier(EventQualifier model)
        {
            var currentCoordinatorId = GetCurrentCoordinatorId();
            var isAdmin = User.IsInRole("Admin");

            var eventDetails = await _context.Events.FirstOrDefaultAsync(e => e.Id == model.EventId);
            if (eventDetails == null) return NotFound("Event not found.");

            // Security check for saving records
            if (!isAdmin && eventDetails.CoordinatorId != currentCoordinatorId)
            {
                return Forbid();
            }

            if (!string.IsNullOrWhiteSpace(model.ParticipantName))
            {
                model.ParticipantName = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(model.ParticipantName.Trim().ToLowerInvariant());
            }

            if (string.IsNullOrWhiteSpace(model.Role))
            {
                model.Role = "Athlete";
            }

            // Automatically determine gender based on event title if not set
            if (string.IsNullOrWhiteSpace(model.Gender) && !string.IsNullOrEmpty(eventDetails.Title))
            {
                if (eventDetails.Title.Contains("boys", StringComparison.OrdinalIgnoreCase))
                {
                    model.Gender = "M";
                }
                else if (eventDetails.Title.Contains("girls", StringComparison.OrdinalIgnoreCase))
                {
                    model.Gender = "W";
                }
            }

            model.UpdatedAt = DateTime.UtcNow;

            if (model.Id == Guid.Empty)
            {
                model.Id = Guid.NewGuid();
                _context.EventQualifiers.Add(model);
                TempData["SuccessMessage"] = "New participant successfully added.";
            }
            else
            {
                _context.EventQualifiers.Update(model);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Manage), new { eventId = model.EventId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkSave(List<EventQualifier> qualifiers, long eventId)
        {
            var currentCoordinatorId = GetCurrentCoordinatorId();
            var isAdmin = User.IsInRole("Admin");

            var eventDetails = await _context.Events.FirstOrDefaultAsync(e => e.Id == eventId);
            if (eventDetails == null) return NotFound("Event not found.");

            if (!isAdmin && eventDetails.CoordinatorId != currentCoordinatorId)
            {
                return Forbid();
            }

            if (qualifiers != null && qualifiers.Any())
            {
                foreach (var q in qualifiers)
                {
                    var existing = await _context.EventQualifiers.FirstOrDefaultAsync(x => x.Id == q.Id && x.EventId == eventId);
                    if (existing != null)
                    {
                        var pName = string.IsNullOrWhiteSpace(q.ParticipantName) ? "" : System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(q.ParticipantName.Trim().ToLowerInvariant());
                        existing.ParticipantName = pName;
                        existing.Team = q.Team;         // Maps to the "district" column in the DB
                        existing.Role = string.IsNullOrWhiteSpace(q.Role) ? "Athlete" : q.Role;
                        existing.TshirtSize = q.TshirtSize;
                        if (!string.IsNullOrWhiteSpace(q.Gender)) existing.Gender = q.Gender;
                        existing.UpdatedAt = DateTime.UtcNow;
                        _context.EventQualifiers.Update(existing);
                    }
                }
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "All participant changes saved successfully.";
            }

            return RedirectToAction(nameof(Manage), new { eventId = eventId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkDelete(List<Guid> selectedIds, long eventId)
        {
            var currentCoordinatorId = GetCurrentCoordinatorId();
            var isAdmin = User.IsInRole("Admin");

            var eventDetails = await _context.Events.FirstOrDefaultAsync(e => e.Id == eventId);
            if (eventDetails == null) return NotFound("Event not found.");

            if (!isAdmin && eventDetails.CoordinatorId != currentCoordinatorId)
            {
                return Forbid();
            }

            if (selectedIds != null && selectedIds.Any())
            {
                var itemsToRemove = await _context.EventQualifiers
                    .Where(q => selectedIds.Contains(q.Id) && q.EventId == eventId)
                    .ToListAsync();

                if (itemsToRemove.Any())
                {
                    _context.EventQualifiers.RemoveRange(itemsToRemove);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"{itemsToRemove.Count} participant(s) deleted successfully.";
                }
            }
            else
            {
                TempData["ErrorMessage"] = "Please select at least one participant to delete.";
            }

            return RedirectToAction(nameof(Manage), new { eventId = eventId });
        }

        // Action for Generate Report View
        [HttpGet]
        public async Task<IActionResult> GenerateReport()
        {
            var qualifiersQuery = await (from q in _context.EventQualifiers
                                         join e in _context.Events on q.EventId equals e.Id
                                         select new
                                         {
                                             Qualifier = q,
                                             EventTitle = e.Title ?? "General",
                                             SportName = e.SportName ?? "General Sport",
                                             Division = e.Division ?? "Uncategorized"
                                         }).ToListAsync();

            // Order helper for Roles: Athletes first, then Coach, Assistant Coach, Chaperon at the end of the sport
            int GetRoleOrder(string? role)
            {
                if (string.IsNullOrWhiteSpace(role)) return 0;
                var r = role.Trim().ToLower();
                if (r == "athlete") return 0;
                if (r == "coach") return 1;
                if (r == "assistant coach") return 2;
                if (r == "chaperon" || r == "chaperone") return 3;
                return 4;
            }

            // Order helper for Gender: Girls first, then Boys
            int GetGenderOrder(string? gender)
            {
                if (string.IsNullOrWhiteSpace(gender)) return 2;
                var g = gender.Trim().ToLower();
                if (g == "w" || g == "female" || g == "girls" || g == "girl") return 0;
                if (g == "m" || g == "male" || g == "boys" || g == "boy") return 1;
                return 2;
            }

            var reportData = qualifiersQuery
                .OrderBy(x => x.Division.ToLower() == "elementary" ? 0 : (x.Division.ToLower() == "secondary" ? 1 : 2))
                .ThenBy(x => x.SportName)
                .ThenBy(x => x.EventTitle)
                .ThenBy(x => GetRoleOrder(x.Qualifier.Role))
                .ThenBy(x => GetGenderOrder(x.Qualifier.Gender))
                .ThenBy(x => x.Qualifier.Team)
                .ThenBy(x => x.Qualifier.ParticipantName)
                .ToList();

            return View(reportData);
        }
    }
}
