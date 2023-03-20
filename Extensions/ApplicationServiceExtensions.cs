using API.Data;
using API.Helpers;
using API.Interface;
using API.Services;
using API.SignalR;
using Microsoft.EntityFrameworkCore;

namespace API.Extensions
{
    public static class ApplicationServiceExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services,
        IConfiguration configuration)
        {
            services.AddDbContext<DataContext>(opt =>
            {
                opt.UseSqlite(configuration.GetConnectionString("DefaultConnection"));
            });

            services.AddCors();
            services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());
            services.Configure<BlobStorageSettings>(configuration.GetSection("BlobStorageSettings"));
            services.AddScoped<LogUserActivity>();

            #region 'Service'
            services.AddScoped<IPhotoService, PhotoService>();
            services.AddScoped<ITokenService, TokenService>();
            services.AddScoped<IUserRepository, UserRepository>();
            #endregion

            #region 'Repository'
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IAuditStripeSession, AuditStripeSessionRepository>();
            #endregion

            services.AddSignalR();
            services.AddSingleton<PresenceTracker>();

            return services;
        }
    }
}