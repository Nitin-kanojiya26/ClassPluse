using ClassPluse.Data;
using ClassPluse.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClassPluse.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AttendanceDbContext _context;
        private readonly ClassPluse.Services.IEmailService _emailService;

        public AdminController(UserManager<ApplicationUser> userManager, AttendanceDbContext context, ClassPluse.Services.IEmailService emailService)
        {
            _userManager = userManager;
            _context = context;
            _emailService = emailService;
        }

        public async Task<IActionResult> Index()
        {
            var studentsCount = (await _userManager.GetUsersInRoleAsync("Student")).Count;
            var facultyCount = (await _userManager.GetUsersInRoleAsync("Faculty")).Count;
            var coursesCount = await _context.Courses.CountAsync();
            
            var today = DateTime.UtcNow.Date;
            var sessionsToday = await _context.LectureSessions
                .CountAsync(ls => ls.StartTime >= today && ls.StartTime < today.AddDays(1));
            
            var courses = await _context.Courses.Include(c => c.Enrollments).ToListAsync();
            var enrollmentsLabels = courses.Select(c => c.CourseCode).ToArray();
            var enrollmentsData = courses.Select(c => c.Enrollments.Count).ToArray();

            ViewBag.StudentsCount = studentsCount;
            ViewBag.FacultyCount = facultyCount;
            ViewBag.CoursesCount = coursesCount;
            ViewBag.SessionsToday = sessionsToday;
            ViewBag.EnrollmentsLabels = enrollmentsLabels;
            ViewBag.EnrollmentsData = enrollmentsData;
            
            return View();
        }

        // Users Management
        public async Task<IActionResult> Users()
        {
            var users = await _userManager.Users.ToListAsync();
            return View(users);
        }

        // Courses Management
        public async Task<IActionResult> Courses()
        {
            var courses = await _context.Courses.Include(c => c.Faculty).ToListAsync();
            return View(courses);
        }

        [HttpGet]
        public async Task<IActionResult> CreateCourse()
        {
            var facultyUsers = await _userManager.GetUsersInRoleAsync("Faculty");
            ViewBag.FacultyList = facultyUsers;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreateCourse(Course course)
        {
            if (ModelState.IsValid)
            {
                _context.Courses.Add(course);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Courses));
            }
            var facultyUsers = await _userManager.GetUsersInRoleAsync("Faculty");
            ViewBag.FacultyList = facultyUsers;
            return View(course);
        }
        
        // Enrollments
        public async Task<IActionResult> Enrollments()
        {
            var enrollments = await _context.Enrollments
                .Include(e => e.Course)
                .Include(e => e.Student)
                .ToListAsync();
            return View(enrollments);
        }

        [HttpGet]
        public async Task<IActionResult> CreateEnrollment()
        {
            ViewBag.Courses = await _context.Courses.ToListAsync();
            ViewBag.Students = await _userManager.GetUsersInRoleAsync("Student");
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreateEnrollment(int courseId, string studentId)
        {
            var existing = await _context.Enrollments.FirstOrDefaultAsync(e => e.CourseId == courseId && e.StudentId == studentId);
            if (existing == null)
            {
                _context.Enrollments.Add(new Enrollment { CourseId = courseId, StudentId = studentId });
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Enrollments));
        }

        [HttpGet]
        public IActionResult CreateUser()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreateUser(string email, string personalEmail, string fullName, string role, string? rollNumber, string? department)
        {
            // Auto-generate secure password
            string generatedPassword = $"Cp@{Guid.NewGuid().ToString().Substring(0, 6).ToUpper()}!";

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                PersonalEmail = personalEmail,
                FullName = fullName,
                RollNumber = role == "Student" ? rollNumber : null,
                Department = department,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, generatedPassword);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, role);

                // Send email with credentials
                string subject = "Welcome to ClassPluse - Your Login Credentials";
                string body = $"Hello {fullName},\n\nYour account has been created.\n\nLogin Email: {email}\nPassword: {generatedPassword}\n\nPlease log in and change your password as soon as possible.";
                
                if (!string.IsNullOrEmpty(personalEmail))
                {
                    await _emailService.SendEmailAsync(personalEmail, subject, body);
                }

                return RedirectToAction(nameof(Users));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Unenroll(int id)
        {
            var enrollment = await _context.Enrollments.FindAsync(id);
            if (enrollment != null)
            {
                _context.Enrollments.Remove(enrollment);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Enrollments));
        }

        [HttpGet]
        public async Task<IActionResult> EditCourse(int id)
        {
            var course = await _context.Courses.FindAsync(id);
            if (course == null) return NotFound();
            
            var facultyUsers = await _userManager.GetUsersInRoleAsync("Faculty");
            ViewBag.FacultyList = facultyUsers;
            return View(course);
        }

        [HttpPost]
        public async Task<IActionResult> EditCourse(int id, Course updatedCourse)
        {
            if (id != updatedCourse.Id) return BadRequest();
            
            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(updatedCourse);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CourseExists(updatedCourse.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Courses));
            }
            var facultyUsers = await _userManager.GetUsersInRoleAsync("Faculty");
            ViewBag.FacultyList = facultyUsers;
            return View(updatedCourse);
        }

        private bool CourseExists(int id)
        {
            return _context.Courses.Any(e => e.Id == id);
        }

        [HttpGet]
        public async Task<IActionResult> EditUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();
            return View(user);
        }

        [HttpPost]
        public async Task<IActionResult> EditUser(string id, ApplicationUser updatedUser)
        {
            if (id != updatedUser.Id) return BadRequest();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            user.FullName = updatedUser.FullName;
            user.Department = updatedUser.Department;
            user.RollNumber = updatedUser.RollNumber;
            // Not updating email/password here for simplicity

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                return RedirectToAction(nameof(Users));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(updatedUser);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                // Note: Consider deleting related records (like Enrollments) if foreign keys do not cascade delete
                await _userManager.DeleteAsync(user);
            }
            return RedirectToAction(nameof(Users));
        }
    }
}
