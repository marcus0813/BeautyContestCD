using API.Data;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using BeautyContestAPI.DTOs;
using BeautyContestAPI.Entities;
using BeautyContestAPI.Interface;
using Microsoft.EntityFrameworkCore;

namespace BeautyContestAPI.Repositorys
{
    public class AuditRepository : IAuditRepository
    {
        private readonly DataContext _context;
        private readonly IMapper _mapper;

        public AuditRepository(
            DataContext context,
            IMapper mapper
            )
        {
            _mapper = mapper;
            _context = context;
        }
        public void AddAuditLog(Audit audit)
        {
            _context.Audits.Add(audit);
        }

        public async Task<AuditDto> GetAuditAsync(string SessionId)
        {
            return await _context.Audits
                .Where(x => x.SessionId == SessionId)
                .ProjectTo<AuditDto>(_mapper.ConfigurationProvider)
                .SingleOrDefaultAsync();
        }

        public async Task<bool> SaveAllAsync()
        {
            return await _context.SaveChangesAsync() > 0;
        }
    }
}