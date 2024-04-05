using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Greedy.Spa.Components;

public partial class DragabbleDice
{
  [Parameter] public List<DiceValue> DiceValues { get; set; } = new();

  [Parameter] public EventCallback<MudItemDropInfo<DropItem>> OnDropCallback { get; set; }

  private List<DropItem> _items = new();

  private async Task ItemUpdatedAsync(MudItemDropInfo<DropItem> dropItem)
  {
    // TODO: Remove nullable
    dropItem!.Item!.Identifier = dropItem.DropzoneIdentifier;
    await OnDropCallback.InvokeAsync(dropItem);
  }

  protected override void OnInitialized()
  {
    _items = DiceValues.Select((d, i) => new DropItem { Index = i, Value = d, Identifier = "Rolled" })
      .ToList();
  }

  protected override void OnParametersSet()
  {
    var itemsCount = Math.Min(_items.Count, DiceValues.Count);
    for (var i = 0; i < itemsCount; i++)
    {
      _items.ElementAt(i).Value = DiceValues.ElementAt(i);
    }

    base.OnParametersSet();
  }

  public class DropItem
  {
    public int Index { get; set; }

    // TODO: remove nullable
    public DiceValue? Value      { get; set; }
    public string?    Identifier { get; set; }
  }
}
