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
                .OrderBy(q => q.Role)
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
            
            return View(qualifiers);
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
                        existing.SchoolName = q.SchoolName;
                        existing.Role = string.IsNullOrWhiteSpace(q.Role) ? "Athlete" : q.Role;
                        existing.TshirtSize = q.TshirtSize;
                        if (!string.IsNullOrWhiteSpace(q.School)) existing.School = q.School;
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
    }
}