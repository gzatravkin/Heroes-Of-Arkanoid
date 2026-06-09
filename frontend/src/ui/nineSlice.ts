/**
 * nineSlice.ts — shared CSS 9-slice (border-image) helper.
 *
 * Stretching a framed button/panel art with `background-size: 100% 100%`
 * distorts its rounded caps and ornaments. A 9-slice pins the four corners +
 * four edges at fixed size and stretches only the middle, so the frame keeps
 * its shape at any element size. Returns a CSS declaration block to drop inside
 * a rule. Relies on the global `box-sizing: border-box` so the border doesn't
 * change the element's footprint.
 */
export function nineSlice(src: string, slice: string, borderWidth: string): string {
  return `background: none;
      border-style: solid;
      border-width: ${borderWidth};
      border-image: url('${src}') ${slice} fill stretch;`;
}

// Presets — slice insets measured from the source art (top right bottom left).
// InterfaceButton.png 626×162 → caps ≈26px tall, ≈92px wide (matches the menu/overlay buttons).
export const btnInterface = () => nineSlice("/ui/InterfaceButton.png", "26 92 26 92", "8px 30px");
// Button1.png 438×110 → smaller pill.
export const btn1 = () => nineSlice("/ui/Button1.png", "24 60 24 60", "8px 22px");
// MissionName.png 481×136 → label plate.
export const missionName = () => nineSlice("/ui/MissionName.png", "28 70 28 70", "10px 24px");
