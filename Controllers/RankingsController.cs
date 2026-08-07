using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SubcongMeet.Data;
using SubcongMeet.Models;
using System.Linq;
using System.Threading.Tasks;

namespace SubcongMeet.Controllers
{
    public class RankingsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public RankingsController(ApplicationDbContext context)
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

            var teams = await _context.Teams.ToDictionaryAsync(t => t.Id, t => t.Name);

            var totalEvents = await _context.Events.CountAsync();

            ViewBag.RecentResults = recentResults;
            ViewBag.Teams = teams;
            ViewBag.TotalEvents = totalEvents;

            return View(tallies);
        }
    }
}