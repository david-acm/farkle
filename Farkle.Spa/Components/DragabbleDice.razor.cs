using Ardalis.GuardClauses;
using BlazorState;
using Farkle.Spa.Features;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Farkle.Spa.Components;

public partial class DragabbleDice : BlazorStateComponent
{
  [Inject]
  public ILogger<DragabbleDice> Logger { get; set; } = null!;
  
  private GameState GameState => GetState<GameState>();
  
  private List<DraggableDie> Items => GameState.DiceInPlay;
  
  private async Task ItemUpdatedAsync(MudItemDropInfo<DraggableDie> dropItem)
  {
    var item = Guard.Against.Null(dropItem.Item);
    
    var identifier = dropItem.DropzoneIdentifier;
    dropItem.Item.Identifier = identifier;
    await Mediator.Send(new GameState.SetDiceAside.Action(item));
    
    Logger.LogInformation("Dropped item with identifier: {identifier}", identifier);
    await Task.CompletedTask;
  }
  
  public class DraggableDie(int index, DiceValue value, string identifier)
  {
    public int       Index      { get; init; } = index;
    public DiceValue Value      { get; init; } = value;
    public string    Identifier { get; set; } = identifier;
  }
}
