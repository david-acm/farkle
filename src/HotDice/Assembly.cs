
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("HotDice.Tests")]
[assembly: InternalsVisibleTo("HotDice.WebTests")]
[assembly: InternalsVisibleTo("HotDice.E2eTests")]
[assembly: InternalsVisibleTo("HotDice.SpaTests")]
// Infrastructure registers the internal GameBroadcastHandler subscription handler; it can't be
// public because its dependencies expose the internal GameState.
[assembly: InternalsVisibleTo("HotDice.Infrastructure")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
namespace HotDice;

public class AssemblyInfo { }
