using System.Net.Http.Headers;
using System.Text;
using DotNet.Testcontainers.Builders;
using Farkle.Application;
using Farkle.ApiClient;
using Farkle.ApiClient.Models;
using Marten;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using Farkle.Infrastructure.Identity;

namespace Farkle.WebTests;

// A deterministic id generator that yields a scripted sequence of ids so the collision-retry path
// in GameCreator.CreateGameAsync can be exercised.
public sealed class ScriptedGameIdGenerator : IGameIdGenerator
{
    private readonly Queue<int> _ids;

    public ScriptedGameIdGenerator(params int[] ids) => _ids = new Queue<int>(ids);

    public int Next() => _ids.Dequeue();
}

// Factory variant that overrides IGameIdGenerator with a scripted sequence [424242, 424242, 424243]:
// the second create collides on 424242 (the Marten stream already exists) and must retry, landing on
// 424243. Postgres-only (ADR 0004) — no ESDB. FARKLE_TEST_PG lets a developer run this without Docker.
public sealed class ScriptedIdGameApiWebAppFactory : FarkleWebApplicationFactory
{
    private static readonly ScriptedGameIdGenerator Scripted =
        new(424242, 424242, 424243);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var localConn = Environment.GetEnvironmentVariable("FARKLE_TEST_PG");
        string pgConn;

        if (!string.IsNullOrWhiteSpace(localConn))
        {
            pgConn = localConn;
        }
        else
        {
            var pgContainer = new ContainerBuilder("postgres:16-alpine")
                .WithPortBinding(5432, true)
                .WithEnvironment("POSTGRES_USER", "farkle_test")
                .WithEnvironment("POSTGRES_PASSWORD", "farkle_test")
                .WithEnvironment("POSTGRES_DB", "farkle_test")
                .WithWaitStrategy(Wait.ForUnixContainer()
                    .UntilCommandIsCompleted("pg_isready", "-h", "127.0.0.1", "-U", "farkle_test"))
                .WithAutoRemove(false).Build();

            pgContainer.StartAsync().GetAwaiter().GetResult();
            var pgPort = pgContainer.GetMappedPublicPort(5432);
            pgConn = $"Host=localhost;Port={pgPort};Database=farkle_test;Username=farkle_test;Password=farkle_test";
        }

        base.ConfigureWebHost(builder);

        builder.ConfigureAppConfiguration(cfg =>
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:RequireAuthorization"] = "true"
            }));

        builder.ConfigureServices(s =>
        {
            var dbContextDescriptor = s.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (dbContextDescriptor != null) s.Remove(dbContextDescriptor);
            s.AddDbContext<AppDbContext>(o => o.UseNpgsql(pgConn));

            s.ConfigureMarten(opts => opts.Connection(pgConn));

            // Override the id generator with the scripted sequence to drive a collision.
            s.RemoveAll<IGameIdGenerator>();
            s.AddSingleton<IGameIdGenerator>(Scripted);
        });
    }
}

public class GameIdGenerationShould : IClassFixture<ScriptedIdGameApiWebAppFactory>, IDisposable
{
    private readonly HttpClient               _httpClient;
    private readonly HttpClientRequestAdapter _adapter;
    private readonly FarkleApiClient          _client;

    public GameIdGenerationShould(ScriptedIdGameApiWebAppFactory factory)
    {
        var inner   = factory.Server.CreateHandler();
        var wrapped = new HttpClient(new EmptyBodyJsonHandler(inner))
        {
            BaseAddress = factory.Server.BaseAddress
        };
        _httpClient = wrapped;
        _adapter = new HttpClientRequestAdapter(
            new AnonymousAuthenticationProvider(), httpClient: _httpClient);
        _adapter.BaseUrl = _httpClient.BaseAddress?.ToString().TrimEnd('/') ?? string.Empty;
        _client = new FarkleApiClient(_adapter);

        AuthenticateAsync().GetAwaiter().GetResult();
    }

    private async Task AuthenticateAsync()
    {
        var email    = $"id-test-{Guid.NewGuid():N}@farkle.dev";
        const string password = "Test@123!";

        await _client.Api.Auth.Register.PostAsync(
            new RegisterRequest { Email = email, Password = password });

        var login = await _client.Api.Auth.Login.PostAsync(
            new LoginRequest { Email = email, Password = password });

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login!.Token);
    }

    [Fact]
    public async Task RetryWhenGeneratedIdCollidesAsync()
    {
        // First create takes 424242.
        var first = await _client.Api.Games.PostAsync();
        Assert.Equal(424242, first!.Id);

        // Second create draws 424242 again (collision) then retries to 424243.
        var second = await _client.Api.Games.PostAsync();
        Assert.Equal(424243, second!.Id);
    }

    public void Dispose()
    {
        _adapter.Dispose();
        _httpClient.Dispose();
    }
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
