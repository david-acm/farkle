namespace Farkle.Contracts;

// Request bodies only. Endpoints whose inputs all come from the route (RollDice, PassTurn, GetGame)
// have no record here. Primitives only — domain types (DieValue, GameId) stay out of the wire format
// so this assembly keeps no dependency on the core; the endpoint maps a request to its Command.
public static class HttpRequests
{
  public record JoinPlayerRequest(int GameId, string PlayerName);

  public record BeginGameRequest(int GameId, int PlayerId);

  public record KeepDiceRequest(int GameId, int PlayerId, IEnumerable<int> DiceValues);

  // #159 — transient keep selection: set a single rolled die aside / put it back.
  public record SetDiceAsideRequest(int GameId, int PlayerId, int DieValue);

  public record ReturnDiceRequest(int GameId, int PlayerId, int DieValue);

  // #277 — POST /api/feedback (anonymous). SessionId is the client-generated feedback stream key
  // (persisted in localStorage); Sentiment is "Up" | "Down". Context (GameId/PlayerId/Stage/Route)
  // is optional — present in-game, absent on landing/lobby. Never carries names/PII.
  public record SubmitFeedbackRequest(
    string  SessionId,
    string  Message,
    string  Sentiment,
    int?    GameId   = null,
    int?    PlayerId = null,
    string? Stage    = null,
    string? Route    = null);
}
