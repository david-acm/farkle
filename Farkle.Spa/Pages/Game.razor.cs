using Farkle.Spa.Components;
using Farkle.Spa.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using static Farkle.Spa.Components.DragabbleDice;

namespace Farkle.Spa.Pages;

public partial class Game
{
  private int                   _gameId;
  private int                   _playerId = 1;
  private Dictionary<int, int> _score = new();
  
  // TODO: Review if this is a good approach for the nullable services below
  [Inject] public IGameService GameService { get; set; } = null!;
  
  [Inject] public ILogger<Game> Logger { get; set; } = null!;
  
  [Parameter] public int PlayerId { get; set; }
  
  public  List<DraggableDie> DiceInPLay { get; set; } = [new DraggableDie() { Index = 1, Value = DiceValue.One, Identifier = "Rolled"}];

  private IEnumerable<int>   KeptDice   => DiceInPLay.Where(d => d.Identifier == "Kept").Select(d => d.Value!.Value);
  
  private async Task RollAsync()
  {
    var dice = await GameService.RollDiceAsync(_gameId, _playerId);
    Logger.LogInformation("Set aside dice being updated in game from roll: {setAsideDice}", dice.Select(d => d.Value));
    DiceInPLay = dice.Select((d, i) => new DraggableDie
    {
      Index      = i,
      Value      = DiceValue.FromValue(d),
      Identifier = "Rolled"
    }).ToList();
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
    var score = await GameService.KeepDiceAsync(_gameId, _playerId, KeptDice);
    _score = new Dictionary<int, int>(score);
  }
}
