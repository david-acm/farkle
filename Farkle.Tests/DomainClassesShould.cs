using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using Farkle.Domain;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Farkle.Tests;

public class DomainClassesShould
{

  private static readonly Architecture _architecture =
    new ArchLoader()
      .LoadAssemblies(typeof(Farkle.Domain.AssemblyInfo).Assembly)
      .LoadAssemblies(typeof(Farkle.WebApi.AssemblyInfo).Assembly)
      .Build();


  [Fact]
  public void DomainTypesShouldNotReferenceInfrastructure()
  {
    var domainTypes = Types()
      .That()
      .ResideInNamespace("Farkle.Domain.*", useRegularExpressions: true)
      .As("Domain Types");

    var dataTypes = Types()
      .That()
      .ResideInNamespace("Farkle.WebApo.*", useRegularExpressions: true)
      .As("Infrastructure Types");

    var rule = domainTypes.Should().NotDependOnAny(dataTypes);

    rule.Check(_architecture);
  }
  
  
  [Fact]
  public void BeInternal()
  {
    var domainTypes = Types()
      .That()
      .ResideInNamespace("Farkle.Domain.*", useRegularExpressions: true)
      .And()
      .AreNot([typeof(AssemblyInfo)])
      .As("Domain Types");

    var rule = domainTypes.Should().BeInternal();
    
    rule.Check(_architecture);
  } 
}
