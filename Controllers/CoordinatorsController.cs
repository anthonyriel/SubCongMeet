using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SubcongMeet.Data;
using SubcongMeet.Models;
using System.Threading.Tasks;
using System.Linq;
using System;

namespace SubcongMeet.Controllers
{
    [Authorize(Roles = "Admin")]
    public class CoordinatorsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CoordinatorsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var coordinators = await _context.Coordinators
                .OrderBy(c => c.FullName)
                .ToListAsync();
            return View(coordinators);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Username,Password,FullName,IsAdmin")] Coordinator coordinator)
        {
            if (ModelState.IsValid)
            {
                var exists = await _context.Coordinators
                    .AnyAsync(c => c.Username.ToLower() == coordinator.Username.ToLower());
                
                if (exists)
                {
                    ModelState.AddModelError("Username", "This username is already taken.");
                    return View(coordinator);
                }

                // Force the default password for new users
                coordinator.Password = "coordinator";

                _context.Add(coordinator);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(coordinator);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var coordinator = await _context.Coordinators.FindAsync(id);
            if (coordinator == null) return NotFound();

            return View(coordinator);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Username,Password,FullName,IsAdmin")] Coordinator coordinator)
        {
            if (id != coordinator.Id) return NotFound();

            if (ModelState.IsValid)
            {
                var usernameExists = await _context.Coordinators
                    .AnyAsync(c => c.Id != coordinator.Id && c.Username.ToLower() == coordinator.Username.ToLower());
                
                if (usernameExists)
                {
                    ModelState.AddModelError("Username", "This username is already taken by another user.");
                    return View(coordinator);
                }

                try
                {
                    _context.Update(coordinator);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CoordinatorExists(coordinator.Id)) return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(coordinator);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var coordinator = await _context.Coordinators.FindAsync(id);
            if (coordinator != null)
            {
                _context.Coordinators.Remove(coordinator);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private bool CoordinatorExists(int id)
        {
            return _context.Coordinators.Any(e => e.Id == id);
        }
    }
}