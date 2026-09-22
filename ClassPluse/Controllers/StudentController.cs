using ClassPluse.Data;
using ClassPluse.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClassPluse.Controllers
{
    [Authorize(Roles = "Student")]
    public class StudentController : Controller
    {
        private readonly AttendanceDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ClassPluse.Services.CryptoService _cryptoService;
        private readonly ClassPluse.Services.FaceRecognitionService _faceRecognitionService;

        public StudentController(
            AttendanceDbContext context, 
            UserManager<ApplicationUser> userManager, 
            ClassPluse.Services.CryptoService cryptoService,
            ClassPluse.Services.FaceRecognitionService faceRecognitionService)
        {
            _context = context;
            _userManager = userManager;
            _cryptoService = cryptoService;
            _faceRecognitionService = faceRecognitionService;
        }

        public IActionResult Scan(string? prefilledToken)
        {
            ViewBag.PrefilledToken = prefilledToken;
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> RegisterFace()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            if (user.HasRegisteredFace)
            {
                return RedirectToAction("Dashboard");
            }

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> RegisterFace([FromBody] RegisterFaceRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            if (string.IsNullOrEmpty(request.FaceImageBase64))
            {
                return BadRequest(ApiResponse.Error("No image provided."));
            }

            try
            {
                // Remove data:image/jpeg;base64, prefix if present
                var base64Data = request.FaceImageBase64.Contains(",") 
                    ? request.FaceImageBase64.Split(',')[1] 
                    : request.FaceImageBase64;
                    
                byte[] imageBytes = Convert.FromBase64String(base64Data);

                var encoding = _faceRecognitionService.GetFaceEncodingFromImage(imageBytes);

                if (string.IsNullOrEmpty(encoding))
                {
                    return BadRequest(ApiResponse.Error("Could not detect a face. Please ensure your face is clearly visible."));
                }

                user.FaceEncoding = encoding;
                user.HasRegisteredFace = true;
                
                var result = await _userManager.UpdateAsync(user);
                
                if (result.Succeeded)
                {
                    return Ok(ApiResponse.Ok("Face registered successfully!"));
                }
                
                return BadRequest(ApiResponse.Error("Error saving user profile."));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse.Error(ex.Message));
            }
        }

        [HttpGet]
        public IActionResult MarkScan(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return RedirectToAction("Scan");
            }

            // Redirect to the scanner page to enforce Face Verification
            return RedirectToAction("Scan", new { prefilledToken = token });
        }

        public async Task<IActionResult> Dashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            // Find all courses the student is enrolled in
            var enrollments = await _context.Enrollments
                .Include(e => e.Course)
                .ThenInclude(c => c!.Faculty)
                .Where(e => e.StudentId == user.Id)
                .ToListAsync();

            var dashboardData = new List<dynamic>();
            int overallTotalSessions = 0;
            int overallAttendedSessions = 0;

            foreach (var enrollment in enrollments)
            {
                var totalSessions = await _context.LectureSessions
                    .CountAsync(ls => ls.CourseId == enrollment.CourseId && ls.EndTime != null);

                var attendedSessions = await _context.AttendanceRecords
                    .CountAsync(ar => ar.StudentId == user.Id 
                                   && ar.LectureSession!.CourseId == enrollment.CourseId
                                   && ar.Status == AttendanceStatus.Present);

                double percentage = totalSessions > 0 ? ((double)attendedSessions / totalSessions) * 100 : 0;
                
                overallTotalSessions += totalSessions;
                overallAttendedSessions += attendedSessions;

                dashboardData.Add(new
                {
                    Course = enrollment.Course,
                    TotalSessions = totalSessions,
                    AttendedSessions = attendedSessions,
                    Percentage = Math.Round(percentage, 1)
                });
            }

            ViewBag.DashboardData = dashboardData;
            ViewBag.TotalCourses = enrollments.Count;
            ViewBag.TotalClassesConducted = overallTotalSessions;
            ViewBag.TotalClassesAttended = overallAttendedSessions;
            ViewBag.OverallAttendancePercentage = overallTotalSessions > 0 ? Math.Round(((double)overallAttendedSessions / overallTotalSessions) * 100, 1) : 0;
            ViewBag.UserFullName = user.FullName;
            ViewBag.UserRollNumber = user.RollNumber;

            // Fetch recent 5 attendance activities
            var recentActivity = await _context.AttendanceRecords
                .Include(a => a.LectureSession)
                .ThenInclude(ls => ls!.Course)
                .Where(a => a.StudentId == user.Id)
                .OrderByDescending(a => a.MarkedAt)
                .Take(5)
                .ToListAsync();
            
            ViewBag.RecentActivity = recentActivity;

            return View();
        }

        public async Task<IActionResult> History(int courseId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var course = await _context.Courses.FindAsync(courseId);
            if (course == null) return NotFound();

            var history = await _context.AttendanceRecords
                .Include(ar => ar.LectureSession)
                .ThenInclude(ls => ls!.Faculty)
                .Where(ar => ar.StudentId == user.Id && ar.LectureSession!.CourseId == courseId)
                .OrderByDescending(ar => ar.LectureSession!.StartTime)
                .ToListAsync();

            var totalSessions = await _context.LectureSessions
                .CountAsync(ls => ls.CourseId == courseId && ls.EndTime != null);

            var attendedSessions = history.Count(ar => ar.Status == AttendanceStatus.Present);
            double percentage = totalSessions > 0 ? ((double)attendedSessions / totalSessions) * 100 : 0;

            ViewBag.CourseName = course.CourseName;
            ViewBag.TotalSessions = totalSessions;
            ViewBag.AttendedSessions = attendedSessions;
            ViewBag.Percentage = Math.Round(percentage, 1);

            return View(history);
        }
    }
}
