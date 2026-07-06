// Resolves after the browser has committed a paint, so a CSS transition has a distinct
// "from" state to animate from. A single requestAnimationFrame runs *before* the next paint;
// two nested rAFs guarantee at least one frame has actually painted in between. The Die
// component paints its neutral orientation first, then awaits this before rotating to the
// rolled face — without it the rotation can land in the same frame as the neutral paint and
// the roll animation is skipped (an intermittent race, worst on the first roll of a turn).
export function afterNextPaint() {
  return new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)));
}
