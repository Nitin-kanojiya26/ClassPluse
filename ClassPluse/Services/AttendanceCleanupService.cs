using ClassPluse.Data;
using ClassPluse.Models;
using Microsoft.EntityFrameworkCore;

namespace ClassPluse.Services
{
    public class AttendanceCleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AttendanceCleanupService> _logger;

        public AttendanceCleanupService(IServiceProvider serviceProvider, ILogger<AttendanceCleanupService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Attendance Cleanup Service running.");

            // Run every 1 hour
            using PeriodicTimer timer = new(TimeSpan.FromHours(1));

            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    await CleanupUnmarkedAttendanceAsync();
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Attendance Cleanup Service is stopping.");
            }
        }

        private async Task CleanupUnmarkedAttendanceAsync()
        {
            _logger.LogInformation("Checking for unmarked attendances to mark as absent.");

            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AttendanceDbContext>();

            var thresholdTime = DateTime.Now.AddHours(-6);

            // Find sessions that ended more than 6 hours ago
            var expiredSessions = await context.LectureSessions
                .Where(ls => !ls.IsActive && ls.EndTime != null && ls.EndTime <= thresholdTime)
                .ToListAsync();

            int newlyMarkedCount = 0;

            foreach (var session in expiredSessions)
            {
                // Get all enrolled students for this course
                var enrolledStudentIds = await context.Enrollments
                    .Where(e => e.CourseId == session.CourseId)
                    .Select(e => e.StudentId)
                    .ToListAsync();

                // Get students who already have an attendance record for this session
                var markedStudentIds = await context.AttendanceRecords
                    .Where(a => a.LectureSessionId == session.Id)
                    .Select(a => a.StudentId)
                    .ToListAsync();

                // Find the difference: enrolled but not marked
                var unmarkedStudentIds = enrolledStudentIds.Except(markedStudentIds).ToList();

                foreach (var studentId in unmarkedStudentIds)
                {
                    var newRecord = new AttendanceRecord
                    {
                        LectureSessionId = session.Id,
                        StudentId = studentId,
                        Status = AttendanceStatus.Absent,
                        MarkedAt = DateTime.Now,
                        MarkedBy = MarkedByType.System,
                        IPAddress = "127.0.0.1"
                    };

                    context.AttendanceRecords.Add(newRecord);
                    newlyMarkedCount++;
                }
            }

            if (newlyMarkedCount > 0)
            {
                await context.SaveChangesAsync();
                _logger.LogInformation($"Marked {newlyMarkedCount} unmarked students as absent.");
            }
        }
    }
}
