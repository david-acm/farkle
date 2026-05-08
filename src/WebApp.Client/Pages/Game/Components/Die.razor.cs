using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using WebApp.Client.Services;

namespace WebApp.Client.Pages.Game.Components;

public partial class Die
{
  private string          _id     = new(Guid.NewGuid().ToString().Where(c => !char.IsDigit(c)).ToArray());
  private DieValue       _number = DieValue.None;
  private (int, int, int) _rotation;
  private double          _scale = 1;
  
  [Parameter] public DieValue DieValue { get; set; } = null!;
  
  [Parameter] public int Size { get; set; } = 50;
  
  [Parameter] public string? Class { get; set; }
  
  [Parameter] public bool IsDragging { get; set; }
  
  [Inject] public ILogger<Die> Logger { get; set; } = null!;
  
  [Inject] public IRotationCalculator RotationCalculator { get; set; } = null!;
  
  private double AngleFor(char a) => a switch
  {
    'x' => _rotation.Item1,
    'y' => _rotation.Item2,
    'z' => _rotation.Item3,
    _   => 0
  };
  
  protected override async Task OnInitializedAsync()
  {
    if (IsDragging)
      RotateToValue();
    await base.OnInitializedAsync();
  }
  
  protected override Task OnParametersSetAsync()
  {
    if (_number != DieValue.None && _number != DieValue) RotateToValue();
    
    return base.OnParametersSetAsync();
  }
  
  protected override async Task OnAfterRenderAsync(bool firstRender)
  {
    if (firstRender)  DelayedRotateToValueAsync();
    // RotateToValue();
    await base.OnAfterRenderAsync(firstRender);
  }
  
  private void DelayedRotateToValueAsync()
  {
      _ = new Timer(_ =>
      {
        RotateToValue();
        InvokeAsync(StateHasChanged);
      }, null, 0, -1);
  }
  
  private void RotateToValue()
  {
    _number = DieValue;
    var rotation = RotationCalculator.CalculateFor(_number, IsDragging);
    SetRotationTo(rotation);
    Logger.LogDebug("Rotating to: {x}, {y}, {z}", _rotation.Item1, _rotation.Item2, _rotation.Item3);
  }
  
  private void SetRotationTo((int, int, int) rotation) =>
    _rotation = rotation;
  
  private void MouseLeave(MouseEventArgs e)
  {
    (var x, var y, var z) = _rotation;
    SetRotationTo((x, y - 10, z - 10));
    Scale(1);
    StateHasChanged();
  }
  
  private void MouseEnter(MouseEventArgs e)
  {
    (var x, var y, var z) = _rotation;
    SetRotationTo((x, y + 10, z + 10));
    Scale(1.4);
    StateHasChanged();
  }
  
  private void Scale(double scale) =>
    _scale = scale;
}
