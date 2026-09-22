using ClassPluse.Models;
using ClassPluse.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ClassPluse.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IEmailService _emailService;

        public AccountController(SignInManager<ApplicationUser> signInManager, IEmailService emailService)
        {
            _signInManager = signInManager;
            _emailService = emailService;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (ModelState.IsValid)
            {
                var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);
                if (result.Succeeded)
                {
                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) && returnUrl != "/")
                    {
                        return Redirect(returnUrl);
                    }
                    
                    var user = await _signInManager.UserManager.FindByEmailAsync(model.Email);
                    if (user != null)
                    {
                        var roles = await _signInManager.UserManager.GetRolesAsync(user);
                        if (roles.Contains("Admin"))
                            return RedirectToAction("Index", "Admin");
                        else if (roles.Contains("Faculty"))
                            return RedirectToAction("Index", "Faculty");
                        else if (roles.Contains("Student"))
                        {
                            if (!user.HasRegisteredFace)
                                return RedirectToAction("RegisterFace", "Student");
                            return RedirectToAction("Dashboard", "Student");
                        }
                    }
                    
                    return RedirectToAction(nameof(HomeController.Index), "Home");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                    return View(model);
                }
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }
        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _signInManager.UserManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction(nameof(Login));
            }

            var changePasswordResult = await _signInManager.UserManager.ChangePasswordAsync(user, model.OldPassword, model.NewPassword);
            if (!changePasswordResult.Succeeded)
            {
                foreach (var error in changePasswordResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return View(model);
            }

            await _signInManager.RefreshSignInAsync(user);
            TempData["StatusMessage"] = "Your password has been changed successfully.";
            return RedirectToAction(nameof(ChangePassword));
        }

        private string GenerateRandomPassword()
        {
            var length = 12;
            const string validChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890!@#$%^&*";
            var res = new System.Text.StringBuilder();
            var rnd = new Random();
            while (res.Length < length)
            {
                res.Append(validChars[rnd.Next(validChars.Length)]);
            }
            res.Append("aA1!"); // Ensure it meets identity requirements
            return res.ToString();
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _signInManager.UserManager.FindByEmailAsync(model.Email);
                if (user == null)
                {
                    user = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(_signInManager.UserManager.Users, u => u.PersonalEmail == model.Email);
                }
                
                if (user != null)
                {
                    var newPassword = GenerateRandomPassword();
                    var token = await _signInManager.UserManager.GeneratePasswordResetTokenAsync(user);
                    var result = await _signInManager.UserManager.ResetPasswordAsync(user, token, newPassword);

                    if (result.Succeeded)
                    {
                        var subject = "Your New Password";
                        var body = $"Your password has been reset. Your new password is: <strong>{newPassword}</strong><br/>Please login and change it immediately.";
                        
                        var targetEmail = !string.IsNullOrEmpty(user.PersonalEmail) ? user.PersonalEmail : user.Email;
                        await _emailService.SendEmailAsync(targetEmail, subject, body);
                    }
                }
                
                return RedirectToAction(nameof(ForgotPasswordConfirmation));
            }
            return View(model);
        }

        [HttpGet]
        public IActionResult ForgotPasswordConfirmation()
        {
            return View();
        }
    }
}
