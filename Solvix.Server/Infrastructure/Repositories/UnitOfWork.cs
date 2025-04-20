using Solvix.Server.Core.Interfaces;
using Solvix.Server.Data;

namespace Solvix.Server.Infrastructure.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ChatDbContext _context;
        private IUserRepository _userRepository;
        private IChatRepository _chatRepository;
        private IMessageRepository _messageRepository;

        public UnitOfWork(
            ChatDbContext context,
            IUserRepository userRepository,
            IChatRepository chatRepository,
            IMessageRepository messageRepository)
        {
            _context = context;
            _userRepository = userRepository;
            _chatRepository = chatRepository;
            _messageRepository = messageRepository;
        }

        public IUserRepository UserRepository => _userRepository;
        public IChatRepository ChatRepository => _chatRepository;
        public IMessageRepository MessageRepository => _messageRepository;

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
