using ClassPluse.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClassPluse.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SessionApiController : ControllerBase
    {
        private readonly QrTokenService _qrTokenService;
        private readonly ClassPluse.Data.AttendanceDbContext _context;

        public SessionApiController(QrTokenService qrTokenService, ClassPluse.Data.AttendanceDbContext context)
        {
            _qrTokenService = qrTokenService;
            _context = context;
        }

        [HttpGet("{id}/qr")]
        [Authorize(Roles = "Faculty")]
        public async Task<IActionResult> GetFreshQr(int id)
        {
            try
            {
                var base64Qr = await _qrTokenService.GenerateQrCodeForSessionAsync(id);
                return Ok(new { qrImageBase64 = base64Qr });
            }
            catch (InvalidOperationException)
            {
                return BadRequest(new { message = "Session is inactive or invalid." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error generating QR code." });
            }
        }

        [HttpGet("{id}/stats")]
        [Authorize(Roles = "Faculty")]
        public async Task<IActionResult> GetLiveStats(int id)
        {
            var presentCount = await _context.AttendanceRecords
                .CountAsync(a => a.LectureSessionId == id && a.Status == ClassPluse.Models.AttendanceStatus.Present);
                
            return Ok(new { presentCount = presentCount });
        }
    }
}
