using API.DTOs;
using API.Entities;
using API.Interface;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace API.Data
{
    public class AuditStripeSessionRepository: IAuditStripeSession
    {
        private readonly DataContext _context;
        private readonly IMapper _mapper;
        public AuditStripeSessionRepository(DataContext context, IMapper mapper)
        {
            _mapper = mapper;
            _context = context;
        }

        public async Task<AuditStripeSessionDto> GetAuditStripeAsync(string SessionId)
        {
            return await _context.AuditStripeSessions
            .Where(x => x.SessionId == SessionId)
            .ProjectTo<AuditStripeSessionDto>(_mapper.ConfigurationProvider)
            .SingleOrDefaultAsync();
        }

        public void AddAuditStripeLog(AuditStripeSession auditStripeSession) 
        {
            _context.AuditStripeSessions.Add(auditStripeSession);
        }
        public async Task<bool> SaveAllAsync()
        {
            return await _context.SaveChangesAsync() > 0;
        }
    }
}