using Microsoft.AspNetCore.SignalR;


namespace Solvix.Server.Hubs
{
    public class ChatHub : Hub
    {
        private static Dictionary<string, string> UserConnections = new Dictionary<string, string>();

        public override async Task OnConnectedAsync()
        {
            string username = Context.Query["user"];
            if (!string.IsNullOrEmpty(username))
            {
                UserConnections[username] = Context.ConnectionId;
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var username = UserConnections.FirstOrDefault(x => x.Value == Context.ConnectionId).Key;
            if (username != null)
            {
                UserConnections.Remove(username);
            }
            await base.OnDisconnectedAsync(exception);
        }

        public async Task SendPrivateMessage(string recipientUsername, string message)
        {
            string senderUsername = UserConnections.FirstOrDefault(x => x.Value == Context.ConnectionId).Key;
            if (senderUsername == null) return; 
            if (UserConnections.TryGetValue(recipientUsername, out string recipientConnectionId))
            {
                await Clients.Client(recipientConnectionId).SendAsync("ReceiveMessage", senderUsername, message);
            }
        }
    }
}
