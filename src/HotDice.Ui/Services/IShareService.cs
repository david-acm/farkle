namespace HotDice.Ui.Services;

/// <summary>
/// Wraps the browser's Web Share API (with a clipboard fallback) so components
/// can share a join link without talking to JS interop directly.
/// </summary>
public interface IShareService
{
  /// <summary>
  /// Opens the native share sheet for <paramref name="url"/>. Returns
  /// <c>false</c> when the Web Share API isn't available (the caller should
  /// then fall back to copying).
  /// </summary>
  Task<bool> ShareAsync(string title, string text, string url);

  /// <summary>Copies <paramref name="text"/> to the clipboard; returns success.</summary>
  Task<bool> CopyAsync(string text);
}
