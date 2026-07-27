using FluentAssertions;
using Xunit;
using static HotDice.ArchitectureTests.ArchitectureModel;

namespace HotDice.ArchitectureTests;

/// <summary>
/// Guards the guardrails. Every rule here is of the form "assembly X must not depend on Y", and
/// each one returns an empty list — the passing answer — when the assembly simply is not loaded.
/// So a project rename, a dropped reference, or a typo in a dll name turns a real rule into a
/// permanently green one, silently.
///
/// That happened for real: the client rule named `WebApp.Client`, and when the UI moved out to
/// `HotDice.Ui` (#348) the rule kept passing while guarding an assembly that then held nothing but
/// an entry point. These tests make that failure mode loud.
/// </summary>
public class ArchitectureModelShould
{
    [Fact]
    public void LoadEveryAssemblyItClaimsToGuard()
    {
        var loaded = Model.Types.Select(t => t.Assembly.Name).Distinct().ToHashSet();

        // ClientAsm is deliberately absent: since #348 the WASM host is a single top-level
        // Program.cs and contributes no types, so there is nothing there for a rule to guard.
        // Naming it in a rule would be decoration — the UI it hosts is guarded via UiAsm.
        var declared = new[] { CoreAsm, SharedAsm, ApiClientAsm, InfraAsm, HostAsm, UiAsm };

        declared.Where(a => !loaded.Contains(a)).Should().BeEmpty(
            "a rule against an assembly that was never loaded passes vacuously — check the dll list in ArchitectureModel");
    }

    [Theory]
    [InlineData(UiAsm)]
    [InlineData(CoreAsm)]
    public void SeeRealTypesInTheAssembliesItGuards(string assembly)
    {
        // Loaded-but-empty is the subtler version of the same trap: the dll resolves, the rule runs,
        // and there is nothing in it to violate anything.
        Model.Types.Count(t => t.Assembly.Name == assembly).Should().BeGreaterThan(5,
            $"{assembly} should contribute real types to the model, not an empty shell");
    }
}
