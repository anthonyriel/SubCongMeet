using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using SubcongMeet.Data;
using SubcongMeet.Models;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace SubcongMeet.Controllers
{
    [AllowAnonymous] 
    public class TeamsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TeamsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Dashboard()
        {
            var teams = await _context.Teams.OrderBy(t => t.Division).ThenBy(t => t.Name).ToListAsync();
            return View(teams);
        }

        // Helper to categorize teams into municipalities/areas
        private string GetMunicipality(string teamName)
        {
            if (string.IsNullOrEmpty(teamName)) return "Calape";
            if (teamName.Contains("Loon", StringComparison.OrdinalIgnoreCase)) return "Loon";
            if (teamName.Contains("Tubigon", StringComparison.OrdinalIgnoreCase)) return "Tubigon";
            return "Calape"; // Default area
        }

        public async Task<IActionResult> Details(int? id, string name)
        {
            // Case 1: Normal Single-Division View (Elementary or Secondary clicked)
            if (id.HasValue)
            {
                var team = await _context.Teams.FindAsync(id.Value);
                if (team == null) return NotFound();

                var teamEvents = await _context.Events
                    .Where(e => e.Status == "Completed" && 
                               (e.GoldTeamId == id.Value || e.SilverTeamId == id.Value || e.BronzeTeamId == id.Value))
                    .OrderByDescending(e => e.UpdatedAt)
                    .ToListAsync();

                // Calculate rank specifically within this team's division
                var tallies = await _context.MedalTallies
                    .Include(t => t.Team)
                    .Where(t => t.Team != null && t.Team.Division == team.Division)
                    .OrderByDescending(t => t.Gold)
                    .ThenByDescending(t => t.Silver)
                    .ThenByDescending(t => t.Bronze)
                    .ToListAsync();

                int displayRank = 1;
                int runningCount = 1;
                int? prevGold = null, prevSilver = null, prevBronze = null;
                string finalRank = "N/A";

                foreach (var tally in tallies)
                {
                    if (prevGold.HasValue && prevSilver.HasValue && prevBronze.HasValue &&
                        tally.Gold == prevGold.Value && tally.Silver == prevSilver.Value && tally.Bronze == prevBronze.Value)
                    {
                        // Tie rank
                    }
                    else
                    {
                        displayRank = runningCount;
                    }

                    if (tally.TeamId == id.Value) 
                    {
                        finalRank = displayRank.ToString();
                        break;
                    }

                    prevGold = tally.Gold;
                    prevSilver = tally.Silver;
                    prevBronze = tally.Bronze;
                    runningCount++;
                }

                ViewBag.Team = team;
                ViewBag.TeamIds = new List<int> { id.Value };
                ViewBag.Rank = finalRank;
                ViewBag.IsOverall = false;

                return View(teamEvents);
            }
            // Case 2: Combined Overall View (All Divisions card clicked)
            else if (!string.IsNullOrEmpty(name))
            {
                var matchedTeams = await _context.Teams
                    .Where(t => t.Name.Contains(name) || t.Acronym.Contains(name))
                    .ToListAsync();

                if (!matchedTeams.Any()) return NotFound();

                var teamIds = matchedTeams.Select(t => t.Id).ToList();

                var teamEvents = await _context.Events
                    .Where(e => e.Status == "Completed" && 
                                (teamIds.Contains(e.GoldTeamId ?? 0) || 
                                 teamIds.Contains(e.SilverTeamId ?? 0) || 
                                 teamIds.Contains(e.BronzeTeamId ?? 0)))
                    .OrderByDescending(e => e.UpdatedAt)
                    .ToListAsync();

                // Calculate Combined Overall Rank across all municipalities
                var allTeams = await _context.Teams.ToListAsync();
                var allTallies = await _context.MedalTallies.ToListAsync();

                var municipalityGroups = allTeams.GroupBy(t => GetMunicipality(t.Name)).ToList();
                var municipalityRankings = new List<MunicipalityRankDto>();

                foreach (var group in municipalityGroups)
                {
                    var mTeamIds = group.Select(t => t.Id).ToList();
                    var mTallies = allTallies.Where(t => mTeamIds.Contains(t.TeamId)).ToList();

                    municipalityRankings.Add(new MunicipalityRankDto
                    {
                        MunicipalityName = group.Key,
                        TeamIds = mTeamIds,
                        Gold = mTallies.Sum(t => t.Gold),
                        Silver = mTallies.Sum(t => t.Silver),
                        Bronze = mTallies.Sum(t => t.Bronze)
                    });
                }

                // Sort by Gold desc, Silver desc, Bronze desc
                municipalityRankings = municipalityRankings
                    .OrderByDescending(x => x.Gold)
                    .ThenByDescending(x => x.Silver)
                    .ThenByDescending(x => x.Bronze)
                    .ToList();

                int currentRank = 1;
                int runningCount = 1;
                int? pGold = null, pSilver = null, pBronze = null;
                string overallRankStr = "1";

                foreach (var mun in municipalityRankings)
                {
                    if (pGold.HasValue && pSilver.HasValue && pBronze.HasValue &&
                        mun.Gold == pGold.Value && mun.Silver == pSilver.Value && mun.Bronze == pBronze.Value)
                    {
                        // Tie rank
                    }
                    else
                    {
                        currentRank = runningCount;
                    }

                    if (mun.TeamIds.Any(id => teamIds.Contains(id)))
                    {
                        overallRankStr = currentRank.ToString();
                        break;
                    }

                    pGold = mun.Gold;
                    pSilver = mun.Silver;
                    pBronze = mun.Bronze;
                    runningCount++;
                }

                ViewBag.Team = matchedTeams.First();
                ViewBag.TeamIds = teamIds;
                ViewBag.Rank = overallRankStr;
                ViewBag.IsOverall = true;

                return View(teamEvents);
            }

            return NotFound();
        }
    }

    public class MunicipalityRankDto
    {
        public string MunicipalityName { get; set; } = string.Empty;
        public List<int> TeamIds { get; set; } = new List<int>();
        public int Gold { get; set; }
        public int Silver { get; set; }
        public int Bronze { get; set; }
    }
}