using Solvix.Server.Core.Entities;

namespace Solvix.Server.Core.Interfaces
{
    public interface IGroupMemberRepository
    {
        Task<GroupMember?> GetMemberAsync(Guid chatId, long userId);
        Task<List<GroupMember>> GetMembersAsync(Guid chatId);
        Task<int> GetMemberCountAsync(Guid chatId);
        Task AddAsync(GroupMember member);
        Task UpdateAsync(GroupMember member);
        Task RemoveAsync(GroupMember member);
        Task DeleteAllMembersAsync(Guid chatId);
    }
}
