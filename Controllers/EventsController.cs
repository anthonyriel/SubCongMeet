using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering; 
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using SubcongMeet.Data;
using SubcongMeet.Models;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SubcongMeet.Controllers
{
    [Authorize]
    public class EventsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public EventsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Helper method to safely extract the CoordinatorId regardless of how the login generated the claims
        private int? GetCurrentCoordinatorId()
        {
            var claimValue = User.FindFirst("CoordinatorId")?.Value 
                ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("Id")?.Value;

            if (int.TryParse(claimValue, out int parsedId))
            {
                return parsedId;
            }
            return null;
        }

        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
            var events = await _context.Events.OrderBy(e => e.Division).ThenBy(e => e.SportName).ThenBy(e => e.Title).ToListAsync();
            ViewBag.Teams = await _context.Teams.OrderBy(t => t.Name).ToListAsync();
            return View(events);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateResult(int eventId, int? goldTeamId, int? silverTeamId, int? bronzeTeamId)
        {
            var ev = await _context.Events.FindAsync(eventId);
            if (ev == null) return NotFound();

            bool isAdmin = User.IsInRole("Admin");
            int? currentCoordinatorId = GetCurrentCoordinatorId();

            if (!isAdmin && ev.CoordinatorId != currentCoordinatorId)
            {
                return Forbid();
            }

            ev.GoldTeamId = goldTeamId;
            ev.SilverTeamId = silverTeamId;
            ev.BronzeTeamId = bronzeTeamId;
            ev.Status = (goldTeamId != null || silverTeamId != null || bronzeTeamId != null) ? "Completed" : "Pending";
            ev.UpdatedAt = DateTime.UtcNow;

            _context.Update(ev);
            await _context.SaveChangesAsync();
            await RecalculateMedalTally(); 

            return RedirectToAction(nameof(Index));
        }

        private async Task RecalculateMedalTally()
        {
            var currentTallies = await _context.MedalTallies.ToListAsync();
            _context.MedalTallies.RemoveRange(currentTallies);
            
            var teams = await _context.Teams.ToListAsync();
            var completedEvents = await _context.Events.Where(e => e.Status == "Completed").ToListAsync();

            foreach (var team in teams)
            {
                var goldCount = completedEvents.Count(e => e.GoldTeamId == team.Id);
                var silverCount = completedEvents.Count(e => e.SilverTeamId == team.Id);
                var bronzeCount = completedEvents.Count(e => e.BronzeTeamId == team.Id);

                var tally = new MedalTally
                {
                    TeamId = team.Id,
                    Gold = goldCount,
                    Silver = silverCount,
                    Bronze = bronzeCount
                };
                _context.MedalTallies.Add(tally);
            }
            await _context.SaveChangesAsync();
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create()
        {
            ViewBag.Teams = await _context.Teams.ToListAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([Bind("Title,SportName,SportCategory,Division,Status,CoordinatorId")] Event ev)
        {
            if (ModelState.IsValid)
            {
                ev.CreatedAt = DateTime.UtcNow;
                ev.UpdatedAt = DateTime.UtcNow;
                
                _context.Add(ev);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(ManageEvents));
            }
            
            return View(ev);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var ev = await _context.Events.FindAsync(id);
            if (ev == null) return NotFound();

            bool isAdmin = User.IsInRole("Admin");
            int? currentCoordinatorId = GetCurrentCoordinatorId();

            if (!isAdmin && ev.CoordinatorId != currentCoordinatorId)
            {
                return Forbid();
            }

            ViewBag.Teams = await _context.Teams.ToListAsync();
            ViewBag.Coordinators = new SelectList(await _context.Coordinators.OrderBy(c => c.FullName).ToListAsync(), "Id", "FullName", ev.CoordinatorId);
            
            return View(ev);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Title,SportName,SportCategory,Division,EliminationType,TeamAId,TeamBId,CoordinatorId,Status")] Event ev)
        {
            if (id != ev.Id) return NotFound();

            if (ModelState.IsValid)
            {
                var existingEvent = await _context.Events.FindAsync(id);
                if (existingEvent == null) return NotFound();

                existingEvent.Title = ev.Title;
                existingEvent.SportName = ev.SportName;
                existingEvent.SportCategory = ev.SportCategory;
                existingEvent.Division = ev.Division;
                existingEvent.CoordinatorId = ev.CoordinatorId;
                existingEvent.Status = ev.Status;
                existingEvent.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await RecalculateMedalTally(); 
                
                return RedirectToAction(nameof(ManageEvents));
            }
            
            ViewBag.Teams = await _context.Teams.ToListAsync();
            ViewBag.Coordinators = new SelectList(await _context.Coordinators.OrderBy(c => c.FullName).ToListAsync(), "Id", "FullName", ev.CoordinatorId);
            
            return View(ev);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var ev = await _context.Events.FindAsync(id);
            if (ev != null)
            {
                _context.Events.Remove(ev);
                await _context.SaveChangesAsync();
                await RecalculateMedalTally(); 
            }
            return RedirectToAction(nameof(ManageEvents));
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ManageEvents()
        {
            var events = await _context.Events.OrderBy(e => e.Division).ThenBy(e => e.SportName).ThenBy(e => e.Title).ToListAsync();
            ViewBag.Coordinators = await _context.Coordinators.OrderBy(c => c.FullName).ToListAsync();
            return View(events);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateAllEventCoordinators(Dictionary<int, int?> assignments)
        {
            if (assignments != null)
            {
                foreach (var kvp in assignments)
                {
                    var ev = await _context.Events.FindAsync(kvp.Key);
                    if (ev != null)
                    {
                        ev.CoordinatorId = kvp.Value;
                        ev.UpdatedAt = DateTime.UtcNow;
                    }
                }
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(ManageEvents));
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> SubmitResults(int id)
        {
            var ev = await _context.Events.FindAsync(id);
            if (ev == null) return NotFound();

            ViewBag.Teams = await _context.Teams.Where(t => t.Division == ev.Division).OrderBy(t => t.Name).ToListAsync();
            return View(ev);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitResults(Event model)
        {
            var eventToUpdate = await _context.Events.FindAsync(model.Id);
            if (eventToUpdate == null) return NotFound();

            bool isAdmin = User.IsInRole("Admin");
            int? currentCoordinatorId = GetCurrentCoordinatorId();

            if (!isAdmin && eventToUpdate.CoordinatorId != currentCoordinatorId)
            {
                return Forbid();
            }

            var allTeams = await _context.Teams.ToListAsync();

            eventToUpdate.GoldTeamId = model.GoldTeamId;
            eventToUpdate.SilverTeamId = model.SilverTeamId;
            eventToUpdate.BronzeTeamId = model.BronzeTeamId;
            eventToUpdate.GoldWinnerName = model.GoldWinnerName;
            eventToUpdate.SilverWinnerName = model.SilverWinnerName;
            eventToUpdate.BronzeWinnerName = model.BronzeWinnerName;
            eventToUpdate.LastUpdatedBy = User.FindFirst("FullName")?.Value ?? User.Identity?.Name;
            eventToUpdate.UpdatedAt = DateTime.UtcNow;
            eventToUpdate.Status = "Completed";

            async Task ProcessQualifiers(long? teamId, string winnerNames, string role)
            {
                if (teamId == null || string.IsNullOrWhiteSpace(winnerNames)) return;

                var teamName = allTeams.FirstOrDefault(t => t.Id == teamId)?.Name ?? "Unknown School";
                var names = winnerNames.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var name in names)
                {
                    var cleanName = name.Trim();
                    if (string.IsNullOrEmpty(cleanName)) continue;

                    var existing = await _context.EventQualifiers
                        .FirstOrDefaultAsync(q => q.EventId == model.Id && q.ParticipantName == cleanName);

                    if (existing != null)
                    {
                        existing.SchoolName = teamName;
                        existing.Role = role;
                        existing.UpdatedAt = DateTime.UtcNow;
                    }
                    else
                    {
                        _context.EventQualifiers.Add(new EventQualifier
                        {
                            Id = Guid.NewGuid(),
                            EventId = model.Id,
                            ParticipantName = cleanName,
                            SchoolName = teamName,
                            Role = role,
                            UpdatedAt = DateTime.UtcNow
                        });
                    }
                }
            }

            await ProcessQualifiers(model.GoldTeamId, model.GoldWinnerName ?? string.Empty, "Athlete");
            await ProcessQualifiers(model.SilverTeamId, model.SilverWinnerName ?? string.Empty, "Athlete");
            await ProcessQualifiers(model.BronzeTeamId, model.BronzeWinnerName ?? string.Empty, "Athlete");

            await _context.SaveChangesAsync();
            await RecalculateMedalTally(); 

            TempData["SuccessMessage"] = "Official results published successfully.";
            return RedirectToAction(nameof(SubmitResults), new { id = model.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearResults(int id) 
        {
            var eventToClear = await _context.Events.FindAsync(id);
            if (eventToClear == null) return NotFound();

            bool isAdmin = User.IsInRole("Admin");
            int? currentCoordinatorId = GetCurrentCoordinatorId();

            if (!isAdmin && eventToClear.CoordinatorId != currentCoordinatorId)
            {
                return Forbid();
            }

            eventToClear.GoldTeamId = null;
            eventToClear.SilverTeamId = null;
            eventToClear.BronzeTeamId = null;
            eventToClear.GoldWinnerName = null;
            eventToClear.SilverWinnerName = null;
            eventToClear.BronzeWinnerName = null;
            eventToClear.Status = "Pending"; 
            eventToClear.LastUpdatedBy = User.FindFirst("FullName")?.Value ?? User.Identity?.Name;
            eventToClear.UpdatedAt = DateTime.UtcNow;

            var qualifiers = await _context.EventQualifiers
                .Where(q => q.EventId == id)
                .ToListAsync();
            
            if (qualifiers.Any())
            {
                _context.EventQualifiers.RemoveRange(qualifiers);
            }

            await _context.SaveChangesAsync();
            await RecalculateMedalTally(); 

            TempData["SuccessMessage"] = "Event data cleared successfully.";
            return RedirectToAction(nameof(SubmitResults), new { id = id });
        }
    }
}