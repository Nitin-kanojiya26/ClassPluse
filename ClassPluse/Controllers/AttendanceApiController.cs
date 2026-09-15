using ClassPluse.Models;
using ClassPluse.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClassPluse.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AttendanceApiController : ControllerBase
    {
        private readonly AttendanceDbContext _context;
        private readonly CryptoService _cryptoService;
        private readonly UserManager<ApplicationUser> _userManager;

        public AttendanceApiController(AttendanceDbContext context, CryptoService cryptoService, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _cryptoService = cryptoService;
            _userManager = userManager;
        }

        [HttpPost("mark")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> MarkAttendance([FromBody] MarkAttendanceRequest request)
        {
            if (string.IsNullOrEmpty(request.Token))
            {
                return BadRequest(new { success = false, message = "Token is required." });
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized(new { success = false, message = "User not found." });

            try
            {
                // 1. Decrypt token
                var decryptedPayload = _cryptoService.Decrypt(request.Token);
                var parts = decryptedPayload.Split('|');
                if (parts.Length != 3)
                {
                    return BadRequest(new { success = false, message = "Invalid token format." });
                }

                var sessionIdStr = parts[0];
                var timestampStr = parts[1];
                var providedHmac = parts[2];

                // 2. Verify HMAC
                var basePayload = $"{sessionIdStr}|{timestampStr}";
                var expectedHmac = _cryptoService.GenerateHmac(basePayload);
                if (providedHmac != expectedHmac)
                {
                    return BadRequest(new { success = false, message = "Token signature is invalid." });
                }

                if (!int.TryParse(sessionIdStr, out int sessionId))
                {
                    return BadRequest(new { success = false, message = "Invalid session ID." });
                }

                // 3. Verify Session and Token Expiry
                var session = await _context.LectureSessions.FindAsync(sessionId);
                if (session == null || !session.IsActive)
                {
                    return BadRequest(new { success = false, message = "Session is not active." });
                }

                var dbToken = await _context.QrTokens
                    .FirstOrDefaultAsync(t => t.LectureSessionId == sessionId && t.EncryptedPayload == request.Token);

                if (dbToken == null)
                {
                    return BadRequest(new { success = false, message = "Token not recognized." });
                }

                if (DateTime.UtcNow > dbToken.ExpiresAt)
                {
                    return BadRequest(new { success = false, message = "QR code has expired. Please scan the current one." });
                }

                // 4. Verify Enrollment
                var isEnrolled = await _context.Enrollments
                    .AnyAsync(e => e.CourseId == session.CourseId && e.StudentId == user.Id);
                
                if (!isEnrolled)
                {
                    return BadRequest(new { success = false, message = "You are not enrolled in this course." });
                }

                // 5. Check Duplicate Attendance
                var existingRecord = await _context.AttendanceRecords
                    .FirstOrDefaultAsync(a => a.LectureSessionId == sessionId && a.StudentId == user.Id);

                if (existingRecord != null)
                {
                    if (existingRecord.Status == AttendanceStatus.Present)
                    {
                        return BadRequest(new { success = false, message = "You have already marked attendance for this session." });
                    }
                    else
                    {
                        // Update if it was marked absent/excused
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

                return Ok(new { success = true, message = "Attendance marked successfully!" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = "An error occurred while processing the QR code." });
            }
        }
    }
}
