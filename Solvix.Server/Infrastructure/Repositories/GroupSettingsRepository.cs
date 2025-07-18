using Microsoft.EntityFrameworkCore;
using Solvix.Server.Core.Entities;
using Solvix.Server.Core.Interfaces;
using Solvix.Server.Data;

namespace Solvix.Server.Infrastructure.Repositories
{
    public class GroupSettingsRepository : IGroupSettingsRepository
    {
        private readonly ChatDbContext _context;

        public GroupSettingsRepository(ChatDbContext context)
        {
            _context = context;
        }

        public async Task<GroupSettings?> GetSettingsAsync(Guid chatId)
        {
            return await _context.GroupSettings
                .FirstOrDefaultAsync(gs => gs.ChatId == chatId);
        }

        public async Task AddAsync(GroupSettings settings)
        {
            await _context.GroupSettings.AddAsync(settings);
        }

        public async Task UpdateAsync(GroupSettings settings)
        {
            _context.GroupSettings.Update(settings);
        }

        public async Task DeleteSettingsAsync(Guid chatId)
        {
            var settings = await _context.GroupSettings
                .FirstOrDefaultAsync(gs => gs.ChatId == chatId);

            if (settings != null)
            {
                _context.GroupSettings.Remove(settings);
            }
        }
    }
}