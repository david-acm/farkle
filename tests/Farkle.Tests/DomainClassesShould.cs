using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Farkle.Tests;

public class DomainClassesShould
{

  private static readonly Architecture _architecture =
    new ArchLoader()
      .LoadAssemblies(typeof(AssemblyInfo).Assembly)
      .Build();


  [Fact]
  public void DomainTypesShouldNotReferenceInfrastructure()
  {
    var domainTypes = Types()
      .That()
      .ResideInNamespace("Farkle.Domain.*")
      .As("Domain Types");

    var infraTypes = Types()
      .That()
      // .ResideInNamespace("Farkle.Endpoints.*")
      // .Or()
      .ResideInAssembly("Farkle.Domain.*")
      .As("Infrastructure Types");

    var rule = domainTypes.Should().NotDependOnAny(infraTypes).WithoutRequiringPositiveResults();

    rule.Check(_architecture);
  }
  
  
  [Fact]
  public void BeInternal()
  {
    var domainTypes = Types()
      .That()
      .ResideInNamespace("Farkle.Domain.*")
      .And()
      .AreNot([typeof(AssemblyInfo)])
      .As("Domain Types");

    var rule = domainTypes.Should().BeInternal().WithoutRequiringPositiveResults();

    rule.Check(_architecture);
  } 
}
