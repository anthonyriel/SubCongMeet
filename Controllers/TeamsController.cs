using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using SubcongMeet.Data;
using System.Linq;
using System.Threading.Tasks;

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

        public async Task<IActionResult> Details(int id)
        {
            var team = await _context.Teams.FindAsync(id);
            if (team == null) return NotFound();

            var teamEvents = await _context.Events
                .Where(e => e.Status == "Completed" && 
                           (e.GoldTeamId == id || e.SilverTeamId == id || e.BronzeTeamId == id))
                .OrderByDescending(e => e.UpdatedAt)
                .ToListAsync();

            // Isolate rank calculation per division to keep it accurate
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
                    // Rank remains the same for a tie
                }
                else
                {
                    displayRank = runningCount;
                }

                if (tally.TeamId == id) 
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
            ViewBag.Rank = finalRank; 
            
            return View(teamEvents);
        }
    }
}