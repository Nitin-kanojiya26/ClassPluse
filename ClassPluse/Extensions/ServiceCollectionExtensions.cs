using ClassPluse.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ClassPluse.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddClassPluseServices(this IServiceCollection services)
        {
            services.AddScoped<CryptoService>();
            services.AddScoped<QrTokenService>();
            services.AddScoped<IEmailService, SmtpEmailService>();
            
            services.AddScoped<AttendanceVerificationService>();
            services.AddSingleton<FaceRecognitionService>();

            return services;
        }
    }
}
