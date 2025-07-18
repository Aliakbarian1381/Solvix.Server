using Google;
using Solvix.Server.Core.Interfaces;
using Solvix.Server.Core.Interfaces.Solvix.Server.Core.Interfaces;
using Solvix.Server.Data;

namespace Solvix.Server.Infrastructure.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _context;
        private IUserRepository? _userRepository;
        private IChatRepository? _chatRepository;
        private IMessageRepository? _messageRepository;
        private IGroupMemberRepository? _groupMemberRepository;
        private IGroupSettingsRepository? _groupSettingsRepository;

        public UnitOfWork(ApplicationDbContext context)
        {
            _context = context;
        }

        public IUserRepository UserRepository =>
            _userRepository ??= new UserRepository(_context);

        public IChatRepository ChatRepository =>
            _chatRepository ??= new ChatRepository(_context);

        public IMessageRepository MessageRepository =>
            _messageRepository ??= new MessageRepository(_context);

        public IGroupMemberRepository GroupMemberRepository =>
            _groupMemberRepository ??= new GroupMemberRepository(_context);

        public IGroupSettingsRepository GroupSettingsRepository =>
            _groupSettingsRepository ??= new GroupSettingsRepository(_context);

        public async Task<int> CompleteAsync()
        {
            return await _context.SaveChangesAsync();
        }

        public void Dispose()
        {
            _context?.Dispose();
        }
    }
}
