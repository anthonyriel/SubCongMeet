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
            ViewBag.Teams = await _context.Teams.OrderBy(t => t.Name).ToListAsync();
            
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

            model.UpdatedAt = DateTime.UtcNow;

            if (model.Id == Guid.Empty)
            {
                model.Id = Guid.NewGuid();
                _context.EventQualifiers.Add(model);
                TempData["SuccessMessage"] = "New participant successfully added.";
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
                        existing.ParticipantName = q.ParticipantName;
                        existing.SchoolName = q.SchoolName;
                        existing.Role = q.Role;
                        existing.TshirtSize = q.TshirtSize;
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