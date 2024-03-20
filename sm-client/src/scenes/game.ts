import { Application, Circle, Container, Graphics, Point } from "pixi.js";
import * as signalR from "@microsoft/signalr";

type GameEntity = { id: string, x: number, y: number, r: number, type: string, radius: number };

type GameState = {
	view: { x: number, y: number, zoom: number },
	entities: GameEntity[],
	playerEntityId: string,
	keys: {
		panUp: boolean,
		panDown: boolean,
		panLeft: boolean,
		panRight: boolean,
		zoomIn: boolean,
		zoomOut: boolean,
		thrust: boolean,
		rotLeft: boolean,
		rotRight: boolean,
		fire: boolean
	}
}

export class Game extends Container {
	app: Application;
	state: GameState = {
		view: { x: 0, y: 0, zoom: 1 },
		entities: [],
		playerEntityId: "",
		keys: {
			panUp: false, panDown: false, panLeft: false, panRight: false, zoomIn: false, zoomOut: false,
			thrust: false, rotLeft: false, rotRight: false, fire: false
		}
	};

	viewport: {
		width: number,
		height: number,
		offset: { x: number, y: number }
	} = {
			width: 0,
			height: 0,
			offset: { x: 0, y: 0 }
		};

	public centerViewport() {
		this.viewport.offset.x = this.viewport.width / 2 - this.state.view.x;
		this.viewport.offset.y = this.viewport.height / 2 - this.state.view.y;
	}

	public setViewport(width: number, height: number) {
		this.viewport.width = width;
		this.viewport.height = height;
		this.centerViewport();
	}

	constructor(app: Application) {
		super();
		this.app = app;

		this.setViewport(app.screen.width, app.screen.height);

		// Ticker for screen refresh
		app.ticker.add(() => this.drawTick());

		var handleKeys = (keyCode: string, keyDown: boolean) => {
			switch (keyCode) {
				case "Numpad8": this.state.keys.panUp = keyDown; break;
				case "Numpad2": this.state.keys.panDown = keyDown; break;
				case "Numpad4": this.state.keys.panLeft = keyDown; break;
				case "Numpad6": this.state.keys.panRight = keyDown; break;
				case "NumpadSubtract": this.state.keys.zoomOut = keyDown; break;
				case "NumpadAdd": this.state.keys.zoomIn = keyDown; break;

				case "KeyW": this.state.keys.thrust = keyDown; break;
				case "KeyA": this.state.keys.rotLeft = keyDown; break;
				case "KeyD": this.state.keys.rotRight = keyDown; break;
				case "Space": this.state.keys.fire = keyDown; break;
				default: console.log(keyCode);
			}
		};

		document.addEventListener("keydown", (e: KeyboardEvent) => handleKeys(e.code, true));
		document.addEventListener("keyup", (e: KeyboardEvent) => handleKeys(e.code, false));

		// Auth, lol
		let queryString = new URLSearchParams(window.location.search);
		let username = queryString.get("user") || "Anonymous";

		// Create a connection to the game hub
		let connection = new signalR.HubConnectionBuilder()
			.withUrl("http://localhost:5291/hub", { accessTokenFactory: () => username })
			.withAutomaticReconnect()
			.build();

		// Tell the game we've joined
		connection.start().then(x => connection.send("ClientConnected", { message: "Hello, World!", user: username }));

		// Subscribe to position updates
		connection.on("PositionUpdate", (x: GameEntity[]) => {
			this.state.entities = x;
		});

		connection.on("Welcome", (x: string) => {
			this.state.playerEntityId = x;
		});

		// Ticker for local key handling
		app.ticker.add(() => {
			const zoomSpeed = 0.01;

			// Viewport adjustment is handling locally
			if (this.state.keys.panUp) this.state.view.y++;
			if (this.state.keys.panDown) this.state.view.y--;
			if (this.state.keys.panLeft) this.state.view.x++;
			if (this.state.keys.panRight) this.state.view.x--;
			if (this.state.keys.zoomIn) this.state.view.zoom *= (1 + zoomSpeed);
			if (this.state.keys.zoomOut) this.state.view.zoom *= (1 - zoomSpeed);

			// Send ship controls to game for processing
			if (connection.state == signalR.HubConnectionState.Connected) {
				connection.send("KeyState", {
					thrust: this.state.keys.thrust,
					rotLeft: this.state.keys.rotLeft,
					rotRight: this.state.keys.rotRight,
					fire: this.state.keys.fire
				});
			}
		});
	}

	drawTick() {
		//this.centerViewport();
		this.position.set(this.viewport.offset.x, this.viewport.offset.y);
		this.scale.set(this.state.view.zoom, this.state.view.zoom);

		if (!this.state.entities[0]) return;

		for (let i = 0; i < this.state.entities.length; i++) {
			const entity = this.state.entities[i];
			let thing = this.getChildByName(entity.id) as Graphics;
			if (!thing) {
				thing = new Graphics();
				thing.name = entity.id;
				this.addChild(thing);

				if (entity.type == "p") {
					thing.lineStyle(2, 0x0081c6);
					thing.beginFill(0x0081c6);
					thing.moveTo(0, 20);
					thing.lineTo(20, -25);
					thing.lineTo(0, -15);
					thing.lineTo(-20, -25);
					thing.closePath();
					thing.endFill();
					thing.pivot = new Point(0, -2.5);
				}
				else {
					thing.lineStyle(2, 0xffffff, 1);
					thing.beginFill(0xffffff, 0.8);

					let points = [];
					let radius = entity.radius;
					let sides = 50;
					for (let angle = 0; angle < Math.PI * 2; angle += (Math.PI * 2 / sides) + (Math.random() * Math.PI * 2) / 10) {
						let r = radius + Math.floor(Math.random() * (radius * 0.5));
						let x = Math.cos(angle) * r;
						let y = Math.sin(angle) * r;
						points.push(x);
						points.push(y);
					}

					thing.drawPolygon(points);
					thing.endFill();

					thing.pivot = new Point(0, -2.5);
				}
			}

			thing.position.set(entity.x, entity.y);
			thing.angle = entity.r;
		}
	}
}
