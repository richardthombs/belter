import { Container, Graphics } from "pixi.js";

type BackgroundTreatment = "starfield" | "grid";

interface BackgroundLayerOptions {
	developmentMode?: boolean;
	treatmentOverride?: BackgroundTreatment;
	reducedMotionOverride?: boolean;
}

interface BackgroundUpdateInput {
	cameraX: number;
	cameraY: number;
	viewportWidth: number;
	viewportHeight: number;
	deltaMs: number;
}

interface BackgroundDebugState {
	treatment: BackgroundTreatment;
	reducedMotion: boolean;
	starCount: number;
	starHashes: number[];
	gridLineCount: number;
	gridSubLineCount: number;
	motionOffset: number;
}

const MILLIMETERS_PER_PIXEL = 1000;
const STAR_TILE_SIZE_MM = 1_000_000;
const GRID_SPACING_MM = 2_500_000;
const STARS_PER_TILE = 18;
const GRID_TICK_STEP_MM = GRID_SPACING_MM / 10;

function snapLineCoordinate(value: number): number {
	return Math.round(value) + 0.5;
}

function deterministicHash(x: number, y: number, salt: number): number {
	let seed = x * 374761393 + y * 668265263 + salt * 69069;
	seed = (seed ^ (seed >>> 13)) * 1274126177;
	seed ^= seed >>> 16;
	return seed >>> 0;
}

function random01(hash: number): number {
	return (hash & 0xffffff) / 0xffffff;
}

function resolveStarVisuals(hashClass: number, hashRadius: number, hashAlpha: number): { radius: number; alpha: number } {
	const rarityRoll = random01(hashClass);

	// Weighted tiers: many faint stars, fewer medium, very few bright.
	if (rarityRoll < 0.02) {
		return {
			radius: 1.35 + random01(hashRadius) * 0.8,
			alpha: 0.24 + random01(hashAlpha) * 0.14,
		};
	}

	if (rarityRoll < 0.16) {
		return {
			radius: 0.9 + random01(hashRadius) * 0.7,
			alpha: 0.14 + random01(hashAlpha) * 0.1,
		};
	}

	return {
		radius: 0.5 + random01(hashRadius) * 0.7,
		alpha: 0.07 + random01(hashAlpha) * 0.07,
	};
}

function parseTreatment(search: string): BackgroundTreatment | null {
	const params = new URLSearchParams(search);
	const value = params.get("bgRef");
	if (value === "stars") {
		return "starfield";
	}
	if (value === "grid" || value === "starfield") {
		return value;
	}
	return null;
}

function prefersReducedMotion(): boolean {
	if (typeof window === "undefined" || typeof window.matchMedia !== "function") {
		return false;
	}
	return window.matchMedia("(prefers-reduced-motion: reduce)").matches;
}

export class BackgroundLayer extends Container {
	private readonly visuals: Graphics;
	private readonly developmentMode: boolean;
	private readonly reducedMotion: boolean;
	private readonly selectedTreatment: BackgroundTreatment;
	private motionPhase = 0;
	private debugState: BackgroundDebugState = {
		treatment: "starfield",
		reducedMotion: false,
		starCount: 0,
		starHashes: [],
		gridLineCount: 0,
		gridSubLineCount: 0,
		motionOffset: 0,
	};

	constructor(options?: BackgroundLayerOptions) {
		super();
		this.developmentMode = options?.developmentMode ?? import.meta.env.DEV;
		this.reducedMotion = options?.reducedMotionOverride ?? prefersReducedMotion();
		this.selectedTreatment = this.resolveTreatment(options?.treatmentOverride);

		this.eventMode = "none";
		this.interactiveChildren = false;

		this.visuals = new Graphics();
		this.addChild(this.visuals);

		this.debugState = {
			treatment: this.selectedTreatment,
			reducedMotion: this.reducedMotion,
			starCount: 0,
			starHashes: [],
			gridLineCount: 0,
			gridSubLineCount: 0,
			motionOffset: 0,
		};
	}

	update(input: BackgroundUpdateInput): void {
		if (!this.reducedMotion) {
			this.motionPhase += input.deltaMs * 0.0005;
		}

		const motionOffset = this.reducedMotion ? 0 : Math.sin(this.motionPhase) * 0.75;

		if (this.selectedTreatment === "grid") {
			const gridMotionOffset = 0;
			const gridRenderStats = this.renderGrid(input, gridMotionOffset);
			this.debugState = {
				treatment: "grid",
				reducedMotion: this.reducedMotion,
				starCount: 0,
				starHashes: [],
				gridLineCount: gridRenderStats.lineCount,
				gridSubLineCount: gridRenderStats.subLineCount,
				motionOffset: gridMotionOffset,
			};
			return;
		}

		const starHashes = this.renderStarfield(input, motionOffset);
		this.debugState = {
			treatment: "starfield",
			reducedMotion: this.reducedMotion,
			starCount: starHashes.length,
			starHashes,
			gridLineCount: 0,
			gridSubLineCount: 0,
			motionOffset,
		};
	}

	getDebugState(): BackgroundDebugState {
		return {
			...this.debugState,
			starHashes: [...this.debugState.starHashes],
		};
	}

	private resolveTreatment(override?: BackgroundTreatment): BackgroundTreatment {
		if (override) {
			return override;
		}

		if (this.developmentMode && typeof window !== "undefined") {
			const querySelection = parseTreatment(window.location.search);
			if (querySelection) {
				return querySelection;
			}
		}

		return "grid";
	}

	private renderStarfield(input: BackgroundUpdateInput, motionOffset: number): number[] {
		this.visuals.clear();

		const halfWidthMm = (input.viewportWidth / 2) * MILLIMETERS_PER_PIXEL;
		const halfHeightMm = (input.viewportHeight / 2) * MILLIMETERS_PER_PIXEL;
		const parallax = 0.18;
		const generationHalfWidthMm = halfWidthMm / parallax;
		const generationHalfHeightMm = halfHeightMm / parallax;

		const minWorldX = input.cameraX - generationHalfWidthMm;
		const maxWorldX = input.cameraX + generationHalfWidthMm;
		const minWorldY = input.cameraY - generationHalfHeightMm;
		const maxWorldY = input.cameraY + generationHalfHeightMm;

		const minTileX = Math.floor(minWorldX / STAR_TILE_SIZE_MM) - 1;
		const maxTileX = Math.floor(maxWorldX / STAR_TILE_SIZE_MM) + 1;
		const minTileY = Math.floor(minWorldY / STAR_TILE_SIZE_MM) - 1;
		const maxTileY = Math.floor(maxWorldY / STAR_TILE_SIZE_MM) + 1;

		const starHashes: number[] = [];

		for (let tileX = minTileX; tileX <= maxTileX; tileX += 1) {
			for (let tileY = minTileY; tileY <= maxTileY; tileY += 1) {
				for (let starIndex = 0; starIndex < STARS_PER_TILE; starIndex += 1) {
					const baseSalt = starIndex + 1;
					const hashClass = deterministicHash(tileX, tileY, baseSalt * 13 + 2);
					const hashX = deterministicHash(tileX, tileY, baseSalt * 17 + 3);
					const hashY = deterministicHash(tileX, tileY, baseSalt * 37 + 11);
					const hashRadius = deterministicHash(tileX, tileY, baseSalt * 53 + 19);
					const hashAlpha = deterministicHash(tileX, tileY, baseSalt * 71 + 29);

					starHashes.push(hashX ^ hashY);

					const starXWorld =
						tileX * STAR_TILE_SIZE_MM + random01(hashX) * STAR_TILE_SIZE_MM;
					const starYWorld =
						tileY * STAR_TILE_SIZE_MM + random01(hashY) * STAR_TILE_SIZE_MM;

					const screenX =
						(((starXWorld - input.cameraX) * parallax) / MILLIMETERS_PER_PIXEL) +
						input.viewportWidth / 2 +
						motionOffset;
					const screenY =
						(((starYWorld - input.cameraY) * parallax) / MILLIMETERS_PER_PIXEL) +
						input.viewportHeight / 2 +
						motionOffset * 0.5;

					if (
						screenX < -2 ||
						screenX > input.viewportWidth + 2 ||
						screenY < -2 ||
						screenY > input.viewportHeight + 2
					) {
						continue;
					}

					const starVisuals = resolveStarVisuals(hashClass, hashRadius, hashAlpha);

					this.visuals
						.circle(screenX, screenY, starVisuals.radius)
						.fill({ color: 0xd9e4ff, alpha: starVisuals.alpha });
				}
			}
		}

		starHashes.sort((left, right) => left - right);
		return starHashes;
	}

	private renderGrid(
		input: BackgroundUpdateInput,
		motionOffset: number,
	): { lineCount: number; subLineCount: number } {
		this.visuals.clear();

		const halfWidthMm = (input.viewportWidth / 2) * MILLIMETERS_PER_PIXEL;
		const halfHeightMm = (input.viewportHeight / 2) * MILLIMETERS_PER_PIXEL;
		const parallax = 1;
		const generationHalfWidthMm = halfWidthMm / parallax;
		const generationHalfHeightMm = halfHeightMm / parallax;

		const minWorldX = input.cameraX - generationHalfWidthMm;
		const maxWorldX = input.cameraX + generationHalfWidthMm;
		const minWorldY = input.cameraY - generationHalfHeightMm;
		const maxWorldY = input.cameraY + generationHalfHeightMm;

		const firstGridX = Math.floor(minWorldX / GRID_SPACING_MM) * GRID_SPACING_MM;
		const firstGridY = Math.floor(minWorldY / GRID_SPACING_MM) * GRID_SPACING_MM;
		let lineCount = 0;
		let subLineCount = 0;
		const firstSubGridX = Math.floor(minWorldX / GRID_TICK_STEP_MM) * GRID_TICK_STEP_MM;
		const firstSubGridY = Math.floor(minWorldY / GRID_TICK_STEP_MM) * GRID_TICK_STEP_MM;

		for (let x = firstSubGridX; x <= maxWorldX + GRID_TICK_STEP_MM; x += GRID_TICK_STEP_MM) {
			const subStepIndex = Math.round(x / GRID_TICK_STEP_MM);
			const isMajorLine = Math.abs(subStepIndex % 10) === 0;
			if (isMajorLine) {
				continue;
			}

			const rawScreenX =
				(((x - input.cameraX) * parallax) / MILLIMETERS_PER_PIXEL) +
				input.viewportWidth / 2 +
				motionOffset;
			const screenX = snapLineCoordinate(rawScreenX);

			this.visuals
				.moveTo(screenX, 0)
				.lineTo(screenX, input.viewportHeight)
				.stroke({ color: 0x33e6ff, width: 1, alpha: 0.09 });
			subLineCount += 1;
		}

		for (let y = firstSubGridY; y <= maxWorldY + GRID_TICK_STEP_MM; y += GRID_TICK_STEP_MM) {
			const subStepIndex = Math.round(y / GRID_TICK_STEP_MM);
			const isMajorLine = Math.abs(subStepIndex % 10) === 0;
			if (isMajorLine) {
				continue;
			}

			const rawScreenY =
				(((y - input.cameraY) * parallax) / MILLIMETERS_PER_PIXEL) +
				input.viewportHeight / 2 +
				motionOffset;
			const screenY = snapLineCoordinate(rawScreenY);

			this.visuals
				.moveTo(0, screenY)
				.lineTo(input.viewportWidth, screenY)
				.stroke({ color: 0x33e6ff, width: 1, alpha: 0.09 });
			subLineCount += 1;
		}

		for (let x = firstGridX; x <= maxWorldX + GRID_SPACING_MM; x += GRID_SPACING_MM) {
			const gridIndex = Math.round(x / GRID_SPACING_MM);
			const isMajor = Math.abs(gridIndex % 4) === 0;
			const rawScreenX =
				(((x - input.cameraX) * parallax) / MILLIMETERS_PER_PIXEL) +
				input.viewportWidth / 2 +
				motionOffset;
			const screenX = snapLineCoordinate(rawScreenX);

			this.visuals
				.moveTo(screenX, 0)
				.lineTo(screenX, input.viewportHeight)
				.stroke({ color: 0x33e6ff, width: 1, alpha: isMajor ? 0.24 : 0.14 });
			lineCount += 1;
		}

		for (let y = firstGridY; y <= maxWorldY + GRID_SPACING_MM; y += GRID_SPACING_MM) {
			const gridIndex = Math.round(y / GRID_SPACING_MM);
			const isMajor = Math.abs(gridIndex % 4) === 0;
			const rawScreenY =
				(((y - input.cameraY) * parallax) / MILLIMETERS_PER_PIXEL) +
				input.viewportHeight / 2 +
				motionOffset;
			const screenY = snapLineCoordinate(rawScreenY);

			this.visuals
				.moveTo(0, screenY)
				.lineTo(input.viewportWidth, screenY)
				.stroke({ color: 0x33e6ff, width: 1, alpha: isMajor ? 0.24 : 0.14 });
			lineCount += 1;
		}

		return { lineCount, subLineCount };
	}
}
