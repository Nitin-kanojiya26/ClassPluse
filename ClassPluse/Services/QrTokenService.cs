using ClassPluse.Data;
using ClassPluse.Models;
using QRCoder;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ClassPluse.Services
{
    public class QrTokenService
    {
        private readonly CryptoService _cryptoService;
        private readonly AttendanceDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        
        // OPTION: Set this to true to stop QR code from refreshing (reuses same token for testing on localhost)
        private readonly bool _reuseTokenForLocalTesting = true;

        public QrTokenService(CryptoService cryptoService, AttendanceDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _cryptoService = cryptoService;
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<string> GenerateQrCodeForSessionAsync(int sessionId)
        {
            var session = await _context.LectureSessions.FindAsync(sessionId);
            if (session == null || !session.IsActive)
            {
                throw new InvalidOperationException("Session not found or not active.");
            }

            string encryptedPayload = "";

            if (_reuseTokenForLocalTesting)
            {
                var existingToken = await _context.QrTokens
                    .Where(t => t.LectureSessionId == sessionId && t.ExpiresAt > DateTime.UtcNow)
                    .OrderByDescending(t => t.GeneratedAt)
                    .FirstOrDefaultAsync();

                if (existingToken != null)
                {
                    encryptedPayload = existingToken.EncryptedPayload;
                }
            }

            if (string.IsNullOrEmpty(encryptedPayload))
            {
                // Create token payload: SessionId|UtcTimestamp
                var timestamp = DateTime.UtcNow.Ticks.ToString();
                var basePayload = $"{sessionId}|{timestamp}";
                
                // Add HMAC
                var hmac = _cryptoService.GenerateHmac(basePayload);
                var fullPayload = $"{basePayload}|{hmac}";
                
                // Encrypt
                encryptedPayload = _cryptoService.Encrypt(fullPayload);

                var token = new QrToken
                {
                    LectureSessionId = sessionId,
                    EncryptedPayload = encryptedPayload,
                    GeneratedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddDays(1) // Long expiry for testing
                };

                _context.QrTokens.Add(token);
                await _context.SaveChangesAsync();
            }

            // Generate QR Image (Base64 PNG)
            var request = _httpContextAccessor.HttpContext?.Request;
            var baseUrl = request != null ? $"{request.Scheme}://{request.Host}" : "";
            var qrUrl = $"{baseUrl}/Student/MarkScan?token={Uri.EscapeDataString(encryptedPayload)}";

            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(qrUrl, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrCodeData);
            var qrCodeBytes = qrCode.GetGraphic(20);
            
            return Convert.ToBase64String(qrCodeBytes);
        }
    }
}
