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
            var tallies = await _context.GetTeamStandings()
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
            var medalTallies = await _context.GetTeamStandings()
                .OrderByDescending(m => m.Gold)
                .ThenByDescending(m => m.Silver)
                .ThenByDescending(m => m.Bronze)
                .ToListAsync();

            return View(medalTallies);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> PlayerMedalReport(string? sport, string? category, string? division)
        {
            var options = await _context.Events.AsNoTracking()
                .Select(e => new { e.SportName, e.SportCategory, e.Division }).Distinct().ToListAsync();
            var query = _context.Events.AsNoTracking().Where(e => e.Status == "Completed");
            if (!string.IsNullOrEmpty(sport)) query = query.Where(e => e.SportName == sport);
            if (!string.IsNullOrEmpty(category)) query = query.Where(e => e.SportCategory == category);
            if (!string.IsNullOrEmpty(division)) query = query.Where(e => e.Division == division);
            var events = await query.Select(e => new Event {
                Title = e.Title, Division = e.Division, Status = e.Status,
                GoldTeamId = e.GoldTeamId, SilverTeamId = e.SilverTeamId, BronzeTeamId = e.BronzeTeamId,
                GoldWinnerName = e.GoldWinnerName, SilverWinnerName = e.SilverWinnerName, BronzeWinnerName = e.BronzeWinnerName
            }).ToListAsync();
            var teams = await _context.Teams.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Name);
            return View(new Models.PlayerMedalReport {
                Players = Models.PlayerMedalReport.CountMedals(events, teams),
                Sports = options.Select(e => e.SportName).OfType<string>().Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().OrderBy(s => s).ToList(),
                Categories = options.Select(e => e.SportCategory).OfType<string>().Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().OrderBy(s => s).ToList(),
                Divisions = options.Select(e => e.Division).Distinct().OrderBy(s => s).ToList(),
                Sport = sport, Category = category, Division = division
            });
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> PlayerEventReport(string? sport, string? category, string? division, string? player, string? district)
        {
            var options = await _context.Events.AsNoTracking()
                .Select(e => new { e.SportName, e.SportCategory, e.Division }).Distinct().ToListAsync();
            var query = _context.Events.AsNoTracking().Where(e => e.Status == "Completed");
            if (!string.IsNullOrEmpty(sport)) query = query.Where(e => e.SportName == sport);
            if (!string.IsNullOrEmpty(category)) query = query.Where(e => e.SportCategory == category);
            if (!string.IsNullOrEmpty(division)) query = query.Where(e => e.Division == division);
            var events = await query.Select(e => new Event {
                Title = e.Title, SportName = e.SportName, SportCategory = e.SportCategory, Division = e.Division, Status = e.Status,
                GoldTeamId = e.GoldTeamId, SilverTeamId = e.SilverTeamId, BronzeTeamId = e.BronzeTeamId,
                GoldWinnerName = e.GoldWinnerName, SilverWinnerName = e.SilverWinnerName, BronzeWinnerName = e.BronzeWinnerName
            }).ToListAsync();
            var teams = await _context.Teams.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Name);
            return View(new Models.PlayerEventReport {
                Results = Models.PlayerEventReport.Build(events, teams, player, district),
                Player = player, District = district, Districts = teams.Values.Distinct().OrderBy(t => t).ToList(),
                Sports = options.Select(e => e.SportName).OfType<string>().Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().OrderBy(s => s).ToList(),
                Categories = options.Select(e => e.SportCategory).OfType<string>().Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().OrderBy(s => s).ToList(),
                Divisions = options.Select(e => e.Division).Distinct().OrderBy(s => s).ToList(),
                Sport = sport, Category = category, Division = division
            });
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AllEventsResultsReport(List<string> sportsName, List<int> eventId, List<string> division, List<int> schoolId)
        {
            var query = _context.Events.AsNoTracking().AsQueryable();

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

            if (schoolId != null && schoolId.Any())
            {
                query = query.Where(e => 
                    (e.GoldTeamId.HasValue && schoolId.Contains(e.GoldTeamId.Value)) || 
                    (e.SilverTeamId.HasValue && schoolId.Contains(e.SilverTeamId.Value)) || 
                    (e.BronzeTeamId.HasValue && schoolId.Contains(e.BronzeTeamId.Value)));
            }

            var resultList = await query
                .OrderBy(e => e.Title)
                .ToListAsync();

            // Populate filter dropdowns with AsNoTracking for ultra-fast loading
            ViewBag.SportsList = await _context.Events.AsNoTracking()
                .Where(e => !string.IsNullOrEmpty(e.SportName))
                .Select(e => e.SportName)
                .Distinct()
                .OrderBy(s => s)
                .Select(s => new SelectListItem { Value = s, Text = s })
                .ToListAsync();

            ViewBag.EventsList = await _context.Events.AsNoTracking()
                .OrderBy(e => e.Title)
                .Select(e => new SelectListItem { Value = e.Id.ToString(), Text = e.Title })
                .Distinct()
                .ToListAsync();

            ViewBag.DivisionsList = (await _context.Teams.AsNoTracking()
                .Where(t => !string.IsNullOrEmpty(t.Division))
                .Select(t => t.Division)
                .Distinct()
                .ToListAsync())
                .Union(new[] { "Elementary", "Secondary", "Paragames" })
                .OrderBy(d => d)
                .Select(d => new SelectListItem { Value = d, Text = d })
                .ToList();

            // Schools list for dropdown and name resolution
            ViewBag.SchoolsList = await _context.Teams.AsNoTracking()
                .OrderBy(t => t.Name)
                .ToListAsync();

            return View(resultList);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> PendingEventsReport(List<string> sportsName, List<int> eventId, List<string> division)
        {
            var query = _context.Events.AsNoTracking().AsQueryable();

            // Published events are complete even when no medals were awarded.
            query = query.Where(e => e.Status != "Completed");

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

            var resultList = await query
                .OrderBy(e => e.Title)
                .ToListAsync();

            ViewBag.SportsList = await _context.Events.AsNoTracking()
                .Where(e => !string.IsNullOrEmpty(e.SportName))
                .Select(e => e.SportName)
                .Distinct()
                .OrderBy(s => s)
                .Select(s => new SelectListItem { Value = s, Text = s })
                .ToListAsync();

            ViewBag.EventsList = await _context.Events.AsNoTracking()
                .OrderBy(e => e.Title)
                .Select(e => new SelectListItem { Value = e.Id.ToString(), Text = e.Title })
                .Distinct()
                .ToListAsync();

            ViewBag.DivisionsList = (await _context.Teams.AsNoTracking()
                .Where(t => !string.IsNullOrEmpty(t.Division))
                .Select(t => t.Division)
                .Distinct()
                .ToListAsync())
                .Union(new[] { "Elementary", "Secondary", "Paragames" })
                .OrderBy(d => d)
                .Select(d => new SelectListItem { Value = d, Text = d })
                .ToList();

            ViewBag.SchoolsList = await _context.Teams.AsNoTracking()
                .OrderBy(t => t.Name)
                .ToListAsync();

            return View(resultList);
        }

        [Authorize(Roles = "Admin,Coordinator")]
        public async Task<IActionResult> DistrictQualifierReport(List<string> sportsName, List<long> eventId, List<string> division, List<string> district)
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

            if (district != null && district.Any())
            {
                joinedQuery = joinedQuery.Where(x => x.Qualifier.Team != null && district.Contains(x.Qualifier.Team));
            }

            var rawList = await joinedQuery.ToListAsync();

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

            ViewBag.DivisionsList = (await _context.Teams
                .Where(t => !string.IsNullOrEmpty(t.Division))
                .Select(t => t.Division)
                .Distinct()
                .ToListAsync())
                .Union(new[] { "Elementary", "Secondary", "Paragames" })
                .OrderBy(d => d)
                .Select(d => new SelectListItem { Value = d, Text = d })
                .ToList();

            ViewBag.TeamNamesList = await _context.EventQualifiers
                .Where(q => !string.IsNullOrEmpty(q.Team))
                .Select(q => q.Team)
                .Distinct()
                .OrderBy(s => s)
                .Select(s => new SelectListItem { Value = s, Text = s })
                .ToListAsync();

            // Populate DistrictsList for the dropdown
            ViewBag.DistrictsList = await _context.EventQualifiers
                .Where(q => !string.IsNullOrEmpty(q.Team))
                .Select(q => q.Team)
                .Distinct()
                .OrderBy(d => d)
                .Select(d => new SelectListItem { Value = d, Text = d })
                .ToListAsync();

            return View(sortedQualifiers);
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Coordinator")]
        public async Task<IActionResult> EditQualifiers(List<string> sportsName, List<long> eventId, List<string> division, List<string> teamName)
        {
            var joinedQuery = from q in _context.EventQualifiers.AsNoTracking()
                              join e in _context.Events.AsNoTracking() on q.EventId equals e.Id
                              select new { Qualifier = q, Event = new { e.Id, e.Division, e.SportName, e.Title } };

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

            if (teamName == null || teamName.Count == 0)
                teamName = Request.Query["team"].Select(value => value ?? string.Empty).ToList();

            if (teamName != null && teamName.Any())
            {
                joinedQuery = joinedQuery.Where(x => x.Qualifier.Team != null && teamName.Contains(x.Qualifier.Team));
            }

            var rawList = await joinedQuery.ToListAsync();

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

            // Reuse the same read-only event and team data for every dropdown.
            var allEvents = await _context.Events.AsNoTracking()
                .OrderBy(e => e.SportName).ThenBy(e => e.Title)
                .Select(e => new Event { Id = e.Id, Title = e.Title, SportName = e.SportName, Division = e.Division })
                .ToListAsync();
            var teams = await _context.Teams.AsNoTracking().OrderBy(t => t.Name).ToListAsync();
            ViewBag.SportsList = allEvents.Where(e => !string.IsNullOrEmpty(e.SportName))
                .Select(e => e.SportName).Distinct().OrderBy(s => s)
                .Select(s => new SelectListItem { Value = s, Text = s }).ToList();
            ViewBag.EventsList = allEvents.OrderBy(e => e.Title)
                .Select(e => new SelectListItem { Value = e.Id.ToString(), Text = e.Title }).ToList();
            ViewBag.DivisionsList = teams.Where(t => !string.IsNullOrEmpty(t.Division))
                .Select(t => t.Division).Union(new[] { "Elementary", "Secondary", "Paragames" })
                .OrderBy(d => d).Select(d => new SelectListItem { Value = d, Text = d }).ToList();
            ViewBag.TeamNamesList = await _context.EventQualifiers.AsNoTracking()
                .Where(q => !string.IsNullOrEmpty(q.Team)).Select(q => q.Team).Distinct().OrderBy(t => t)
                .Select(t => new SelectListItem { Value = t, Text = t }).ToListAsync();
            ViewBag.TeamsList = teams;
            ViewBag.AllEvents = allEvents;
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
