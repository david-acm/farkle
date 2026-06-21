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
/// We drop the <c>handler.*</c> leaf (redundant detail under <c>consumer.*</c>) and the
/// <c>checkpoint*</c> heartbeat, and keep <c>consumer.*</c> (the consume operation + latency),
/// the HTTP requests, exceptions, <c>eventstore.*</c>, <c>postgresql</c> and the domain customEvents.
///
/// #221 — we KEEP <c>sub.*</c>. It looks like infra noise but it's the <b>structural parent</b>
/// that links <c>consumer.*</c> back up to the produce span (the <c>eventstore.append</c> of the
/// originating command): <c>request → eventstore.append → sub.* → consumer.*</c>. #220 originally
/// dropped it as "redundant", but dropping a parent while keeping its child <b>orphans the child in
/// the trace tree</b> — the consumer's parent id then points at a span that was never exported, so
/// the broadcast renders as a sibling branch instead of nesting under the command that triggered it.
/// Correlation (shared trace id) survived, but the hierarchy didn't. Keeping <c>sub.*</c> is the
/// minimum needed for the broadcast/projector/telemetry consumers to nest under their command.
/// </summary>
internal sealed class SubscriptionNoiseSampler(Sampler inner) : Sampler
{
  // Span name prefixes that are pure subscription infrastructure noise. NOTE: "sub." is deliberately
  // NOT here (#221) — it's the structural parent that keeps consumer.* nested under the command.
  private static readonly string[] DropPrefixes = ["handler.", "checkpoint"];

  public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
  {
    var name = samplingParameters.Name;
    foreach (var prefix in DropPrefixes)
      if (name.StartsWith(prefix, StringComparison.Ordinal))
        return new SamplingResult(SamplingDecision.Drop);

    return inner.ShouldSample(samplingParameters);
  }
}
