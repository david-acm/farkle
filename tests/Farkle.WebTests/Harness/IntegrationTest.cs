using System.Net.Http.Headers;
using System.Text;
using Alba;
using Farkle.ApiClient;
using Farkle.ApiClient.Models;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using Wolverine;
using Wolverine.Tracking;

namespace Farkle.WebTests.Harness;

// Base class for the per-slice Alba integration tests. Drives requests through the generated Kiota
// FarkleApiClient (so the shared client stays covered at runtime — the layer that caught the #303
// mapping bugs) pointed at the shared IAlbaHost's in-memory TestServer, exactly as the old
// WebApplicationFactory tests did. Alba adds two things on top: the shared host, and TrackedSession
// waits for the broadcast cascade (Executed<T>() below) instead of Task.Delay.
public abstract class IntegrationTest : IAsyncLifetime
{
  protected AppFixture Fixture { get; }
  protected IAlbaHost Host => Fixture.Host;
  protected FarkleApiClient Client { get; }

  private readonly HttpClient _http;
  private readonly HttpClientRequestAdapter _adapter;

  protected IntegrationTest(AppFixture fixture)
  {
    Fixture = fixture;
    _http = new HttpClient(new EmptyBodyJsonHandler(fixture.Host.Server.CreateHandler()))
    {
      BaseAddress = fixture.Host.Server.BaseAddress,
    };
    _adapter = new HttpClientRequestAdapter(new AnonymousAuthenticationProvider(), httpClient: _http);
    _adapter.BaseUrl = _http.BaseAddress?.ToString().TrimEnd('/') ?? string.Empty;
    Client = new FarkleApiClient(_adapter);
  }

  // Reset Marten data for isolation, then register + log in so the client carries a bearer token for
  // the authorized game endpoints. Identity users accumulate (each test uses a fresh Guid email) —
  // ResetAllData only clears Marten, which is what the assertions read.
  public async Task InitializeAsync()
  {
    await Fixture.ResetAsync();
    await AuthenticateAsync();
  }

  public Task DisposeAsync()
  {
    _adapter.Dispose();
    _http.Dispose();
    return Task.CompletedTask;
  }

  private async Task AuthenticateAsync()
  {
    var email = $"it-{Guid.NewGuid():N}@farkle.dev";
    const string password = "Test@123!";

    await Client.Api.Auth.Register.PostAsync(new RegisterRequest { Email = email, Password = password });
    var login = await Client.Api.Auth.Login.PostAsync(new LoginRequest { Email = email, Password = password });

    _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Token);
  }

  // Runs the given request against the host and waits — deterministically, no sleep — for every
  // Wolverine message it cascades (the post-commit broadcast notifications) to finish processing,
  // then returns the tracked session so the test can assert which notification fired.
  protected Task<ITrackedSession> TrackAsync(Func<Task> request) =>
    Host.ExecuteAndWaitAsync(request);

  // Fires a raw authorized POST (empty JSON body, like the WASM client) and returns just the status
  // code — the Kiota client throws on non-2xx, which is awkward when a test wants to inspect the code
  // (e.g. asserting a concurrency race never surfaces 412/500).
  protected async Task<System.Net.HttpStatusCode> PostStatusAsync(string relativeUrl)
  {
    using var response = await _http.PostAsync(relativeUrl, content: null);
    return response.StatusCode;
  }

  // Drops the client's bearer token for the duration of the action (to assert the 401 path).
  protected async Task AsAnonymous(Func<Task> action)
  {
    var saved = _http.DefaultRequestHeaders.Authorization;
    _http.DefaultRequestHeaders.Authorization = null;
    try { await action(); }
    finally { _http.DefaultRequestHeaders.Authorization = saved; }
  }

  // --- Shared game-driving helpers (Kiota) so each slice test sets up just enough state. ---

  protected async Task<int> StartGameAsync() =>
    (await Client.Api.Games.PostAsync())?.Id ?? 0;

  protected async Task<int> JoinAsync(int gameId, string playerName) =>
    (await Client.Api.Games[gameId].Players.PostAsync(new JoinPlayerRequest { PlayerName = playerName }))?.Id ?? 0;

  protected Task<LobbyStateResponse?> BeginAsync(int gameId, int hostId = 1) =>
    Client.Api.Games[gameId].Start.PostAsync(new BeginGameRequest { PlayerId = hostId });

  protected Task<RollDiceResponse?> RollAsync(int gameId, int playerId) =>
    Client.Api.Games[gameId].Players[playerId].Rolls.PostAsync();

  protected Task<KeepDiceResponse?> KeepAsync(int gameId, int playerId, IEnumerable<int> dice) =>
    Client.Api.Games[gameId].Players[playerId].Keeps.PostAsync(
      new KeepDiceRequest { DiceValues = dice.Select(v => (int?)v).ToList() });

  protected Task<PassTurnResponse?> PassAsync(int gameId, int playerId) =>
    Client.Api.Games[gameId].Players[playerId].Turns.PostAsync();

  // Starts a two-player game with player 1 in turn, ready to roll.
  protected async Task<(int gameId, int player1, int player2)> StartTwoPlayerGameAsync()
  {
    var gameId  = await StartGameAsync();
    var player1 = await JoinAsync(gameId, "David");
    var player2 = await JoinAsync(gameId, "Allison");
    await BeginAsync(gameId);
    return (gameId, player1, player2);
  }
}

// Kiota issues body-less POSTs (StartGame, Rolls, Turns) with no Content-Type; inject an empty JSON
// body so the server binds them, mirroring the WASM production client's EmptyBodyJsonHandler.
internal sealed class EmptyBodyJsonHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
{
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
  {
    if (request.Method == HttpMethod.Post && request.Content == null)
      request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
    return base.SendAsync(request, ct);
  }
}
