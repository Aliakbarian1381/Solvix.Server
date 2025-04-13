using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;


namespace Solvix.Server.Hubs
{
    public class ChatHub : Hub
    {
        private static ConcurrentDictionary<string, string> UserConnections = new();


        public override async Task OnConnectedAsync()
        {
            var httpContext = Context.GetHttpContext();

            if (httpContext != null && httpContext.Request.Query.TryGetValue("user", out var username))
            {
                if (!string.IsNullOrEmpty(username))
                {
                    UserConnections[username] = Context.ConnectionId;
                    await Clients.Caller.SendAsync("UserConnected", username);
                    await SendConnectedUsers();
                }

            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var username = UserConnections.FirstOrDefault(x => x.Value == Context.ConnectionId).Key;
            if (!string.IsNullOrEmpty(username))
            {
                UserConnections.TryRemove(username, out _);
                await Clients.All.SendAsync("UserDisconnected", username);
            }
            await base.OnDisconnectedAsync(exception);
        }


        public async Task SendPrivateMessage(string recipientUsername, string message)
        {
            string senderUsername = UserConnections.FirstOrDefault(x => x.Value == Context.ConnectionId).Key;
            if (string.IsNullOrEmpty(senderUsername))
                return;

            if (UserConnections.TryGetValue(recipientUsername, out string recipientConnectionId))
            {
                await Clients.Client(recipientConnectionId).SendAsync("ReceiveMessage", senderUsername, message);
            }
            else
            {
                await Clients.Caller.SendAsync("ErrorMessage", $"کاربر '{recipientUsername}' در حال حاضر آنلاین نیست.");
            }
        }


        private async Task SendConnectedUsers()
        {
            await Clients.All.SendAsync("ConnectedUsers", UserConnections.Keys.ToList());
        }
    }
}
