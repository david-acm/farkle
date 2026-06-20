using System.Diagnostics;
using FluentAssertions;
using OpenTelemetry.Trace;
using WebApp.Telemetry;
using Xunit;

namespace Farkle.WebTests;

// #220 — the OTel sampler drops the redundant Eventuous subscription-infrastructure spans
// (sub.* / handler.* / checkpoint*) and delegates everything else to the inner sampler.
public class SubscriptionNoiseSamplerShould
{
  private static readonly Sampler Sut = new SubscriptionNoiseSampler(new AlwaysOnSampler());

  private static SamplingResult Sample(string name) =>
    Sut.ShouldSample(new SamplingParameters(default, ActivityTraceId.CreateRandom(), name, ActivityKind.Internal));

  [Theory]
  [InlineData("sub.GameBroadcasts/V1.DiceKept")]
  [InlineData("handler.GameTelemetryHandler/V1.DiceKept")]
  [InlineData("checkpoint.write")]
  public void DropSubscriptionInfrastructureSpans(string name) =>
    Sample(name).Decision.Should().Be(SamplingDecision.Drop);

  [Theory]
  [InlineData("consumer.GameViewProjector/V1.DiceKept")] // the consume operation — keep (latency)
  [InlineData("eventstore.append/Game-1")]
  [InlineData("POST /api/games/{gameId}/players/{playerId}/keeps")]
  [InlineData("postgresql")]
  public void KeepEverythingElse(string name) =>
    Sample(name).Decision.Should().Be(SamplingDecision.RecordAndSample);
}
