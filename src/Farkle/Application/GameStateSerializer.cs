using System.Collections.Immutable;
using System.Text.Json;
using Farkle.Domain.GameAggregate;

namespace Farkle.Application;

// #156 — serializes the internal GameState to/from JSON for the GameView read model.
// GameState (and Player/Score/DieValue/GameId) are internal value objects with non-trivial
// shapes (SmartEnum, Eventuous Id, immutable collections), so instead of fighting
// System.Text.Json over internal accessibility we map to a flat primitive DTO and rebuild
// through the GameState.FromSnapshot seam. The DTO IS the persisted schema — evolve it
// additively (new optional members) and reproject if the fold logic changes.
internal static class GameStateSerializer
{
  private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

  public static string Serialize(GameState state) =>
    JsonSerializer.Serialize(ToDto(state), Options);

  public static GameState Deserialize(string json) =>
    FromDto(JsonSerializer.Deserialize<GameStateDto>(json, Options)
            ?? throw new InvalidOperationException("GameView snapshot JSON deserialized to null"));

  private static GameStateDto ToDto(GameState s) =>
    new(
      Id:                    s.Id.Id,
      Stage:                 (int)s.GameStage,
      Winner:                s.Winner is null ? null : new PlayerDto(s.Winner.Id, s.Winner.Name),
      TurnScore:             s.TurnScore.Value,
      Players:               s.Players.Select(p => new PlayerDto(p.Id, p.Name)).ToArray(),
      TableCenter:           s.TableCenter.Select(d => d.Value).ToArray(),
      DiceKept:              s.DiceKept.Select(d => d.Value).ToArray(),
      DiceSetAside:          s.DiceSetAside.Select(d => d.Value).ToArray(),
      StraightsKeptThisTurn: s.StraightsKeptThisTurn,
      ScoreTable:            s.ScoreTable.ToDictionary(kv => kv.Key, kv => kv.Value));

  private static GameState FromDto(GameStateDto d) =>
    GameState.FromSnapshot(
      id:                    new GameId(d.Id ?? 0),
      gameStage:             (GameStage)d.Stage,
      // Colour is derived from the id (PlayerColors), so it survives a snapshot round-trip
      // without bloating the persisted DTO with a redundant, id-derivable field.
      winner:                d.Winner is null ? null : new Player(d.Winner.Id, d.Winner.Name, PlayerColors.For(d.Winner.Id)),
      turnScore:             new Score(d.TurnScore),
      players:               d.Players.Select(p => new Player(p.Id, p.Name, PlayerColors.For(p.Id))).ToImmutableArray(),
      tableCenter:           d.TableCenter.Select(DieValue.FromValue).ToImmutableArray(),
      diceKept:              d.DiceKept.Select(DieValue.FromValue).ToImmutableArray(),
      diceSetAside:          d.DiceSetAside.Select(DieValue.FromValue).ToImmutableArray(),
      straightsKeptThisTurn: d.StraightsKeptThisTurn,
      scoreTable:            d.ScoreTable.ToImmutableDictionary());

  private sealed record GameStateDto(
    int?                Id,
    int                 Stage,
    PlayerDto?          Winner,
    int                 TurnScore,
    PlayerDto[]         Players,
    int[]               TableCenter,
    int[]               DiceKept,
    int[]               DiceSetAside,
    int                 StraightsKeptThisTurn,
    Dictionary<int,int> ScoreTable);

  private sealed record PlayerDto(int Id, string Name);
}
