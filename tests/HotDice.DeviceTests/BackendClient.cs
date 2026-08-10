using System.Net.Http.Json;

namespace HotDice.DeviceTests;

/// <summary>
/// A tiny HTTP client that acts as a SECOND player from outside the device, hitting the same
/// backend the app talks to. Joining a game this way makes the device observe a real
/// SignalR-pushed <c>LobbyChanged</c> update with no on-device interaction — that is the
/// "one SignalR-pushed update observed" acceptance criterion, exercised on a single device.
/// </summary>
internal sealed class BackendClient(Uri backendUri) : IDisposable
{
    private readonly HttpClient _http = new() { BaseAddress = backendUri };

    public async Task JoinAsync(int gameId, string playerName)
    {
        // Matches JoinPlayerRequest(int GameId, string PlayerName); game endpoints are anonymous
        // unless Auth:RequireAuthorization is on (the device gate backend runs with it off).
        using var response = await _http.PostAsJsonAsync(
            $"api/games/{gameId}/players",
            new { gameId, playerName });
        response.EnsureSuccessStatusCode();
    }

    public void Dispose() => _http.Dispose();
}
