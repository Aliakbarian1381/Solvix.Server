using Solvix.Server.Core.Entities;

namespace Solvix.Server.Core.Interfaces
{
    public interface IUserRepository : IRepository<AppUser>
    {
        Task<AppUser?> GetByUsernameAsync(string username);
        Task<AppUser?> GetByPhoneNumberAsync(string phoneNumber);
        Task<bool> PhoneNumberExistsAsync(string phoneNumber);
        Task<List<AppUser>> SearchUsersAsync(string searchTerm, int limit = 20);
        Task<IEnumerable<AppUser>> FindUsersByPhoneNumbersAsync(IEnumerable<string> phoneNumbers);

    }
}
