import * as PIXI from "pixi.js";
import { GameClient } from "./GameClient";

const main = async () => {
	console.log("Hello, World!");
	// Main app
	let app = new PIXI.Application({ antialias: true });
	let gameClient = new GameClient(app);

	// Display application properly
	document.body.style.margin = '0';
	app.renderer.view.style.position = 'absolute';
	app.renderer.view.style.display = 'block';

	const fitToContainer = (w: number, h: number) => {
		app.renderer.resize(w, h);
		gameClient.resizeViewport(w, h);
		console.info(`screen ${app.screen.width}x${app.screen.height}`);
	};

	fitToContainer(window.innerWidth, window.innerHeight);
	window.addEventListener('resize', (e) => fitToContainer(window.innerWidth, window.innerHeight));

	// Load assets
	document.body.appendChild(app.view);

	// Set scene
	gameClient.app.stage.addChild(gameClient);
};

main();
