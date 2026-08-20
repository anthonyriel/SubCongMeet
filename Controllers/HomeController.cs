using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization; 
using SubcongMeet.Data;
using SubcongMeet.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SubcongMeet.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var tallies = await _context.MedalTallies
                .Include(t => t.Team)
                .OrderByDescending(t => t.Gold)
                .ThenByDescending(t => t.Silver)
                .ThenByDescending(t => t.Bronze)
                .ToListAsync();

            var recentResults = await _context.Events
                .Where(g => g.Status == "Completed")
                .OrderByDescending(g => g.UpdatedAt)
                .ToListAsync();

            var totalEvents = await _context.Events.CountAsync();

            ViewBag.RecentResults = recentResults;
            ViewBag.TotalEvents = totalEvents;

            return View(tallies);
        }
        
        [Authorize(Roles = "Admin")] 
        public async Task<IActionResult> GeneralOfficialReport()
        {
            var medalTallies = await _context.MedalTallies
                .Include(m => m.Team)
                .OrderByDescending(m => m.Gold)
                .ThenByDescending(m => m.Silver)
                .ThenByDescending(m => m.Bronze)
                .ToListAsync();

            return View(medalTallies);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AllEventsResultsReport(List<string> sportsName, List<int> eventId, List<string> division, List<int> teamId)
        {
            var query = _context.Events.AsQueryable();

            if (sportsName != null && sportsName.Any())
            {
                query = query.Where(e => e.SportName != null && sportsName.Contains(e.SportName));
            }

            if (eventId != null && eventId.Any())
            {
                query = query.Where(e => eventId.Contains(e.Id));
            }

            if (division != null && division.Any())
            {
                query = query.Where(e => e.Division != null && division.Contains(e.Division));
            }

            if (teamId != null && teamId.Any())
            {
                query = query.Where(e => (e.GoldTeamId.HasValue && teamId.Contains(e.GoldTeamId.Value)) || 
                                       (e.SilverTeamId.HasValue && teamId.Contains(e.SilverTeamId.Value)) || 
                                       (e.BronzeTeamId.HasValue && teamId.Contains(e.BronzeTeamId.Value)));
            }

            var eventsList = await query
                .OrderBy(e => e.Title)
                .ToListAsync();

            ViewBag.SportsList = await _context.Events
                .Where(e => !string.IsNullOrEmpty(e.SportName))
                .Select(e => e.SportName)
                .Distinct()
                .OrderBy(s => s)
                .Select(s => new SelectListItem { Value = s, Text = s })
                .ToListAsync();

            ViewBag.EventsList = await _context.Events
                .OrderBy(e => e.Title)
                .Select(e => new SelectListItem { Value = e.Id.ToString(), Text = e.Title })
                .Distinct()
                .ToListAsync();

            ViewBag.DivisionsList = await _context.Teams
                .Where(t => !string.IsNullOrEmpty(t.Division))
                .Select(t => t.Division)
                .Distinct()
                .OrderBy(d => d)
                .Select(d => new SelectListItem { Value = d, Text = d })
                .ToListAsync();

            ViewBag.TeamsList = await _context.Teams
                .OrderBy(t => t.Name)
                .ToListAsync();

            return View(eventsList);
        }

        [Authorize(Roles = "Admin,Coordinator")]
        public async Task<IActionResult> DistrictQualifierReport(List<string> sportsName, List<long> eventId, List<string> division, List<string> teamName)
        {
            var joinedQuery = from q in _context.EventQualifiers
                              join e in _context.Events on q.EventId equals e.Id
                              select new { Qualifier = q, Event = e };

            if (sportsName != null && sportsName.Any())
            {
                joinedQuery = joinedQuery.Where(x => x.Event.SportName != null && sportsName.Contains(x.Event.SportName));
            }

            if (eventId != null && eventId.Any())
            {
                joinedQuery = joinedQuery.Where(x => eventId.Contains(x.Event.Id));
            }

            if (division != null && division.Any())
            {
                joinedQuery = joinedQuery.Where(x => division.Contains(x.Event.Division));
            }

            if (teamName != null && teamName.Any())
            {
                joinedQuery = joinedQuery.Where(x => x.Qualifier.Team != null && teamName.Contains(x.Qualifier.Team));
            }

            var rawList = await joinedQuery.ToListAsync();

            // Strictly order by Sports Name -> Event Title -> Role (Athlete, Coach, Chaperon) -> Participant Name
            var sortedQualifiers = rawList
                .OrderBy(x => x.Event.SportName ?? "")
                .ThenBy(x => x.Event.Title ?? "")
                .ThenBy(x => {
                    string r = x.Qualifier.Role?.ToLower() ?? "";
                    if (r.Contains("athlete")) return 1;
                    if (r.Contains("coach")) return 2;
                    if (r.Contains("chaperon") || r.Contains("chaperone")) return 3;
                    return 4;
                })
                .ThenBy(x => x.Qualifier.ParticipantName ?? "")
                .Select(x => x.Qualifier)
                .ToList();

            ViewBag.SportsList = await _context.Events
                .Where(e => !string.IsNullOrEmpty(e.SportName))
                .Select(e => e.SportName)
                .Distinct()
                .OrderBy(s => s)
                .Select(s => new SelectListItem { Value = s, Text = s })
                .ToListAsync();

            ViewBag.EventsList = await _context.Events
                .OrderBy(e => e.Title)
                .Select(e => new SelectListItem { Value = e.Id.ToString(), Text = e.Title })
                .Distinct()
                .ToListAsync();

            ViewBag.DivisionsList = await _context.Teams
                .Where(t => !string.IsNullOrEmpty(t.Division))
                .Select(t => t.Division)
                .Distinct()
                .OrderBy(d => d)
                .Select(d => new SelectListItem { Value = d, Text = d })
                .ToListAsync();

            ViewBag.TeamNamesList = await _context.EventQualifiers
                .Where(q => !string.IsNullOrEmpty(q.Team))
                .Select(q => q.Team)
                .Distinct()
                .OrderBy(s => s)
                .Select(s => new SelectListItem { Value = s, Text = s })
                .ToListAsync();

            return View(sortedQualifiers);
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Coordinator")]
        public async Task<IActionResult> EditQualifiers(List<string> sportsName, List<long> eventId, List<string> division, List<string> teamName)
        {
            var joinedQuery = from q in _context.EventQualifiers
                              join e in _context.Events on q.EventId equals e.Id
                              select new { Qualifier = q, Event = e };

            if (sportsName != null && sportsName.Any())
            {
                joinedQuery = joinedQuery.Where(x => x.Event.SportName != null && sportsName.Contains(x.Event.SportName));
            }

            if (eventId != null && eventId.Any())
            {
                joinedQuery = joinedQuery.Where(x => eventId.Contains(x.Event.Id));
            }

            if (division != null && division.Any())
            {
                joinedQuery = joinedQuery.Where(x => division.Contains(x.Event.Division));
            }

            if (teamName != null && teamName.Any())
            {
                joinedQuery = joinedQuery.Where(x => x.Qualifier.Team != null && teamName.Contains(x.Qualifier.Team));
            }

            var rawList = await joinedQuery.ToListAsync();

            // Strict multi-level sorting: Division -> Sports -> Event Title -> Role (Athlete, Coach, Chaperon) -> Participant Name
            var sortedQualifiers = rawList
                .OrderBy(x => x.Event.Division ?? "")
                .ThenBy(x => x.Event.SportName ?? "")
                .ThenBy(x => x.Event.Title ?? "")
                .ThenBy(x => {
                    string r = x.Qualifier.Role?.ToLower() ?? "";
                    if (r.Contains("athlete")) return 1;
                    if (r.Contains("coach")) return 2;
                    if (r.Contains("chaperon") || r.Contains("chaperone")) return 3;
                    return 4;
                })
                .ThenBy(x => x.Qualifier.ParticipantName ?? "")
                .Select(x => x.Qualifier)
                .ToList();

            ViewBag.SportsList = await _context.Events
                .Where(e => !string.IsNullOrEmpty(e.SportName))
                .Select(e => e.SportName)
                .Distinct()
                .OrderBy(s => s)
                .Select(s => new SelectListItem { Value = s, Text = s })
                .ToListAsync();

            ViewBag.EventsList = await _context.Events
                .OrderBy(e => e.Title)
                .Select(e => new SelectListItem { Value = e.Id.ToString(), Text = e.Title })
                .Distinct()
                .ToListAsync();

            ViewBag.DivisionsList = await _context.Teams
                .Where(t => !string.IsNullOrEmpty(t.Division))
                .Select(t => t.Division)
                .Distinct()
                .OrderBy(d => d)
                .Select(d => new SelectListItem { Value = d, Text = d })
                .ToListAsync();

            ViewBag.TeamNamesList = await _context.EventQualifiers
                .Where(q => !string.IsNullOrEmpty(q.Team))
                .Select(q => q.Team)
                .Distinct()
                .OrderBy(s => s)
                .Select(s => new SelectListItem { Value = s, Text = s })
                .ToListAsync();

            ViewBag.TeamsList = await _context.Teams.OrderBy(t => t.Name).ToListAsync();
            ViewBag.AllEvents = await _context.Events.OrderBy(e => e.SportName).ThenBy(e => e.Title).ToListAsync();

            return View(sortedQualifiers);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Coordinator")]
        public async Task<IActionResult> BulkSaveQualifiers(List<EventQualifier> qualifiers)
        {
            if (qualifiers != null && qualifiers.Any())
            {
                foreach (var q in qualifiers)
                {
                    var existing = await _context.EventQualifiers.FindAsync(q.Id);
                    if (existing != null)
                    {
                        existing.EventId = q.EventId;
                        existing.ParticipantName = q.ParticipantName;
                        existing.Team = q.Team; 
                        existing.Role = q.Role;
                        existing.TshirtSize = q.TshirtSize;
                        existing.UpdatedAt = DateTime.UtcNow;
                        _context.EventQualifiers.Update(existing);
                    }
                }
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "All qualifier updates saved successfully.";
            }
            return RedirectToAction(nameof(EditQualifiers));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Coordinator")]
        public async Task<IActionResult> BulkDeleteQualifiers(List<Guid> selectedIds)
        {
            if (selectedIds != null && selectedIds.Any())
            {
                var itemsToRemove = await _context.EventQualifiers
                    .Where(q => selectedIds.Contains(q.Id))
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
            return RedirectToAction(nameof(EditQualifiers));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Coordinator")]
        public async Task<IActionResult> SaveQualifierModal(EventQualifier model)
        {
            if (ModelState.IsValid)
            {
                model.UpdatedAt = DateTime.UtcNow;
                if (model.Id == Guid.Empty)
                {
                    model.Id = Guid.NewGuid();
                    _context.EventQualifiers.Add(model);
                }
                else
                {
                    _context.EventQualifiers.Update(model);
                }
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "New participant successfully added.";
            }
            return RedirectToAction(nameof(EditQualifiers));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Coordinator")]
        public async Task<IActionResult> DeleteQualifier(Guid id)
        {
            var qualifier = await _context.EventQualifiers.FindAsync(id);
            if (qualifier != null)
            {
                _context.EventQualifiers.Remove(qualifier);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Participant successfully deleted.";
            }
            return RedirectToAction(nameof(EditQualifiers));
        }
    }
}