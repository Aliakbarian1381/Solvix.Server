using Google;
using Solvix.Server.Core.Entities;
using Solvix.Server.Core.Interfaces;

namespace Solvix.Server.Infrastructure.Repositories
{
    public class GroupSettingsRepository : IGroupSettingsRepository
    {
        private readonly ApplicationDbContext _context;

        public GroupSettingsRepository(ApplicationDbContext context)
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
