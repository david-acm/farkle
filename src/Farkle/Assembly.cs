
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Farkle.Tests")]
[assembly: InternalsVisibleTo("Farkle.WebTests")]
[assembly: InternalsVisibleTo("Farkle.E2eTests")]
[assembly: InternalsVisibleTo("Farkle.SpaTests")]
// Infrastructure registers the internal GameBroadcastHandler subscription handler; it can't be
// public because its dependencies expose the internal GameState.
[assembly: InternalsVisibleTo("Farkle.Infrastructure")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
namespace Farkle;

public class AssemblyInfo { }
