// Application entry point.
// PixiJS Application.init() is async in v8 — initialise here when ready.
import { Renderer } from "./rendering/Renderer";
import { GameHubClient } from "./network/GameHubClient";
import { InputManager } from "./input/InputManager";
import { apply } from "./state/WorldState";
import { spawn, AuthError, logout } from "./network/RestClient";

export async function app(): Promise<void> {
	console.log("Belter Life initialising...");

	let spawnResponse: Awaited<ReturnType<typeof spawn>>;
	try {
		spawnResponse = await spawn();
	} catch (err) {
		if (err instanceof AuthError && err.status === 401) {
			await logout();
			window.location.reload();
			return;
		}
		throw err;
	}

	const canvas = document.createElement("canvas");
	document.getElementById("app")?.appendChild(canvas);

	const renderer = new Renderer();
	await renderer.init(canvas);
	renderer.setLocalShipId(spawnResponse.shipId);
	renderer.start();

	const hubClient = new GameHubClient();
	await hubClient.start();
	hubClient.onWorldStateUpdate((update) => apply(update));

	const input = new InputManager(hubClient);
	input.start();
}
