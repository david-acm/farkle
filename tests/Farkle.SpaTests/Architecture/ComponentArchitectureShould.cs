using System.Reflection;
using FluentAssertions;
using WebApp.Client.Pages.Game.Components;

namespace Farkle.SpaTests.Architecture;

public class ComponentArchitectureShould
{
  [Fact]
  public void NotHaveMutableListFieldsOnBlazorStateComponents()
  {
    var componentTypes = typeof(DiceTray).Assembly
      .GetTypes()
      .Where(t => !t.IsAbstract && InheritsBlazorStateComponent(t));

    var violations = componentTypes
      .SelectMany(t => t.GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
        .Select(f => (Type: t, Field: f)))
      .Where(x => x.Field.FieldType.IsGenericType &&
                  x.Field.FieldType.GetGenericTypeDefinition() == typeof(List<>))
      .Select(x => $"{x.Type.Name}.{x.Field.Name}")
      .ToList();

    violations.Should().BeEmpty(
      "BlazorState components must store all collection state in GameState, " +
      "not in local fields that shadow the store and go out of sync");
  }

  private static bool InheritsBlazorStateComponent(Type t)
  {
    var current = t.BaseType;
    while (current is not null)
    {
      if (current.FullName == "BlazorState.BlazorStateComponent") return true;
      current = current.BaseType;
    }
    return false;
  }
}
