import { Application } from "pixi.js";
import { GameClient } from "./GameClient";

let app = new Application();


(async () => {
	await app.init({
		antialias: true
	});

	document.body.style.margin = '0';
	document.body.appendChild(app.canvas);

	let gameClient = new GameClient(app);
	app.stage.addChild(gameClient);

	window.addEventListener('resize', (e) => fitToContainer(window.innerWidth, window.innerHeight));
	fitToContainer(window.innerWidth, window.innerHeight);

	return;

	function fitToContainer(w: number, h: number) {
		app.renderer.resize(w, h);
		gameClient.resizeViewport(w, h);
		console.info(`screen ${app.screen.width}x${app.screen.height}`);
	};
	
})();
