using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Farkle.E2eTests;

/// <summary>
/// Browser-free checks that the storyboard host boots with the stubbed backend
/// (in-memory aggregate store, no Postgres / EventStore / Docker) and that the
/// in-memory store round-trips events through the real domain. Drives the HTTP API
/// directly via the factory's test client, so it runs anywhere — no Playwright.
///
/// Tagged Storyboard so the infra-light workflow selects it; it shares the light
/// <see cref="StoryboardWebAppFactory"/> and never starts containers.
/// </summary>
[Trait("Category", "Storyboard")]
public class BackendStubShould(StoryboardWebAppFactory factory) : IClassFixture<StoryboardWebAppFactory>
{
    [Fact]
    public async Task Boot_and_round_trip_a_game_through_the_in_memory_store()
    {
        var client = factory.CreateClient();

        // Start a new game (server generates the id) — proves DI built without ESDB/Postgres.
        var start = await client.PostAsync("/api/games", content: null);
        start.StatusCode.Should().Be(HttpStatusCode.OK);
        var gameId = JsonDocument.Parse(await start.Content.ReadAsStringAsync())
            .RootElement.GetProperty("id").GetInt32();

        // Two players join — exercises Store (append) + Load (replay) on the in-memory store.
        foreach (var name in new[] { "Alice", "Bob" })
        {
            var join = await client.PostAsJsonAsync($"/api/games/{gameId}/players",
                new { gameId, playerName = name });
            join.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // Reload the snapshot — replays the whole stream and proves both joins persisted.
        var state = await client.GetAsync($"/api/games/{gameId}");
        state.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await state.Content.ReadAsStringAsync();
        body.Should().Contain("Alice").And.Contain("Bob");
    }
}
