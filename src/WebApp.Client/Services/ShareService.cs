using Microsoft.JSInterop;

namespace WebApp.Client.Services;

/// <summary>
/// <see cref="IShareService"/> backed by the <c>window.farkleShare</c> helper
/// (see <c>WebApp/Components/App.razor</c>), which wraps the Web Share API and
/// the clipboard. Failures are swallowed — sharing is a convenience.
/// </summary>
public sealed class ShareService(IJSRuntime js) : IShareService
{
  public async Task<bool> ShareAsync(string title, string text, string url)
  {
    try
    {
      return await js.InvokeAsync<bool>("farkleShare.share", title, text, url);
    }
    catch (JSException)
    {
      return false;
    }
  }

  public async Task<bool> CopyAsync(string text)
  {
    try
    {
      return await js.InvokeAsync<bool>("farkleShare.copy", text);
    }
    catch (JSException)
    {
      return false;
    }
  }
}
