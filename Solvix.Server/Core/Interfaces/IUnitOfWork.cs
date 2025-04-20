namespace Solvix.Server.Core.Interfaces
{
    public interface IUnitOfWork : IDisposable
    {
        IUserRepository UserRepository { get; }
        IChatRepository ChatRepository { get; }
        IMessageRepository MessageRepository { get; }

        Task<int> CompleteAsync();
    }
}
