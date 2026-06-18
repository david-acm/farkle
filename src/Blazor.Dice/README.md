# Blazor.Dice

3D CSS dice components for Blazor — a `Die`, a presentational tap-to-select `DiceTray`, and a
`DieValue` smart enum. No application/state coupling: the tray takes data and raises callbacks,
so you own the dice list and decide what a tap means.

## Install

```sh
dotnet add package Blazor.Dice
```

Register the services (provides `IRotationCalculator`):

```csharp
builder.Services.AddBlazorDice();
```

## A single die

```razor
@using Blazor.Dice

<Die DieValue="DieValue.Five" Size="64" Animate="true" />
```

`Die` parameters: `DieValue` (required), `Size` (pixels, default 50), `Selected`, `Animate`,
`Class`, `AnimationDurationMs`, and the `OnAnimated` callback (raised once a roll spin finishes).

## The tap-to-select tray

```razor
@using Blazor.Dice

<DiceTray Dice="_dice"
          ReadOnly="false"
          OnToggle="Toggle"
          OnDieAnimated="ClearAnimations" />

@code {
  List<TrayDie> _dice =
  [
    TrayDie.Unselected(0, DieValue.One),
    TrayDie.Selected(1, DieValue.Five),
  ];

  void Toggle(TrayDie die)
  {
    if (die.IsSelected) die.Deselect();
    else die.Select();
  }

  void ClearAnimations()
  {
    foreach (var die in _dice) die.DisableAnimation();
  }
}
```

`TrayDie` is constructed via `TrayDie.Unselected(index, value)` / `TrayDie.Selected(index, value)`
(animated by default; chain `.DisableAnimation()`), and mutated through `Select()` / `Deselect()`
/ `DisableAnimation()`. `IsSelected` and `IsAnimated` are get-only. The tray ignores taps when
`ReadOnly` is true (e.g. spectators).

## DieValue

`DieValue` is an [Ardalis.SmartEnum](https://github.com/ardalis/SmartEnum): `None`, `One`…`Six`,
with `.Value` (the `int` face), `.Name` (`"One"`…), `.Pip` (the Unicode glyph, e.g. `"⚄"`), and
`DieValue.FromValue(int)`.
