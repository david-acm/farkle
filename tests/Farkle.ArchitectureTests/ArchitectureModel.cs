using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArchUnitNET.Domain;
using ArchUnitNET.Loader;

namespace Farkle.ArchitectureTests;

/// <summary>
/// Shared architecture model + dependency helpers for the guardrail tests.
///
/// ArchUnitNET only turns *loaded* assemblies into first-class types, and its namespace
/// matchers are exact-match — so the fluent "NotDependOnAny(ResideInNamespace(...))" form
/// passes vacuously against unloaded external libraries (EventStore, EF, …). Instead we walk
/// each type's recorded dependencies (whose target namespaces are populated even for unloaded
/// targets) and match those namespaces by prefix. This detects real edges to ESDB/EF/SignalR
/// without having to load every infrastructure package into the model.
/// </summary>
internal static class ArchitectureModel
{
  // Production assembly simple-names (what IType.Assembly.Name reports). The "Asm" suffix avoids
  // colliding with the same-named Farkle.* namespaces when imported via `using static`.
  public const string CoreAsm         = "Farkle";              // domain + application + the Features/ slices
  public const string SharedAsm       = "Farkle.Shared";       // merged Contracts + SharedKernel leaf
  public const string ApiClientAsm    = "Farkle.ApiClient";
  public const string InfraAsm        = "Farkle.Infrastructure";
  public const string HostAsm         = "WebApp";
  public const string ClientAsm       = "WebApp.Client";

  private static readonly string[] AssemblyDlls =
  [
    "Farkle.dll", "Farkle.Shared.dll", "Farkle.ApiClient.dll",
    "WebApp.dll", "WebApp.Client.dll",
    "Farkle.Infrastructure.dll",
  ];

  public static readonly Architecture Model = Build();

  private static Architecture Build()
  {
    var dir = System.AppContext.BaseDirectory;
    var assemblies = AssemblyDlls
      .Select(dll => Path.Join(dir, dll))
      .Where(File.Exists)
      .Select(System.Reflection.Assembly.LoadFrom)
      .ToArray();
    return new ArchLoader().LoadAssemblies(assemblies).Build();
  }

  /// <summary>
  /// Returns "SourceType -> targetNamespace" for every dependency from a type in
  /// <paramref name="sourceAssembly"/> whose target namespace equals, or is nested under, any
  /// of the <paramref name="forbiddenNamespacePrefixes"/>. Empty = rule satisfied.
  /// </summary>
  public static IReadOnlyList<string> ForbiddenDependencies(
    string sourceAssembly, params string[] forbiddenNamespacePrefixes) =>
    ForbiddenDependencies(t => t.Assembly.Name == sourceAssembly, forbiddenNamespacePrefixes);

  /// <summary>As above, but the source set is any type matching <paramref name="source"/>.</summary>
  public static IReadOnlyList<string> ForbiddenDependencies(
    System.Func<IType, bool> source, params string[] forbiddenNamespacePrefixes) =>
    Model.Types
      .Where(source)
      .SelectMany(t => t.Dependencies.Select(d => new
      {
        Source = t.FullName,
        Ns = d.Target.Namespace?.FullName ?? string.Empty
      }))
      .Where(x => forbiddenNamespacePrefixes.Any(p =>
        x.Ns == p || x.Ns.StartsWith(p + ".", System.StringComparison.Ordinal)))
      .Select(x => $"{x.Source} -> {x.Ns}")
      .Distinct()
      .OrderBy(x => x)
      .ToList();

  /// <summary>True for types belonging to the Farkle core's <c>Farkle.Domain</c> namespaces.</summary>
  public static bool IsDomain(IType t) =>
    t.Assembly.Name == CoreAsm &&
    (t.Namespace.FullName == "Farkle.Domain" ||
     t.Namespace.FullName.StartsWith("Farkle.Domain.", System.StringComparison.Ordinal));

  /// <summary>
  /// True for a vertical-slice type: any type under the <c>Farkle.Features</c> namespace tree
  /// (#301). Slices colocate a command's decider (and, later, its endpoint/handler/response).
  /// </summary>
  public static bool IsFeatureSlice(IType t) =>
    t.Assembly.Name == CoreAsm &&
    (t.Namespace.FullName == "Farkle.Features" ||
     t.Namespace.FullName.StartsWith("Farkle.Features.", System.StringComparison.Ordinal));

  /// <summary>
  /// True for a slice's pure decision function (a <c>*Decider</c> type). Deciders must stay
  /// framework-free even though they live in the framework-coupled slice, so they get their own
  /// selector for the decider-purity guardrail (#301) — purity by test, not by project boundary.
  /// </summary>
  public static bool IsDecider(IType t) =>
    IsFeatureSlice(t) && t.Name.EndsWith("Decider", System.StringComparison.Ordinal);

  /// <summary>Concrete (instantiable) types in the model that implement the named interface.</summary>
  public static IReadOnlyList<IType> ConcreteImplementationsOf(string interfaceFullName) =>
    Model.Types
      .OfType<Class>()
      .Where(c => c.IsAbstract != true && c.ImplementedInterfaces.Any(i => i.FullName == interfaceFullName))
      .Cast<IType>()
      .ToList();
}
