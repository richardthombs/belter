// Application entry point.
// PixiJS Application.init() is async in v8 — initialise here when ready.
import { Renderer } from './rendering/Renderer';
import { GameHubClient } from './network/GameHubClient';
import { apply } from './state/WorldState';
import { spawn } from './network/RestClient';

export async function app(): Promise<void> {
	console.log('Belter Life initialising...');

	await spawn();

	const canvas = document.createElement('canvas');
	document.getElementById('app')?.appendChild(canvas);

	const renderer = new Renderer();
	await renderer.init(canvas);
	renderer.start();

	const hubClient = new GameHubClient();
	await hubClient.start();
	hubClient.onWorldStateUpdate(update => apply(update));
}
