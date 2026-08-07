using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using SubcongMeet.Data;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SubcongMeet.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login()
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");

            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string username, string password)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ViewBag.Error = "Please fill in all fields.";
                return View();
            }

            var coordinator = await _context.Coordinators
                .FirstOrDefaultAsync(c => c.Username.ToLower() == username.ToLower());

            if (coordinator == null || coordinator.Password != password)
            {
                ViewBag.Error = "Invalid username or password.";
                return View();
            }

            // Check if the user needs to change their password
            if (coordinator.NeedsPasswordChange)
            {
                TempData["Username"] = coordinator.Username; // Pass securely to the change view
                return RedirectToAction("ChangePassword");
            }

            string assignedRole = coordinator.IsAdmin ? "Admin" : "Coordinator";

            // FIX: Added "CoordinatorId" claim here!
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, coordinator.Username),
                new Claim(ClaimTypes.NameIdentifier, coordinator.Username),
                new Claim("FullName", coordinator.FullName),
                new Claim("CoordinatorId", coordinator.Id.ToString()), // <--- This was missing
                new Claim(ClaimTypes.Role, assignedRole)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ChangePassword()
        {
            var username = TempData["Username"]?.ToString();
            if (string.IsNullOrEmpty(username))
            {
                return RedirectToAction("Login");
            }
            
            TempData.Keep("Username"); 
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string newPassword, string confirmPassword)
        {
            var username = TempData["Username"]?.ToString();
            if (string.IsNullOrEmpty(username)) 
                return RedirectToAction("Login");

            if (string.IsNullOrEmpty(newPassword) || string.IsNullOrEmpty(confirmPassword))
            {
                ViewBag.Error = "All fields are required.";
                TempData.Keep("Username");
                return View();
            }

            if (newPassword != confirmPassword)
            {
                ViewBag.Error = "Passwords do not match.";
                TempData.Keep("Username");
                return View();
            }

            if (newPassword == "coordinator")
            {
                ViewBag.Error = "Your new password cannot be the default password.";
                TempData.Keep("Username");
                return View();
            }

            var coordinator = await _context.Coordinators.FirstOrDefaultAsync(c => c.Username.ToLower() == username.ToLower());
            if (coordinator == null) 
                return RedirectToAction("Login");

            coordinator.Password = newPassword;
            
            _context.Update(coordinator);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Password successfully changed. Please log in with your new credentials.";
            return RedirectToAction("Login");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}