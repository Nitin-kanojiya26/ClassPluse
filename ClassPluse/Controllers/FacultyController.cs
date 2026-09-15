using ClassPluse.Data;
using ClassPluse.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClassPluse.Controllers
{
    [Authorize(Roles = "Faculty")]
    public class FacultyController : Controller
    {
        private readonly AttendanceDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public FacultyController(AttendanceDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var courses = await _context.Courses
                .Where(c => c.FacultyId == user.Id)
                .ToListAsync();

            var courseStats = new List<dynamic>();
            foreach (var course in courses)
            {
                var activeSession = await _context.LectureSessions
                    .FirstOrDefaultAsync(ls => ls.CourseId == course.Id && ls.IsActive);
                
                var totalEnrolled = await _context.Enrollments
                    .CountAsync(e => e.CourseId == course.Id);

                var lastSession = await _context.LectureSessions
                    .Where(ls => ls.CourseId == course.Id && !ls.IsActive)
                    .OrderByDescending(ls => ls.StartTime)
                    .FirstOrDefaultAsync();

                var totalSessions = await _context.LectureSessions
                    .CountAsync(ls => ls.CourseId == course.Id && ls.EndTime != null);

                var attendedCount = await _context.AttendanceRecords
                    .CountAsync(a => a.LectureSession!.CourseId == course.Id && a.Status == AttendanceStatus.Present);
                
                var possibleAttendance = totalEnrolled * totalSessions;
                double avgAttendance = possibleAttendance > 0 ? ((double)attendedCount / possibleAttendance) * 100 : 0;

                courseStats.Add(new {
                    Course = course,
                    HasActiveSession = activeSession != null,
                    ActiveSessionId = activeSession?.Id,
                    TotalEnrolled = totalEnrolled,
                    LastSessionDate = lastSession?.StartTime,
                    AvgAttendance = Math.Round(avgAttendance, 1)
                });
            }

            var recentSessions = await _context.LectureSessions
                .Include(ls => ls.Course)
                .Where(ls => ls.FacultyId == user.Id && !ls.IsActive)
                .OrderByDescending(ls => ls.StartTime)
                .Take(5)
                .Select(ls => new {
                    Session = ls,
                    TotalStudents = _context.Enrollments.Count(e => e.CourseId == ls.CourseId),
                    PresentCount = _context.AttendanceRecords.Count(a => a.LectureSessionId == ls.Id && a.Status == AttendanceStatus.Present)
                })
                .ToListAsync();

            ViewBag.CourseStats = courseStats;
            ViewBag.RecentSessions = recentSessions;
            ViewBag.UserFullName = user.FullName;

            return View();
        }

        public async Task<IActionResult> Sessions(int courseId)
        {
            var user = await _userManager.GetUserAsync(User);
            var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == courseId && c.FacultyId == user!.Id);
            if (course == null) return NotFound();

            var sessions = await _context.LectureSessions
                .Where(ls => ls.CourseId == courseId)
                .OrderByDescending(ls => ls.StartTime)
                .ToListAsync();

            ViewBag.CourseName = course.CourseName;
            ViewBag.CourseId = courseId;
            return View(sessions);
        }

        [HttpPost]
        public async Task<IActionResult> StartSession(int courseId)
        {
            var user = await _userManager.GetUserAsync(User);
            var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == courseId && c.FacultyId == user!.Id);
            if (course == null) return NotFound();

            var session = new LectureSession
            {
                CourseId = courseId,
                FacultyId = user!.Id,
                StartTime = DateTime.UtcNow,
                IsActive = true
            };

            _context.LectureSessions.Add(session);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(LiveSession), new { sessionId = session.Id });
        }

        [HttpPost]
        public async Task<IActionResult> EndSession(int sessionId)
        {
            var user = await _userManager.GetUserAsync(User);
            var session = await _context.LectureSessions.FirstOrDefaultAsync(ls => ls.Id == sessionId && ls.FacultyId == user!.Id);
            
            if (session == null) return NotFound();

            session.IsActive = false;
            session.EndTime = DateTime.UtcNow;
            
            // Invalidate existing tokens
            var activeTokens = await _context.QrTokens
                .Where(t => t.LectureSessionId == sessionId && t.ExpiresAt > DateTime.UtcNow)
                .ToListAsync();
                
            foreach(var token in activeTokens)
            {
                token.ExpiresAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Sessions), new { courseId = session.CourseId });
        }

        public async Task<IActionResult> LiveSession(int sessionId)
        {
            var user = await _userManager.GetUserAsync(User);
            var session = await _context.LectureSessions
                .Include(ls => ls.Course)
                .FirstOrDefaultAsync(ls => ls.Id == sessionId && ls.FacultyId == user!.Id);

            if (session == null || !session.IsActive) return NotFound();

            var totalEnrolled = await _context.Enrollments.CountAsync(e => e.CourseId == session.CourseId);
            ViewBag.TotalEnrolled = totalEnrolled;

            return View(session);
        }

        [HttpGet]
        public async Task<IActionResult> AdjustAttendance(int sessionId)
        {
            var user = await _userManager.GetUserAsync(User);
            var session = await _context.LectureSessions
                .Include(ls => ls.Course)
                .FirstOrDefaultAsync(ls => ls.Id == sessionId && ls.FacultyId == user!.Id);

            if (session == null) return NotFound();

            // Get enrolled students
            var enrolledStudents = await _context.Enrollments
                .Where(e => e.CourseId == session.CourseId)
                .Include(e => e.Student)
                .Select(e => e.Student)
                .ToListAsync();

            var attendanceRecords = await _context.AttendanceRecords
                .Where(a => a.LectureSessionId == sessionId)
                .ToListAsync();

            ViewBag.SessionId = sessionId;
            ViewBag.CourseName = session.Course!.CourseName;
            ViewBag.AttendanceRecords = attendanceRecords;

            return View(enrolledStudents);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateAttendance(int sessionId, string studentId, AttendanceStatus status)
        {
            var user = await _userManager.GetUserAsync(User);
            var session = await _context.LectureSessions.FirstOrDefaultAsync(ls => ls.Id == sessionId && ls.FacultyId == user!.Id);
            if (session == null) return Unauthorized();

            var record = await _context.AttendanceRecords
                .FirstOrDefaultAsync(a => a.LectureSessionId == sessionId && a.StudentId == studentId);

            if (record == null)
            {
                record = new AttendanceRecord
                {
                    LectureSessionId = sessionId,
                    StudentId = studentId,
                    Status = status,
                    MarkedAt = DateTime.UtcNow,
                    MarkedBy = MarkedByType.ManualFaculty
                };
                _context.AttendanceRecords.Add(record);
            }
            else
            {
                record.Status = status;
                record.MarkedBy = MarkedByType.ManualFaculty;
                record.MarkedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(AdjustAttendance), new { sessionId = sessionId });
        }

        public async Task<IActionResult> ClassMatrix(int courseId)
        {
            var user = await _userManager.GetUserAsync(User);
            var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == courseId && c.FacultyId == user!.Id);
            if (course == null) return NotFound();

            var enrolledStudents = await _context.Enrollments
                .Where(e => e.CourseId == courseId)
                .Include(e => e.Student)
                .Select(e => e.Student)
                .ToListAsync();

            var sessions = await _context.LectureSessions
                .Where(ls => ls.CourseId == courseId)
                .OrderBy(ls => ls.StartTime)
                .ToListAsync();

            var attendanceRecords = await _context.AttendanceRecords
                .Where(a => a.LectureSession!.CourseId == courseId)
                .ToListAsync();

            ViewBag.CourseName = course.CourseName;
            ViewBag.CourseId = courseId;
            ViewBag.Sessions = sessions;
            ViewBag.AttendanceRecords = attendanceRecords;

            return View(enrolledStudents);
        }

        public async Task<IActionResult> MyCourses()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var courses = await _context.Courses
                .Where(c => c.FacultyId == user.Id)
                .ToListAsync();

            var courseStats = new List<dynamic>();
            foreach (var course in courses)
            {
                var activeSession = await _context.LectureSessions
                    .FirstOrDefaultAsync(ls => ls.CourseId == course.Id && ls.IsActive);
                
                var totalEnrolled = await _context.Enrollments
                    .CountAsync(e => e.CourseId == course.Id);

                var lastSession = await _context.LectureSessions
                    .Where(ls => ls.CourseId == course.Id && !ls.IsActive)
                    .OrderByDescending(ls => ls.StartTime)
                    .FirstOrDefaultAsync();

                var allSessionsIds = await _context.LectureSessions
                    .Where(ls => ls.CourseId == course.Id && !ls.IsActive)
                    .Select(ls => ls.Id)
                    .ToListAsync();

                var totalSessions = await _context.LectureSessions
                    .CountAsync(ls => ls.CourseId == course.Id && ls.EndTime != null);

                double avgAttendance = 0;
                if (allSessionsIds.Any() && totalEnrolled > 0)
                {
                    var attendedCount = await _context.AttendanceRecords
                        .CountAsync(a => allSessionsIds.Contains(a.LectureSessionId) && a.Status == AttendanceStatus.Present);
                    
                    var possibleAttendance = totalEnrolled * allSessionsIds.Count;
                    avgAttendance = ((double)attendedCount / possibleAttendance) * 100;
                }

                courseStats.Add(new {
                    Course = course,
                    HasActiveSession = activeSession != null,
                    ActiveSessionId = activeSession?.Id,
                    TotalEnrolled = totalEnrolled,
                    LastSessionDate = lastSession?.StartTime,
                    AvgAttendance = Math.Round(avgAttendance, 1),
                    TotalSessions = totalSessions
                });
            }

            return View(courseStats);
        }

        public async Task<IActionResult> CourseDashboard(int courseId)
        {
            var user = await _userManager.GetUserAsync(User);
            var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == courseId && c.FacultyId == user!.Id);
            if (course == null) return NotFound();

            var activeSession = await _context.LectureSessions
                .FirstOrDefaultAsync(ls => ls.CourseId == course.Id && ls.IsActive);
            
            var totalEnrolled = await _context.Enrollments
                .CountAsync(e => e.CourseId == course.Id);

            var totalSessions = await _context.LectureSessions
                .CountAsync(ls => ls.CourseId == course.Id && ls.EndTime != null);

            var attendedCount = await _context.AttendanceRecords
                .CountAsync(a => a.LectureSession!.CourseId == course.Id && a.Status == AttendanceStatus.Present);
            
            var possibleAttendance = totalEnrolled * totalSessions;
            double avgAttendance = possibleAttendance > 0 ? ((double)attendedCount / possibleAttendance) * 100 : 0;

            var recentSessions = await _context.LectureSessions
                .Where(ls => ls.CourseId == courseId && !ls.IsActive)
                .OrderByDescending(ls => ls.StartTime)
                .Take(5)
                .Select(ls => new {
                    Session = ls,
                    TotalStudents = _context.Enrollments.Count(e => e.CourseId == ls.CourseId),
                    PresentCount = _context.AttendanceRecords.Count(a => a.LectureSessionId == ls.Id && a.Status == AttendanceStatus.Present)
                })
                .ToListAsync();

            ViewBag.HasActiveSession = activeSession != null;
            ViewBag.ActiveSessionId = activeSession?.Id;
            ViewBag.TotalEnrolled = totalEnrolled;
            ViewBag.TotalSessions = totalSessions;
            ViewBag.AvgAttendance = Math.Round(avgAttendance, 1);
            ViewBag.RecentSessions = recentSessions;

            return View(course);
        }

        [HttpGet]
        public async Task<IActionResult> ExportReport(int courseId)
        {
            var user = await _userManager.GetUserAsync(User);
            var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == courseId && c.FacultyId == user!.Id);
            if (course == null) return NotFound();

            var enrolledStudents = await _context.Enrollments
                .Where(e => e.CourseId == courseId)
                .Include(e => e.Student)
                .Select(e => e.Student)
                .OrderBy(s => s!.FullName)
                .ToListAsync();

            var sessions = await _context.LectureSessions
                .Where(ls => ls.CourseId == courseId)
                .OrderBy(ls => ls.StartTime)
                .ToListAsync();

            var attendanceRecords = await _context.AttendanceRecords
                .Where(a => a.LectureSession!.CourseId == courseId)
                .ToListAsync();

            using var workbook = new ClosedXML.Excel.XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Attendance Matrix");

            // Header Row
            worksheet.Cell(1, 1).Value = "Student Name";
            worksheet.Cell(1, 2).Value = "Id No";
            worksheet.Cell(1, 3).Value = "Attendance %";

            for (int i = 0; i < sessions.Count; i++)
            {
                worksheet.Cell(1, 4 + i).Value = sessions[i].StartTime.ToLocalTime().ToString("MM/dd");
            }

            // Data Rows
            for (int r = 0; r < enrolledStudents.Count; r++)
            {
                var student = enrolledStudents[r];
                worksheet.Cell(r + 2, 1).Value = student!.FullName;
                worksheet.Cell(r + 2, 2).Value = student.RollNumber ?? "";

                int attendedCount = 0;
                for (int c = 0; c < sessions.Count; c++)
                {
                    var record = attendanceRecords.FirstOrDefault(a => a.StudentId == student.Id && a.LectureSessionId == sessions[c].Id);
                    string statusStr = "Absent";
                    if (record != null)
                    {
                        statusStr = record.Status.ToString();
                        if (record.Status == AttendanceStatus.Present) attendedCount++;
                    }
                    else
                    {
                        statusStr = "Unmarked";
                    }
                    worksheet.Cell(r + 2, 4 + c).Value = statusStr;
                }

                double percentage = sessions.Count > 0 ? ((double)attendedCount / sessions.Count) * 100 : 0;
                worksheet.Cell(r + 2, 3).Value = Math.Round(percentage, 1) + "%";
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var content = stream.ToArray();

            return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{course.CourseCode}_Attendance.xlsx");
        }
    }
}
