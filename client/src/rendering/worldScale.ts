export const WORLD_TO_SCREEN = 1 / 1000;

export function toScreen(valueMm: number): number {
	return valueMm * WORLD_TO_SCREEN;
}
