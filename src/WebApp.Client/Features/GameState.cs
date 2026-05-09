using BlazorState;
using WebApp.Client.Pages.Game.Components;

namespace WebApp.Client.Features;

public partial class GameState : State<GameState>
{
  public GameId    GameId    { get; private set; } = new GameId(0);
  public PlayerId  PlayerId  { get; private set; } = new PlayerId(0);
  public TurnScore TurnScore { get; private set; } = new(0);
  public bool      Error     { get; private set; }
  
  public List<DraggableDie> DiceInPlay { get; private set; } =
  [
    new DraggableDie(
      index: 1,
      value: DieValue.One,
      identifier: "Rolled")
  ];
  
  public List<DraggableDie> KeptDice => DiceInPlay.Any() ? DiceInPlay.Where(d => d.Identifier == "SetAside").ToList() : new();
  
  private List<int> DiceSetAside     { get; set; }         = [];
  public  string    ErrorMessage     { get; private set; } = string.Empty;
  public  bool      ShowError        { get; private set; };
  public  object    LastErrorMessage { get; private set; } = string.Empty;
  public  string?   WinnerName       { get; set; }
  
  public override void Initialize()
  {
    GameId       = new GameId(0);
    PlayerId     = new PlayerId(0);
    DiceSetAside = [];
  }
}

public record TurnScore(int Value);

public record GameId(int Value)
{
  public static implicit operator int(GameId id) => id.Value;
}

public record PlayerId(int Value)
{
  public static implicit operator int(PlayerId id) => id.Value;
}

public record PlayerName(string Value)
{
  public static implicit operator string(PlayerName name) => name.Value;
}
