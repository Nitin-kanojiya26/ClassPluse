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

        public StudentController(AttendanceDbContext context, UserManager<ApplicationUser> userManager, ClassPluse.Services.CryptoService cryptoService)
        {
            _context = context;
            _userManager = userManager;
            _cryptoService = cryptoService;
        }

        public IActionResult Scan()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> MarkScan(string token)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            if (string.IsNullOrEmpty(token))
            {
                ViewBag.Message = "Token is required.";
                ViewBag.Success = false;
                return View("MarkScanResult");
            }

            try
            {
                var decryptedPayload = _cryptoService.Decrypt(token);
                var parts = decryptedPayload.Split('|');
                if (parts.Length != 3)
                {
                    ViewBag.Message = "Invalid token format.";
                    ViewBag.Success = false;
                    return View("MarkScanResult");
                }

                var sessionIdStr = parts[0];
                var timestampStr = parts[1];
                var providedHmac = parts[2];

                var basePayload = $"{sessionIdStr}|{timestampStr}";
                var expectedHmac = _cryptoService.GenerateHmac(basePayload);
                if (providedHmac != expectedHmac)
                {
                    ViewBag.Message = "Token signature is invalid.";
                    ViewBag.Success = false;
                    return View("MarkScanResult");
                }

                if (!int.TryParse(sessionIdStr, out int sessionId))
                {
                    ViewBag.Message = "Invalid session ID.";
                    ViewBag.Success = false;
                    return View("MarkScanResult");
                }

                var session = await _context.LectureSessions.FindAsync(sessionId);
                if (session == null || !session.IsActive)
                {
                    ViewBag.Message = "Session is not active.";
                    ViewBag.Success = false;
                    return View("MarkScanResult");
                }

                var dbToken = await _context.QrTokens
                    .FirstOrDefaultAsync(t => t.LectureSessionId == sessionId && t.EncryptedPayload == token);

                if (dbToken == null)
                {
                    ViewBag.Message = "Token not recognized.";
                    ViewBag.Success = false;
                    return View("MarkScanResult");
                }

                if (DateTime.UtcNow > dbToken.ExpiresAt)
                {
                    ViewBag.Message = "QR code has expired. Please scan the current one.";
                    ViewBag.Success = false;
                    return View("MarkScanResult");
                }

                var isEnrolled = await _context.Enrollments
                    .AnyAsync(e => e.CourseId == session.CourseId && e.StudentId == user.Id);
                
                if (!isEnrolled)
                {
                    ViewBag.Message = "You are not enrolled in this course.";
                    ViewBag.Success = false;
                    return View("MarkScanResult");
                }

                var existingRecord = await _context.AttendanceRecords
                    .FirstOrDefaultAsync(a => a.LectureSessionId == sessionId && a.StudentId == user.Id);

                if (existingRecord != null)
                {
                    if (existingRecord.Status == AttendanceStatus.Present)
                    {
                        ViewBag.Message = "You have already marked attendance for this session.";
                        ViewBag.Success = true; // It's already marked, so consider it a success
                        return View("MarkScanResult");
                    }
                    else
                    {
                        existingRecord.Status = AttendanceStatus.Present;
                        existingRecord.MarkedBy = MarkedByType.Scan;
                        existingRecord.MarkedAt = DateTime.UtcNow;
                    }
                }
                else
                {
                    var record = new AttendanceRecord
                    {
                        LectureSessionId = sessionId,
                        StudentId = user.Id,
                        Status = AttendanceStatus.Present,
                        MarkedAt = DateTime.UtcNow,
                        MarkedBy = MarkedByType.Scan,
                        IPAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
                    };
                    _context.AttendanceRecords.Add(record);
                }

                await _context.SaveChangesAsync();
                
                ViewBag.Message = "Attendance marked successfully!";
                ViewBag.Success = true;
                return View("MarkScanResult");
            }
            catch (Exception ex)
            {
                ViewBag.Message = "An error occurred while processing the QR code.";
                ViewBag.Success = false;
                return View("MarkScanResult");
            }
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
