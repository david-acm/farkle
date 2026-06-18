namespace Blazor.Dice;

/// <summary>A 3D rotation, in degrees, applied to a die face (X, Y and Z axes).</summary>
public readonly record struct DieRotation(int X, int Y, int Z);
