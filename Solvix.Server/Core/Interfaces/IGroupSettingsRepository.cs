using Solvix.Server.Core.Entities;

namespace Solvix.Server.Core.Interfaces
{
    public interface IGroupSettingsRepository
    {
        Task<GroupSettings?> GetSettingsAsync(Guid chatId);
        Task AddAsync(GroupSettings settings);
        Task UpdateAsync(GroupSettings settings);
        Task DeleteSettingsAsync(Guid chatId);
    }
}
