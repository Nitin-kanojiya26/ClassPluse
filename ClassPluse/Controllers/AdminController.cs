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

        public AdminController(UserManager<ApplicationUser> userManager, AttendanceDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var studentsCount = (await _userManager.GetUsersInRoleAsync("Student")).Count;
            var facultyCount = (await _userManager.GetUsersInRoleAsync("Faculty")).Count;
            var coursesCount = await _context.Courses.CountAsync();
            
            var today = DateTime.UtcNow.Date;
            var sessionsToday = await _context.LectureSessions
                .CountAsync(ls => ls.StartTime >= today && ls.StartTime < today.AddDays(1));
            
            ViewBag.StudentsCount = studentsCount;
            ViewBag.FacultyCount = facultyCount;
            ViewBag.CoursesCount = coursesCount;
            ViewBag.SessionsToday = sessionsToday;
            
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
        public async Task<IActionResult> CreateUser(string email, string password, string fullName, string role, string? rollNumber, string? department)
        {
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = fullName,
                RollNumber = role == "Student" ? rollNumber : null,
                Department = department,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, role);
                return RedirectToAction(nameof(Users));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View();
        }
    }
}
