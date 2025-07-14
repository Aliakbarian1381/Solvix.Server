using Solvix.Server.Core.Interfaces;
using Solvix.Server.Core.Interfaces.Solvix.Server.Core.Interfaces;
using Solvix.Server.Data;

namespace Solvix.Server.Infrastructure.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ChatDbContext _context;
        private IUserRepository _userRepository;
        private IChatRepository _chatRepository;
        private IMessageRepository _messageRepository;
        private IUserContactRepository? _userContactRepository;

        public UnitOfWork(
            ChatDbContext context,
            IUserRepository userRepository,
            IChatRepository chatRepository,
            IMessageRepository messageRepository,
            IUserContactRepository userContactRepository)
        {
            _context = context;
            _userRepository = userRepository;
            _chatRepository = chatRepository;
            _messageRepository = messageRepository;
            _userContactRepository = userContactRepository;
        }

        public IUserRepository UserRepository => _userRepository;
        public IChatRepository ChatRepository => _chatRepository;
        public IMessageRepository MessageRepository => _messageRepository;
        public IUserContactRepository UserContactRepository => _userContactRepository ??= new UserContactRepository(_context);

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
