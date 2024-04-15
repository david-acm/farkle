using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Farkle.Spa.Components;

public partial class DragabbleDice
{
  [Inject]
  public ILogger<DragabbleDice> Logger { get; set; } = null!;
  
  [Parameter]
  public List<DraggableDie> DiceInPlay { get; set; } = new();
  
  [Parameter] 
  public EventCallback<List<DraggableDie>> DiceInPlayChanged { get; set; }
  
  private List<DraggableDie> _items = new();
  
  private async Task ItemUpdatedAsync(MudItemDropInfo<DraggableDie> dropItem)
  {
    // TODO: Remove nullable and hardcoded string
    var identifier = dropItem.DropzoneIdentifier;
    dropItem!.Item!.Identifier = identifier;
    Logger.LogInformation("droped item with identifier: {identifier}", identifier);
    await DiceInPlayChanged.InvokeAsync(_items);
    await Task.CompletedTask;
  }
  
  protected override void OnInitialized()
  {
    _items = DiceInPlay.ToList();
    base.OnInitialized();
  }
  
  protected override void OnParametersSet()
  {
    foreach (var die in DiceInPlay)
    {
      Logger.LogInformation("Dragabble dice parameters set");
      var dieItem = _items.FirstOrDefault(i => i.Index == die.Index);
      if (dieItem is null)
      {
        _items.Add(new DraggableDie
        {
          Value      = die.Value,
          Identifier = die.Identifier,
          Index = die.Index
        });
        continue;
      }
      if(dieItem.Value != die.Value)
        dieItem.Value = die.Value;
    }

    base.OnParametersSet();
  }
  
  public class DraggableDie
  {
    public int Index { get; set; }
    
    // TODO: remove nullable
    public DiceValue? Value      { get; set; }
    public string?    Identifier { get; set; }
  }
}
