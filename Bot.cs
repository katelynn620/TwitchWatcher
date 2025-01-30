using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;

namespace TwitchWatcher;

class Bot
{
    TwitchClient client;

    public Bot(string username, string accessToken)
    {
        ConnectionCredentials credentials = new ConnectionCredentials(username, accessToken);
        var clientOptions = new ClientOptions
        {
            MessagesAllowedInPeriod = 750,
            ThrottlingPeriod = TimeSpan.FromSeconds(30)
        };
        WebSocketClient customClient = new WebSocketClient(clientOptions);
        client = new TwitchClient(customClient);

        client.Initialize(credentials, new List<string>([username]));

        // client.OnLog += Client_OnLog;
        client.OnJoinedChannel += Client_OnJoinedChannel;
        client.OnConnected += Client_OnConnected;
        client.OnUserJoined += Client_OnUserJoined;
        client.OnUserLeft += Client_OnUserLeft;

        client.Connect();

    }

    private void Client_OnUserJoined(object? sender, OnUserJoinedArgs e)
    {
        Console.WriteLine($"{e.Username} joined channel {e.Channel}");
    }

    private void Client_OnUserLeft(object? sender, OnUserLeftArgs e)
    {
        Console.WriteLine($"{e.Username} left channel {e.Channel}");
    }

    private void Client_OnLog(object? sender, OnLogArgs e)
    {
        Console.WriteLine($"{e.DateTime}: {e.BotUsername} - {e.Data}");
    }

    private void Client_OnConnected(object? sender, OnConnectedArgs e)
    {
        Console.WriteLine($"Connected to {e.AutoJoinChannel}");
    }

    private void Client_OnJoinedChannel(object? sender, OnJoinedChannelArgs e)
    {
        Console.WriteLine($"Channel joined: {e.Channel}");
    }
}

