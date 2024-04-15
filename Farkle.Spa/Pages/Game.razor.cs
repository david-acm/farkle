using Farkle.Spa.Components;
using Farkle.Spa.Services;
using Microsoft.AspNetCore.Components;
using static Farkle.Spa.Components.DragabbleDice;

namespace Farkle.Spa.Pages;

public partial class Game
{
  private int          _gameId;
  private int          _playerId     = 1;
  private int          _score        = new();
  private string _errorMessage = string.Empty;
  
  // TODO: Review if this is a good approach for the nullable services below
  [Inject] public IGameService GameService { get; set; } = null!;
  
  [Inject] public ILogger<Game> Logger { get; set; } = null!;
  
  [Parameter] public int PlayerId { get; set; }
  
  public List<DraggableDie> DiceInPlay { get; set; } =
  [
    new DraggableDie()
    {
      Index = 1, Value = DiceValue.One, Identifier = "Rolled"
    }
  ];
  
  public void ToggleOpen()
  {
    Error = !Error;
  }
  
  public List<DraggableDie> KeptDice   => DiceInPlay.Any() ? DiceInPlay.Where(d => d.Identifier == "Kept").ToList() : new();
  public  bool               Error      { get; set; }
  
  private async Task RollAsync()
  {
    
    var dice = await GameService.RollDiceAsync(_gameId, _playerId);
    if (!dice.IsSuccess)
    {
      Error         = true;
      _errorMessage = string.Join(", ", dice.Errors.ToList());
      return;
    }
    
    Logger.LogInformation("Set aside dice being updated in game from roll: {setAsideDice}", dice.Value.Select(d => d.Value));
    DiceInPlay = dice.Value.Select((d, i) => new DraggableDie
      {
        Index = i, Value = DiceValue.FromValue(d), Identifier = "Rolled"
      })
      .ToList();
  }
  
  protected override Task OnInitializedAsync()
  {
    var random = new Random();
    return Task.CompletedTask;
  }
  
  public void GameStarted(int gameId)
  {
    _gameId = gameId;
    Logger.LogInformation($"Game started with id: {gameId}");
  }
  
  private async Task DiceKeptAsync()
  {
    Logger.LogInformation("Sending request to keep dice: {dice}", KeptDice);
    _score = await GameService.KeepDiceAsync(_gameId, _playerId, KeptDice.Select(d => d.Value!.Value));
    KeptDice.ForEach(k => DiceInPlay.Remove(k));
  }
}
