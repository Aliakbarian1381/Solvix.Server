using Solvix.Server.Core.Interfaces;
using Solvix.Server.Data;

namespace Solvix.Server.Infrastructure.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ChatDbContext _context;
        private IChatRepository? _chatRepository;
        private IMessageRepository? _messageRepository;
        private IUserRepository? _userRepository;
        private IGroupSettingsRepository? _groupSettingsRepository;
        private IUserContactRepository? _userContactRepository;

        public UnitOfWork(ChatDbContext context)
        {
            _context = context;
        }

        public IChatRepository ChatRepository =>
            _chatRepository ??= new ChatRepository(_context);

        public IMessageRepository MessageRepository =>
            _messageRepository ??= new MessageRepository(_context);

        public IUserRepository UserRepository =>
            _userRepository ??= new UserRepository(_context);

      
        public IGroupSettingsRepository GroupSettingsRepository =>
            _groupSettingsRepository ??= new GroupSettingsRepository(_context);

        public IUserContactRepository UserContactRepository =>
            _userContactRepository ??= new UserContactRepository(_context);

        public async Task<int> CompleteAsync()
        {
            return await _context.SaveChangesAsync();
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}