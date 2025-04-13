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
                UserConnections[username] = Context.ConnectionId;
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var username = UserConnections.FirstOrDefault(x => x.Value == Context.ConnectionId).Key;
            if (!string.IsNullOrEmpty(username))
            {
                UserConnections.TryRemove(username, out _);
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
        }
    }
}
