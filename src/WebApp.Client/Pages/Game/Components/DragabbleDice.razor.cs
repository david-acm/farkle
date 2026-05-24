using Ardalis.GuardClauses;
using BlazorState;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using WebApp.Client.Features;

namespace WebApp.Client.Pages.Game.Components;

public partial class DragabbleDice : BlazorStateComponent
{
  [Inject]
  public ILogger<DragabbleDice> Logger { get; set; } = null!;

  private GameState GameState => GetState<GameState>();

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
