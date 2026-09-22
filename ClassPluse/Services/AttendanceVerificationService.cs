using ClassPluse.Data;
using ClassPluse.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace ClassPluse.Services
{
    public class AttendanceVerificationService
    {
        private readonly AttendanceDbContext _context;
        private readonly CryptoService _cryptoService;
        private readonly FaceRecognitionService _faceRecognitionService;
        private readonly UserManager<ApplicationUser> _userManager;

        public AttendanceVerificationService(
            AttendanceDbContext context,
            CryptoService cryptoService,
            FaceRecognitionService faceRecognitionService,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _cryptoService = cryptoService;
            _faceRecognitionService = faceRecognitionService;
            _userManager = userManager;
        }

        public async Task<ApiResponse> VerifyAndMarkAttendanceAsync(string userId, string userFaceEncoding, string token, string faceImageBase64, string? ipAddress)
        {
            if (string.IsNullOrEmpty(token)) return ApiResponse.Error("Token is required.");
            if (string.IsNullOrEmpty(faceImageBase64)) return ApiResponse.Error("Face image is required for attendance verification.");

            try
            {
                // 1. Verify Face
                var base64Data = faceImageBase64.Contains(",") 
                    ? faceImageBase64.Split(',')[1] 
                    : faceImageBase64;
                    
                byte[] imageBytes = Convert.FromBase64String(base64Data);
                var newEncoding = _faceRecognitionService.GetFaceEncodingFromImage(imageBytes);

                if (string.IsNullOrEmpty(newEncoding))
                {
                    return ApiResponse.Error("No face detected in the camera frame. Try again.");
                }

                double faceDistance = _faceRecognitionService.GetFaceDistance(userFaceEncoding, newEncoding);

                if (faceDistance > 0.6)
                {
                    return ApiResponse.Error("Face verification failed. It does not match your registered profile.");
                }

                // CONTINUOUS LEARNING: If it's a near-perfect match, silently update their profile
                if (faceDistance < 0.35)
                {
                    var user = await _userManager.FindByIdAsync(userId);
                    if (user != null)
                    {
                        user.FaceEncoding = newEncoding;
                        await _userManager.UpdateAsync(user);
                    }
                }

                // 2. Decrypt token
                var decryptedPayload = _cryptoService.Decrypt(token);
                var parts = decryptedPayload.Split('|');
                if (parts.Length != 3)
                {
                    return ApiResponse.Error("Invalid token format.");
                }

                var sessionIdStr = parts[0];
                var timestampStr = parts[1];
                var providedHmac = parts[2];

                // 3. Verify HMAC
                var basePayload = $"{sessionIdStr}|{timestampStr}";
                var expectedHmac = _cryptoService.GenerateHmac(basePayload);
                if (providedHmac != expectedHmac)
                {
                    return ApiResponse.Error("Token signature is invalid.");
                }

                if (!int.TryParse(sessionIdStr, out int sessionId))
                {
                    return ApiResponse.Error("Invalid session ID.");
                }

                // 4. Verify Session and Token Expiry
                var session = await _context.LectureSessions.FindAsync(sessionId);
                if (session == null || !session.IsActive)
                {
                    return ApiResponse.Error("Session is not active.");
                }

                var dbToken = await _context.QrTokens
                    .FirstOrDefaultAsync(t => t.LectureSessionId == sessionId && t.EncryptedPayload == token);

                if (dbToken == null)
                {
                    return ApiResponse.Error("Token not recognized.");
                }

                if (DateTime.UtcNow > dbToken.ExpiresAt)
                {
                    return ApiResponse.Error("QR code has expired. Please scan the current one.");
                }

                // 5. Verify Enrollment
                var isEnrolled = await _context.Enrollments
                    .AnyAsync(e => e.CourseId == session.CourseId && e.StudentId == userId);
                
                if (!isEnrolled)
                {
                    return ApiResponse.Error("You are not enrolled in this course.");
                }

                // 6. Check Duplicate Attendance
                var existingRecord = await _context.AttendanceRecords
                    .FirstOrDefaultAsync(a => a.LectureSessionId == sessionId && a.StudentId == userId);

                if (existingRecord != null)
                {
                    if (existingRecord.Status == AttendanceStatus.Present)
                    {
                        return ApiResponse.Error("You have already marked attendance for this session.");
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
                        StudentId = userId,
                        Status = AttendanceStatus.Present,
                        MarkedAt = DateTime.UtcNow,
                        MarkedBy = MarkedByType.Scan,
                        IPAddress = ipAddress
                    };
                    _context.AttendanceRecords.Add(record);
                }

                await _context.SaveChangesAsync();
                return ApiResponse.Ok("Attendance marked successfully!");
            }
            catch (Exception ex)
            {
                return ApiResponse.Error($"An error occurred while processing the QR code. {ex.Message}");
            }
        }
    }
}
