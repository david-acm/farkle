using BlazorState;
using Microsoft.AspNetCore.Components;
using WebApp.Client.Features;

namespace WebApp.Client.Pages.Game;

public partial class Game : BlazorStateComponent
{
  private string _errorMessage = string.Empty;
  
  // TODO: Review if this is a good approach for the nullable services below
  [Inject]
  public ILogger<Game> Logger { get; set; } = null!;
  
  [Parameter]
  public int PlayerId { get; set; }
  
  [Parameter]
  public int ParameterGameId { get; set; } = 0;
  
  private GameId GameId  => new(this.ParameterGameId);
  
  private GameState GameState => GetState<GameState>();
  
  protected override async Task OnParametersSetAsync()
  {
    await Mediator.Send(new GameState.StartGame.Action(GameId));
    Logger.LogInformation($"Game started with id: {GameId}");
    await Mediator.Send(new GameState.JoinPlayer.Action(new(PlayerId), new(string.Empty)));
    await base.OnParametersSetAsync();
  }
}
