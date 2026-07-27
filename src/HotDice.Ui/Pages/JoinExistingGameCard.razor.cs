using Microsoft.AspNetCore.Components;

namespace HotDice.Ui.Pages;

public partial class JoinExistingGameCard : ComponentBase
{
  [Inject]
  public NavigationManager Nav { get; set; } = null!;

  private string _joinName = string.Empty;
  private int    _joinId;

  private void JoinExistingGame()
  {
    if (string.IsNullOrWhiteSpace(_joinName) || _joinId <= 0) return;
    Nav.NavigateTo($"/games/{_joinId}?name={Uri.EscapeDataString(_joinName)}");
  }
}
