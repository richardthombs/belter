import { GameEntity } from "./GameEntity";

export type GameState = {
	view: { x: number; y: number; zoom: number; };
	entities: GameEntity[];
	keys: KeyState;
	clientKeys: ClientKeyState
};

export type KeyState = {
	thrust: boolean;
	rotLeft: boolean;
	rotRight: boolean;
	fire: boolean;
};

export type ClientKeyState = {
	panUp: boolean;
	panDown: boolean;
	panLeft: boolean;
	panRight: boolean;
	zoomIn: boolean;
	zoomOut: boolean;
}