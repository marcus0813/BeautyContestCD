using API.DTOs;
using API.Entities;

namespace API.Interface
{
    public interface IAuditStripeSession
    {
        Task<AuditStripeSessionDto> GetAuditStripeAsync(string Sessionid);

        void AddAuditStripeLog(AuditStripeSession auditStripeSession);

        Task<bool> SaveAllAsync();
    }
}