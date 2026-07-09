using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using Farkle.ApiClient;
using Farkle.ApiClient.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;

namespace Farkle.WebTests;

public class GameApiShould : IClassFixture<GameApiWebAppFactory>
{
    private readonly HttpClient            _httpClient;
    private readonly FarkleApiClient       _client;
    private readonly GameApiWebAppFactory  _factory;

    public GameApiShould(GameApiWebAppFactory factory)
    {
        _factory = factory;
        // FastEndpoints requires Content-Type: application/json even on bodyless POSTs.
        // Wrap the factory client's handler to inject an empty JSON body when none is present,
        // mirroring the EmptyBodyJsonHandler used by the WASM client in production.
        var inner   = factory.Server.CreateHandler();
        var wrapped = new HttpClient(new EmptyBodyJsonHandler(inner))
        {
            BaseAddress = factory.Server.BaseAddress
        };
        _httpClient = wrapped;
        var adapter = new HttpClientRequestAdapter(
            new AnonymousAuthenticationProvider(), httpClient: _httpClient);
        adapter.BaseUrl = _httpClient.BaseAddress?.ToString().TrimEnd('/') ?? string.Empty;
        _client = new FarkleApiClient(adapter);

        AuthenticateAsync().GetAwaiter().GetResult();
    }

    private async Task AuthenticateAsync()
    {
        var email    = $"test-{Guid.NewGuid():N}@farkle.dev";
        const string password = "Test@123!";

        await _client.Api.Auth.Register.PostAsync(
            new WebAppAuthRegisterRequest { Email = email, Password = password });

        var login = await _client.Api.Auth.Login.PostAsync(
            new WebAppAuthLoginRequest { Email = email, Password = password });

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login!.Token);
    }

    [Fact]
    public async Task MultiplePlayersCanJoinAndPlayAsync()
    {
        var gameId = await StartGameAsync();
        var player1 = await JoinGameAsync(gameId, "David");
        var player2 = await JoinGameAsync(gameId, "Allison");
        await BeginGameAsync(gameId);

        // Server assigns sequential IDs — no collisions
        Assert.Equal(1, player1);
        Assert.Equal(2, player2);

        // Player 1 goes first
        var roll1       = await _client.Api.Games[gameId].Players[player1].Rolls.PostAsync();
        var scoringDice = (roll1!.DiceValues ?? []).Where(v => v == 1 || v == 5).Select(v => (int)v!).ToArray();
        if (scoringDice.Length > 0)
            await KeepDiceAsync(gameId, player1, scoringDice);

        var pass = await PassTurnAsync(gameId, player1);
        Assert.Equal(player2, pass!.CurrentPlayerId);

        // Player 2 can now roll — turn rotated correctly
        var roll2 = await _client.Api.Games[gameId].Players[player2].Rolls.PostAsync();
        Assert.NotNull(roll2?.DiceValues);
        Assert.NotEmpty(roll2!.DiceValues!);
    }

    [Fact]
    public async Task CreateGameReturnsFreshPositiveIdAsync()
    {
        var id = await StartGameAsync();
        Assert.True(id > 0, "the server should generate a positive game id");
    }

    [Fact]
    public async Task TwoCreatesReturnDistinctIdsAsync()
    {
        var first  = await StartGameAsync();
        var second = await StartGameAsync();
        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task RejectUnauthenticatedRequestsAsync()
    {
        var savedAuth = _httpClient.DefaultRequestHeaders.Authorization;
        _httpClient.DefaultRequestHeaders.Authorization = null;

        var ex = await Assert.ThrowsAsync<ApiException>(
            () => _client.Api.Games.PostAsync());

        _httpClient.DefaultRequestHeaders.Authorization = savedAuth;
        Assert.Equal(401, ex.ResponseStatusCode);
    }

    [Fact]
    public async Task AllowPlayerToRollDiceAsync()
    {
        var gameId = await StartGameAsync();
        var player1 = await JoinGameAsync(gameId, "David");
        await JoinGameAsync(gameId, "Allison");
        await BeginGameAsync(gameId);

        var roll = await _client.Api.Games[gameId].Players[player1].Rolls.PostAsync();
        Assert.NotNull(roll?.DiceValues);
        Assert.NotEmpty(roll!.DiceValues!);

        var scoringDice = roll.DiceValues.Where(v => v == 1 || v == 5).Select(v => (int)v!).ToArray();
        if (scoringDice.Length > 0)
            await KeepDiceAsync(gameId, player1, scoringDice);
    }

    [Fact]
    public async Task PassTurnResetsTurnScoreAsync()
    {
        var gameId = await StartGameAsync();
        var player1 = await JoinGameAsync(gameId, "David");
        var player2 = await JoinGameAsync(gameId, "Allison");
        await BeginGameAsync(gameId);

        // Player 1 keeps all their scoring dice to build a non-zero turn score.
        var roll1        = await _client.Api.Games[gameId].Players[player1].Rolls.PostAsync();
        var scoringDice1 = (roll1!.DiceValues ?? []).Where(v => v == 1 || v == 5).Select(v => (int)v!).ToArray();
        int player1TurnScore = 0;
        if (scoringDice1.Length > 0)
        {
            var kept = await KeepDiceAsync(gameId, player1, scoringDice1);
            player1TurnScore = kept!.TurnScore ?? 0;
            Assert.True(player1TurnScore > 0, "turn score should be positive after keeping scoring dice");
        }

        // Passing locks the turn score into the player's cumulative game score.
        var pass = await PassTurnAsync(gameId, player1);
        Assert.NotNull(pass);
        Assert.Equal(player1TurnScore, pass.NewScore);

        // Player 2 can roll — confirming the game correctly advanced the turn with a fresh score.
        var roll2 = await _client.Api.Games[gameId].Players[player2].Rolls.PostAsync();
        Assert.NotNull(roll2!.DiceValues);
        Assert.NotEmpty(roll2.DiceValues);
    }

    [Fact]
    public async Task PassTurnIncludesScoreboardAsync()
    {
        var gameId = await StartGameAsync();
        var player1 = await JoinGameAsync(gameId, "David");
        var player2 = await JoinGameAsync(gameId, "Allison");
        await BeginGameAsync(gameId);
        _ = player2; // joined to populate scoreboard; not needed for further assertions

        var roll1        = await _client.Api.Games[gameId].Players[player1].Rolls.PostAsync();
        var scoringDice1 = (roll1!.DiceValues ?? []).Where(v => v == 1 || v == 5).Select(v => (int)v!).ToArray();
        if (scoringDice1.Length > 0)
            await KeepDiceAsync(gameId, player1, scoringDice1);

        var pass = await PassTurnAsync(gameId, player1);

        Assert.NotNull(pass?.Scoreboard);
        Assert.Equal(2, pass!.Scoreboard!.Count);

        var names = pass.Scoreboard.Select(p => p.Name).ToHashSet();
        Assert.Contains("David", names);
        Assert.Contains("Allison", names);

        var david = pass.Scoreboard.First(p => p.Name == "David");
        Assert.Equal(pass.NewScore, david.Score);
    }

    [Fact]
    public async Task RejectRollFromPlayerNotInTurnAsync()
    {
        var gameId = await StartGameAsync();
        var player1 = await JoinGameAsync(gameId, "David");
        var player2 = await JoinGameAsync(gameId, "Allison");
        await BeginGameAsync(gameId);
        _ = player1;

        // Player 1 goes first; player 2 rolling out of turn must be rejected.
        var ex = await Assert.ThrowsAsync<ApiException>(
            () => RollDiceAsync(gameId, player2));

        Assert.Equal(400, ex.ResponseStatusCode);
    }

    [Fact]
    public async Task RejectDoubleRollWithoutKeepingAsync()
    {
        var gameId = await StartGameAsync();
        var player1 = await JoinGameAsync(gameId, "David");
        await JoinGameAsync(gameId, "Allison");
        await BeginGameAsync(gameId);

        // First roll always succeeds.
        await RollDiceAsync(gameId, player1);

        // A second roll before keeping any dice must be rejected.
        var ex = await Assert.ThrowsAsync<ApiException>(
            () => RollDiceAsync(gameId, player1));

        Assert.Equal(400, ex.ResponseStatusCode);
    }

    [Fact]
    public async Task RejectPassWithoutRollingAsync()
    {
        var gameId = await StartGameAsync();
        var player1 = await JoinGameAsync(gameId, "David");
        await JoinGameAsync(gameId, "Allison");
        await BeginGameAsync(gameId);

        // Attempting to pass before rolling must be rejected.
        var ex = await Assert.ThrowsAsync<ApiException>(
            () => PassTurnAsync(gameId, player1));

        Assert.Equal(400, ex.ResponseStatusCode);
    }

    [Fact]
    public async Task ReturnLobbyRosterWhenPlayerJoinsAsync()
    {
        var gameId = await StartGameAsync();
        await _client.Api.Games[gameId].Players.PostAsync(
            new FarkleContractsHttpRequests_JoinPlayerRequest { PlayerName = "David" });
        var second = await _client.Api.Games[gameId].Players.PostAsync(
            new FarkleContractsHttpRequests_JoinPlayerRequest { PlayerName = "Allison" });

        Assert.NotNull(second);
        Assert.Equal("WaitingForPlayers", second!.Stage);
        Assert.Equal(1, second.HostPlayerId);
        Assert.NotNull(second.Roster);
        Assert.Equal(2, second.Roster!.Count);
        Assert.Contains(second.Roster, p => p.Name == "David");
        Assert.Contains(second.Roster, p => p.Name == "Allison");
    }

    [Fact]
    public async Task LetHostBeginGameAsync()
    {
        var gameId = await StartGameAsync();
        await JoinGameAsync(gameId, "David");
        await JoinGameAsync(gameId, "Allison");

        var lobby = await BeginGameAsync(gameId);

        Assert.NotNull(lobby);
        Assert.Equal("Rolling", lobby!.Stage);
        Assert.Equal(2, lobby.Roster!.Count);
    }

    [Fact]
    public async Task RejectBeginGameFromNonHostAsync()
    {
        var gameId = await StartGameAsync();
        await JoinGameAsync(gameId, "David");
        await JoinGameAsync(gameId, "Allison");

        // Player 2 is not the host; only player 1 (the creator) may begin play.
        var ex = await Assert.ThrowsAsync<ApiException>(() => BeginGameAsync(gameId, hostId: 2));

        Assert.Equal(400, ex.ResponseStatusCode);
    }

    [Fact]
    public async Task ReturnGameStateSnapshotForRefreshAsync()
    {
        var gameId  = await StartGameAsync();
        var player1 = await JoinGameAsync(gameId, "David");
        await JoinGameAsync(gameId, "Allison");
        await BeginGameAsync(gameId);

        // Player 1 rolls — the game is now mid-turn (stage Keeping, dice on the table).
        await RollDiceAsync(gameId, player1);

        // GET reads the GameView projection (#156), which is updated asynchronously by the
        // $all subscription — so poll until it reflects the roll (eventual consistency).
        FarkleContractsHttpResponses_GameStateResponse? snapshot = null;
        for (var attempt = 0; attempt < 50; attempt++)
        {
            snapshot = await _client.Api.Games[gameId].GetAsync();
            if (snapshot?.Stage == "Keeping") break;
            await Task.Delay(100);
        }

        Assert.NotNull(snapshot);
        Assert.Equal(gameId, snapshot!.GameId);
        Assert.Equal("Keeping", snapshot.Stage);
        Assert.Equal(player1, snapshot.CurrentPlayerId);
        Assert.Equal(1, snapshot.HostPlayerId);
        Assert.Null(snapshot.Winner);

        // The full scoreboard (names + scores) is restored, not just the lobby roster.
        Assert.NotNull(snapshot.Scoreboard);
        Assert.Equal(2, snapshot.Scoreboard!.Count);
        Assert.Contains(snapshot.Scoreboard, p => p.Name == "David");
        Assert.Contains(snapshot.Scoreboard, p => p.Name == "Allison");

        // The dice the active player just rolled are part of the snapshot.
        Assert.NotNull(snapshot.TableCenter);
        Assert.NotEmpty(snapshot.TableCenter!);
        Assert.All(snapshot.TableCenter!, v => Assert.InRange(v!.Value, 1, 6));
    }

    [Fact]
    public async Task ExposeTheBuildVersionAnonymouslyAsync()
    {
        // /version answers anonymously even though the factory requires authorization
        // (it's mapped before auth, like the health checks) and returns a non-empty version.
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/version");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        using var doc = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrWhiteSpace(doc.RootElement.GetProperty("version").GetString()));
    }

    [Fact]
    public async Task ReturnNotFoundForUnknownGameAsync()
    {
        var ex = await Assert.ThrowsAsync<ApiException>(
            () => _client.Api.Games[999_999].GetAsync());

        Assert.Equal(404, ex.ResponseStatusCode);
    }

    [Fact]
    public async Task ExposeTheLatestGameStateViaTheMartenReadModelAsync()
    {
        // ADR 0004 — the GameView read model is now the Inline Marten GameState snapshot; GET reads
        // it directly (read-your-own-writes), so no async projection to poll.
        var gameId  = await StartGameAsync();
        var player1 = await JoinGameAsync(gameId, "David");
        await JoinGameAsync(gameId, "Allison");
        await BeginGameAsync(gameId);
        await RollDiceAsync(gameId, player1);

        var state = await _client.Api.Games[gameId].GetAsync();

        Assert.NotNull(state);
        Assert.Equal(gameId, state!.GameId);
        Assert.Equal("Keeping", state.Stage); // GameStage.Keeping after a roll
    }

    // The server now generates the game id; the POST is body-less and returns the id.
    private async Task<int> StartGameAsync()
    {
        var response = await _client.Api.Games.PostAsync();
        return response?.Id ?? 0;
    }

    private async Task<int> JoinGameAsync(int gameId, string playerName)
    {
        var response = await _client.Api.Games[gameId].Players.PostAsync(
            new FarkleContractsHttpRequests_JoinPlayerRequest { PlayerName = playerName });
        return response?.Id ?? 0;
    }

    // The host (player 1) begins play, moving the game out of the lobby into Rolling.
    private Task<FarkleContractsHttpResponses_LobbyStateResponse?> BeginGameAsync(int gameId, int hostId = 1)
        => _client.Api.Games[gameId].Start.PostAsync(
            new FarkleContractsHttpRequests_BeginGameRequest { PlayerId = hostId });

    private Task RollDiceAsync(int gameId, int playerId)
        => _client.Api.Games[gameId].Players[playerId].Rolls.PostAsync();

    private Task<FarkleContractsHttpResponses_KeepDiceResponse?> KeepDiceAsync(int gameId, int playerId, int[] dice)
        => _client.Api.Games[gameId].Players[playerId].Keeps.PostAsync(
            new FarkleContractsHttpRequests_KeepDiceRequest
            {
                DiceValues = dice.Select(v => (int?)v).ToList()
            });

    private Task<FarkleContractsHttpResponses_PassTurnResponse?> PassTurnAsync(int gameId, int playerId)
        => _client.Api.Games[gameId].Players[playerId].Turns.PostAsync();
}

// FastEndpoints rejects POST requests with no Content-Type / body with 415.
// This handler injects an empty JSON body on bodyless POSTs so the Kiota client
// behaves the same way as the WASM production client (EmptyBodyJsonHandler).
file sealed class EmptyBodyJsonHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (request.Method == HttpMethod.Post && request.Content == null)
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        return base.SendAsync(request, ct);
    }
}
