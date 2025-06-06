using Solvix.Server.Core.Entities;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Solvix.Server.Core.Interfaces
{
    public interface INotificationService
    {
        Task SendNotificationAsync(AppUser user, string title, string body, Dictionary<string, string> data);
    }
}