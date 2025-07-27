// فایل: Infrastructure/Repositories/GroupMemberRepository.cs
using Microsoft.EntityFrameworkCore;
using Solvix.Server.Core.Entities;
using Solvix.Server.Core.Interfaces;
using Solvix.Server.Data;

namespace Solvix.Server.Infrastructure.Repositories
{
    public class GroupMemberRepository : IGroupMemberRepository
    {
        private readonly ChatDbContext _context;

        public GroupMemberRepository(ChatDbContext context)
        {
            _context = context;
        }

        public async Task<GroupMember?> GetMemberAsync(Guid chatId, long userId)
        {
            return await _context.GroupMembers
                .Include(gm => gm.User)
                .Include(gm => gm.Chat)
                .FirstOrDefaultAsync(gm => gm.ChatId == chatId && gm.UserId == userId);
        }

        public async Task<List<GroupMember>> GetMembersAsync(Guid chatId)
        {
            return await _context.GroupMembers
                .Include(gm => gm.User)
                .Where(gm => gm.ChatId == chatId)
                .OrderBy(gm => gm.JoinedAt)
                .ToListAsync();
        }

        public async Task<int> GetMemberCountAsync(Guid chatId)
        {
            return await _context.GroupMembers
                .CountAsync(gm => gm.ChatId == chatId);
        }

        public async Task AddAsync(GroupMember member)
        {
            await _context.GroupMembers.AddAsync(member);
        }

        public async Task UpdateAsync(GroupMember member)
        {
            _context.GroupMembers.Update(member);
            await _context.SaveChangesAsync();
        }

        public async Task RemoveAsync(GroupMember member)
        {
            _context.GroupMembers.Remove(member);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAllMembersAsync(Guid chatId)
        {
            var members = await _context.GroupMembers
                .Where(gm => gm.ChatId == chatId)
                .ToListAsync();

            _context.GroupMembers.RemoveRange(members);
            await _context.SaveChangesAsync();
        }
    }
}