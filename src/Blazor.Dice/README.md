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
          OnDieAnimated="OnAnimated" />

@code {
  List<DiceInfo> _dice =
  [
    DiceInfo.Unselected(0, DieValue.One),
    DiceInfo.Selected(1, DieValue.Five),
  ];

  void Toggle(DiceInfo die)
  {
    if (die.IsSelected) die.Deselect();
    else die.Select();
  }

  // OnDieAnimated reports the die that just finished its roll spin, so you clear its
  // one-shot animate flag (the consumer owns the flag; the tray never mutates your data).
  void OnAnimated(DiceInfo die) => die.DisableAnimation();
}
```

`DiceInfo` is constructed via `DiceInfo.Unselected(index, value)` / `DiceInfo.Selected(index, value)`
(animated by default; chain `.DisableAnimation()`), and mutated through `Select()` / `Deselect()`
/ `DisableAnimation()`. `IsSelected` and `IsAnimated` are get-only. The tray ignores taps when
`ReadOnly` is true (e.g. spectators).

## DieValue

`DieValue` is an [Ardalis.SmartEnum](https://github.com/ardalis/SmartEnum): `None`, `One`…`Six`,
with `.Value` (the `int` face), `.Name` (`"One"`…), `.Pip` (the Unicode glyph, e.g. `"⚄"`), and
`DieValue.FromValue(int)`.
