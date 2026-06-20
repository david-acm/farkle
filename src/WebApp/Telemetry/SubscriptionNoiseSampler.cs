using OpenTelemetry.Trace;

namespace WebApp.Telemetry;

/// <summary>
/// #220 — drops the high-volume, low-value Eventuous subscription-infrastructure spans so the
/// traces stay readable. Each committed event fans out across the three subscriptions
/// (broadcast / projector / telemetry) into a 3-layer tree —
/// <c>sub.&lt;Sub&gt;/&lt;Event&gt;</c> (parent) → <c>consumer.&lt;Sub&gt;/&lt;Event&gt;</c> (the consume
/// operation) → <c>handler.&lt;Handler&gt;/&lt;Event&gt;</c> (leaf) — plus a per-subscription
/// <c>checkpoint.write</c> heartbeat.
///
/// We drop the <c>sub.*</c> and <c>handler.*</c> layers (redundant with <c>consumer.*</c>) and the
/// <c>checkpoint*</c> heartbeat, and keep <c>consumer.*</c> (which carries the consume latency),
/// the HTTP requests, exceptions, <c>eventstore.*</c>, <c>postgresql</c> and the domain
/// customEvents. Correlation is unaffected: it rides on the trace id (<c>operation_Id</c>), which
/// every span in the trace shares, not on span parenting — so dropping intermediate spans never
/// breaks the request → event linkage.
/// </summary>
internal sealed class SubscriptionNoiseSampler(Sampler inner) : Sampler
{
  // Span name prefixes that are pure subscription infrastructure noise.
  private static readonly string[] DropPrefixes = ["sub.", "handler.", "checkpoint"];

  public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
  {
    var name = samplingParameters.Name;
    foreach (var prefix in DropPrefixes)
      if (name.StartsWith(prefix, StringComparison.Ordinal))
        return new SamplingResult(SamplingDecision.Drop);

    return inner.ShouldSample(samplingParameters);
  }
}
