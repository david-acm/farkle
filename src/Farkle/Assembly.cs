
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Farkle.Tests")]
[assembly: InternalsVisibleTo("Farkle.WebTests")]
[assembly: InternalsVisibleTo("Farkle.E2eTests")]
[assembly: InternalsVisibleTo("Farkle.SpaTests")]
// Infrastructure registers the internal GameBroadcastHandler subscription handler; it can't be
// public because its dependencies expose the internal GameState.
[assembly: InternalsVisibleTo("Farkle.Infrastructure")]
// #292 — the extracted HTTP endpoints are internal and map internal commands/state, so they see
// the core's internals here rather than forcing those types public (they must stay internal, per
// the domain-purity guardrail).
[assembly: InternalsVisibleTo("Farkle.Endpoints")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
namespace Farkle;

public class AssemblyInfo { }
