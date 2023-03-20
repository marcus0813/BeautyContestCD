using BeautyContestAPI.DTOs;
using BeautyContestAPI.Entities;

namespace BeautyContestAPI.Interface
{
    public interface IAuditRepository
    {
        Task<AuditDto> GetAuditAsync(string SessionId);
        void AddAuditLog(Audit audit);
        Task<bool> SaveAllAsync();
    }
}