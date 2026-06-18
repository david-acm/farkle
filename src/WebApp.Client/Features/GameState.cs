using BlazorState;
using Farkle.SharedKernel.Scoring;
using WebApp.Client.Pages.Game.Components;

namespace WebApp.Client.Features;

public partial class GameState : State<GameState>
{
  public GameId     GameId          { get; private set; } = new GameId(0);
  public PlayerId   PlayerId        { get; private set; } = new PlayerId(0);
  public PlayerName PlayerName      { get; private set; } = new(string.Empty);
  public TurnScore  TurnScore       { get; private set; } = new(0);
  public int        CurrentPlayerId { get; internal set; } = 0;
  public bool       IsMyTurn        => CurrentPlayerId == PlayerId.Value;
  public IReadOnlyList<PlayerStanding> Scoreboard  { get; internal set; } = [];

  // The identity colour of the player whose turn it is. Drives the dice/turn UI so every
  // player (in-turn or spectating) sees whose turn it is. Falls back to the default accent
  // before any colour is known (e.g. mid-join). Looks in the scoreboard first, then the
  // lobby roster.
  public const string DefaultColor = "#FFE600";
  public string ActivePlayerColor =>
    PlayerColorFor(CurrentPlayerId);

  public string PlayerColorFor(int playerId)
  {
    var color = Scoreboard.FirstOrDefault(p => p.PlayerId == playerId)?.Color
                ?? Roster.FirstOrDefault(p => p.PlayerId == playerId)?.Color;
    return string.IsNullOrWhiteSpace(color) ? DefaultColor : color;
  }
  public string?    WinnerName      { get; internal set; }
  public bool       IsGameOver      => WinnerName is not null;
  public bool      Error     { get; private set; }

  // Lobby / waiting-room state.
  public string     GameStage    { get; internal set; } = string.Empty;
  public int        HostPlayerId { get; internal set; } = 0;
  public IReadOnlyList<PlayerStanding> Roster { get; internal set; } = [];
  public bool       IsHost       => PlayerId.Value != 0 && PlayerId.Value == HostPlayerId;
  public bool       IsInLobby    => GameStage == "WaitingForPlayers";
  
  public List<TrayDie> DiceInPlay { get; private set; } =
  [
    TrayDie.Unselected(index: 1, value: DieValue.One)
  ];
  
  public List<TrayDie> KeptDice => DiceInPlay.Where(d => d.IsSelected).ToList();

  // Derived from DiceInPlay so the UI and the server payload can never disagree.
  // Don't add a parallel field — it leaks across turns and duplicates on re-drop.
  internal IReadOnlyList<int> DiceSetAside =>
    DiceInPlay.Where(d => d.IsSelected).Select(d => (int)d.Value).ToList();

  // Live preview of what the current selection would score (matched trick + points), via the
  // shared ScoreCalculator. Lets the UI show the value before the player commits with Keep.
  public ScoreBreakdown SelectionPreview => ScoreCalculator.Evaluate(DiceSetAside);

  public  string    ErrorMessage     { get; private set; } = string.Empty;
  public  bool      ShowError        { get; private set; }
  public  object    LastErrorMessage { get; private set; } = string.Empty;

  public override void Initialize()
  {
    GameId          = new GameId(0);
    PlayerId        = new PlayerId(0);
    PlayerName      = new PlayerName(string.Empty);
    CurrentPlayerId = 0;
    Scoreboard      = [];
    WinnerName      = null;
    DiceInPlay      = [];
    GameStage       = string.Empty;
    HostPlayerId    = 0;
    Roster          = [];
  }
}

public record PlayerStanding(int PlayerId, string Name, int Score, string Color = "");

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
