using FirebaseAdmin.Messaging;
using Solvix.Server.Core.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Solvix.Server.Core.Interfaces
{
    public interface INotificationService
    {
        // امضای متد تغییر کرده است
        Task SendNotificationAsync(AppUser user, Notification notification, Dictionary<string, string> data);
    }
}