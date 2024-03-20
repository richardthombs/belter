import * as PIXI from "pixi.js";
import { Point } from "pixi.js";
import { Game } from "./scenes/game";

const load = (app: PIXI.Application) => {
	return new Promise<void>((resolve) => {
		app.loader.add('assets/hello-world.png').load(() => {
			resolve();
		});
	});
};

const main = async () => {
	// Main app
	let app = new PIXI.Application({ antialias: true });
	let gameScene = new Game(app);

	// Display application properly
	document.body.style.margin = '0';
	app.renderer.view.style.position = 'absolute';
	app.renderer.view.style.display = 'block';

	const fitToContainer = (w: number, h: number) => {
		app.renderer.resize(w, h);
		gameScene.setViewport(w, h);
	};

	fitToContainer(window.innerWidth, window.innerHeight);
	window.addEventListener('resize', (e) => fitToContainer(window.innerWidth, window.innerHeight));

	// Load assets
	await load(app);
	document.body.appendChild(app.view);

	// Set scene
	gameScene.app.stage.addChild(gameScene);
};

main();
