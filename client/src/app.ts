// Application entry point.
// PixiJS Application.init() is async in v8 — initialise here when ready.
import { Renderer } from "./rendering/Renderer";
import { GameHubClient } from "./network/GameHubClient";
import { InputManager } from "./input/InputManager";
import { apply, getAsteroids, getSectorId, getShips } from "./state/WorldState";
import { spawn, AuthError, logout } from "./network/RestClient";
import { showNotification } from "./ui/Notification";
import { HudBottomBar } from "./ui/HudBottomBar";
import { ContextualPanel } from "./ui/ContextualPanel";
import {
	clearSelection,
	getSelectionViewState,
	selectObject,
	updateSelectionFromWorld,
} from "./state/ObjectSelectionState";

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
	const appRoot = document.getElementById("app");
	if (appRoot) {
		appRoot.innerHTML = "";
		appRoot.appendChild(canvas);
	}

	const renderer = new Renderer();
	await renderer.init(canvas);
	renderer.setLocalShipId(spawnResponse.shipId);
	renderer.initCameraAt(spawnResponse.spawnX, spawnResponse.spawnY);

	if (spawnResponse.repositioned) {
		showNotification("Your ship was repositioned — the belt moved while you were away");
	}

	renderer.start();

	const hudBottomBar = new HudBottomBar(spawnResponse.shipId);
	hudBottomBar.mount(document.body);
	const contextualPanel = new ContextualPanel(() => {
		clearSelection();
		contextualPanel.render(getSelectionViewState());
	});
	contextualPanel.mount(document.body);
	renderer.setOnAsteroidSelected((asteroidId) => {
		selectObject("asteroid", asteroidId);
		contextualPanel.render(getSelectionViewState());
	});

	const hubClient = new GameHubClient();
	await hubClient.start();
	const input = new InputManager(hubClient);
	input.start();

	hubClient.onWorldStateUpdate((update) => {
		apply(update);
		const ships = getShips();
		hudBottomBar.update(ships, getSectorId());
		updateSelectionFromWorld(ships, getAsteroids(), spawnResponse.shipId);
		contextualPanel.render(getSelectionViewState());
		// Reconcile ~once/s when server includes input state in snapshot.
		const ownShip = ships.find(
			(s) => s.shipId === spawnResponse.shipId,
		);
		if (ownShip && ownShip.thrust != null && ownShip.torque != null) {
			input.reconcile(ownShip.thrust, ownShip.torque);
		}
	});

	window.addEventListener(
		"beforeunload",
		() => {
			input.stop();
			hudBottomBar.unmount();
			contextualPanel.unmount();
		},
		{ once: true },
	);
}
