using System.Net;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TwitchWatcher;

class Program
{
    private static readonly string clientId = Environment.GetEnvironmentVariable("CLIENT_ID") ?? throw new Exception("env:CLIENT_ID is missing");
    private static readonly string clientSecret = Environment.GetEnvironmentVariable("CLIENT_SECRET") ?? throw new Exception("env:CLIENT_SECRET is missing");
    private static readonly string redirectUri = "http://localhost:8080/";
    private static readonly string[] scopes = { "user:read:email", "chat:read", "chat:edit" }; // for irc
    private static readonly string tokenFilePath = "twitch_tokens.json";
    private static readonly HttpClient httpClient = new();

    static async Task Main()
    {
        TokenData? tokens = LoadTokens();

        if (tokens != null && !string.IsNullOrEmpty(tokens.AccessToken))
        {
            if (await IsTokenValid(tokens.AccessToken))
            {
                Console.WriteLine($"use cached access token: {tokens.AccessToken}");
            }
            else
            {
                Console.WriteLine("access token is expired, refreshing with refresh token...");
                tokens = await RefreshAccessToken(tokens.RefreshToken);
                SaveTokens(tokens);
            }
        }

        if (tokens == null || string.IsNullOrEmpty(tokens.AccessToken))
        {
            string oauthUrl = $"https://id.twitch.tv/oauth2/authorize" +
                              $"?client_id={clientId}" +
                              $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                              $"&response_type=code" +
                              $"&scope={Uri.EscapeDataString(string.Join(" ", scopes))}";

            Console.WriteLine("Please use browser to login:");
            Console.WriteLine(oauthUrl);

            // waiting for oauth
            string code = await WaitForOAuthCallback();
            Console.WriteLine($"get auth code: {code}");

            // use code to get access token
            tokens = await GetAccessToken(code);
            SaveTokens(tokens);

            Console.WriteLine($"get access token: {tokens.AccessToken}");
        }

        UserInfo userInfo = await GetUserInfo(tokens.AccessToken);
        Console.WriteLine($"username: {userInfo.Username}");
        Console.WriteLine($"user id: {userInfo.Id}");

        Bot bot = new Bot(userInfo.Username, tokens.AccessToken);
        Console.ReadLine();
    }

    static async Task<string> WaitForOAuthCallback()
    {
        var listener = new HttpListener();
        listener.Prefixes.Add(redirectUri);
        listener.Start();

        Console.WriteLine("waiting for twitch oauth ...");
        var context = await listener.GetContextAsync();
        var response = context.Response;

        string code = context?.Request.QueryString["code"] ?? throw new Exception("failed to fetch code");
        byte[] buffer = Encoding.UTF8.GetBytes("Successful, please close this page.");
        response.ContentLength64 = buffer.Length;
        response.OutputStream.Write(buffer, 0, buffer.Length);
        response.OutputStream.Close();
        listener.Stop();

        return code;
    }

    static async Task<UserInfo> GetUserInfo(string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://id.twitch.tv/oauth2/validate");
        request.Headers.Add("Authorization", $"OAuth {accessToken}");

        using var response = await httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception("failed to get userinfo");
        }

        string responseBody = await response.Content.ReadAsStringAsync();

        UserInfo? userInfo = JsonConvert.DeserializeObject<UserInfo>(responseBody) ?? throw new Exception("failed to deserialize userInfo");
        return userInfo;
    }

    static async Task<TokenData> GetAccessToken(string code)
    {
        var url = "https://id.twitch.tv/oauth2/token";
        var content = new StringContent($"client_id={clientId}&client_secret={clientSecret}&code={code}" +
                                        $"&grant_type=authorization_code&redirect_uri={Uri.EscapeDataString(redirectUri)}",
                                        Encoding.UTF8, "application/x-www-form-urlencoded");

        using var response = await httpClient.PostAsync(url, content);
        response.EnsureSuccessStatusCode();

        string responseBody = await response.Content.ReadAsStringAsync();
        JObject json = JObject.Parse(responseBody);

        return new TokenData
        {
            AccessToken = json["access_token"]?.ToString() ?? throw new Exception("failed to get access token"),
            RefreshToken = json["refresh_token"]?.ToString() ?? throw new Exception("failed to get refresh token")
        };
    }

    static async Task<TokenData> RefreshAccessToken(string refreshToken)
    {
        var url = "https://id.twitch.tv/oauth2/token";
        var content = new StringContent($"client_id={clientId}&client_secret={clientSecret}&refresh_token={refreshToken}" +
                                        $"&grant_type=refresh_token",
                                        Encoding.UTF8, "application/x-www-form-urlencoded");

        using var response = await httpClient.PostAsync(url, content);
        response.EnsureSuccessStatusCode();

        string responseBody = await response.Content.ReadAsStringAsync();
        JObject json = JObject.Parse(responseBody);

        return new TokenData
        {
            AccessToken = json["access_token"]?.ToString() ?? throw new Exception("failed to get new access token"),
            RefreshToken = json["refresh_token"]?.ToString() ?? refreshToken // keep cached refresh token if server doesn't not reply
        };
    }

    static async Task<bool> IsTokenValid(string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://id.twitch.tv/oauth2/validate");
        request.Headers.Add("Authorization", $"OAuth {accessToken}");

        using var response = await httpClient.SendAsync(request);
        return response.IsSuccessStatusCode;
    }

    static void SaveTokens(TokenData tokens)
    {
        File.WriteAllText(tokenFilePath, JsonConvert.SerializeObject(tokens));
    }

    static TokenData? LoadTokens()
    {
        if (File.Exists(tokenFilePath))
        {
            string json = File.ReadAllText(tokenFilePath);
            return JsonConvert.DeserializeObject<TokenData>(json);
        }
        return null;
    }

    class TokenData
    {
        public string AccessToken { get; set; } = "";
        public string RefreshToken { get; set; } = "";
    }

    class UserInfo
    {
        [JsonProperty("login")]
        public string Username { get; set; } = "";
        [JsonProperty("user_id")]
        public string Id { get; set; } = "";
    }
}
