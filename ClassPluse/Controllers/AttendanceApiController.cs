using ClassPluse.Data;
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
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AttendanceVerificationService _verificationService;

        public AttendanceApiController(
            UserManager<ApplicationUser> userManager,
            AttendanceVerificationService verificationService)
        {
            _userManager = userManager;
            _verificationService = verificationService;
        }

        [HttpPost("mark")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> MarkAttendance([FromBody] MarkAttendanceRequest request)
        {
            if (string.IsNullOrEmpty(request.Token))
            {
                return BadRequest(ApiResponse.Error("Token is required."));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) 
                return Unauthorized(ApiResponse.Error("User not found."));

            if (!user.HasRegisteredFace || string.IsNullOrEmpty(user.FaceEncoding))
            {
                return BadRequest(ApiResponse.Error("You must register your face before you can mark attendance."));
            }

            if (string.IsNullOrEmpty(request.FaceImageBase64))
            {
                return BadRequest(ApiResponse.Error("Face image is required for attendance verification."));
            }

            var result = await _verificationService.VerifyAndMarkAttendanceAsync(
                user.Id, 
                user.FaceEncoding, 
                request.Token, 
                request.FaceImageBase64, 
                HttpContext.Connection.RemoteIpAddress?.ToString());

            if (result.Success)
            {
                return Ok(result);
            }
            return BadRequest(result);
        }
    }
}
