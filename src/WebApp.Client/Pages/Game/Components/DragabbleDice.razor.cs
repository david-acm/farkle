using Ardalis.GuardClauses;
using BlazorState;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using WebApp.Client.Features;
using static WebApp.Client.Pages.Game.Components.DieValue;

namespace WebApp.Client.Pages.Game.Components;

public partial class DragabbleDice : BlazorStateComponent
{
  private List<DraggableDie> _internalDice = [];
  
  [Inject]
  public ILogger<DragabbleDice> Logger { get; set; } = null!;
  
  private GameState GameState => GetState<GameState>();
  
  private List<DraggableDie> Dice => GameState.DiceInPlay;
  
  private List<DraggableDie> InternalDice()
  {
    if (Dice.Count == 0)
    {
      _internalDice.Clear();
      return _internalDice;
    }

    var diceToAdd = Dice.Count - _internalDice.Count;
    Logger.LogInformation("Current dice count: {currentCount}. Dice received: {receivedCount}", _internalDice.Count, Dice.Count);
    for (int i = 0; i < diceToAdd; i++)
    {
      _internalDice.Add(new DraggableDie(6-diceToAdd + i, One, "Rolled"));
    }
    
    for (int i = 0; i < Dice.Count; i++)
    {
      var die = Dice.ElementAt(i);
      
      if(_internalDice[i].Value == die.Value) continue;
      
      _internalDice[i].Value = die.Value;
      _internalDice[i].IsDragging = false;
    }
    
    var diceToRemove = Dice.Where(d => d.Identifier == "Kept").ToList();
    Logger.LogInformation("Removing {diceToRemove}", diceToRemove.Select(d => d.Value));
    foreach (var d in diceToRemove) _internalDice.RemoveAt(d.Index);
    Logger.LogInformation("Returning {diceToRemove}", _internalDice.Select(d => d.Value));
    return _internalDice;
  }
  
  protected override void OnInitialized()
  {
    _internalDice = Dice;
    base.OnInitialized();
  }
  
  private async Task ItemUpdatedAsync(MudItemDropInfo<DraggableDie> dropItem)
  {
    var item = Guard.Against.Null(dropItem.Item);
    
    var identifier = dropItem.DropzoneIdentifier;
    dropItem.Item.Identifier = identifier;
    await Mediator.Send(new GameState.SetDiceAside.Action(item));
    
    Logger.LogDebug("Dropped item with identifier: {identifier}", identifier);
    await Task.CompletedTask;
  }
}
