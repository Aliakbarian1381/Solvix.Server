namespace Solvix.Server.Core.Interfaces
{
    public interface IUnitOfWork : IDisposable
    {
        IUserRepository UserRepository { get; }
        IChatRepository ChatRepository { get; }
        IMessageRepository MessageRepository { get; }
        IUserContactRepository UserContactRepository { get; }
        IGroupMemberRepository GroupMemberRepository { get; }
        IGroupSettingsRepository GroupSettingsRepository { get; }

        Task<int> CompleteAsync();
    }
}