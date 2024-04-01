import { Application, Container, Graphics, Point } from "pixi.js";
import * as signalR from "@microsoft/signalr";
import { SubscriptionService, ClientSubscription } from "./SubscriptionService";
import { GameState, KeyState } from "./GameState";
import { GameEntity } from "./GameEntity";

export class GameClient extends Container {

	app: Application;
	state: GameState = {
		view: { x: 0, y: 0, zoom: 0.1 },
		entities: [],
		keys: {
			thrust: false, rotLeft: false, rotRight: false, fire: false
		},
		clientKeys: {
			panUp: false, panDown: false, panLeft: false, panRight: false, zoomIn: false, zoomOut: false
		}
	};

	prevKeys: KeyState = {
		thrust: false, rotLeft: false, rotRight: false, fire: false
	}

	viewport: {
		width: number,
		height: number,
		offset: { x: number, y: number }
	} = {
			width: 0,
			height: 0,
			offset: { x: 0, y: 0 }
		};

	screenPrev: { w: number, h: number } = { w: 0, h: 0 };

	subscriptionService: SubscriptionService;

	constructor(app: Application) {
		super();
		this.app = app;

		this.resizeViewport(app.screen.width, app.screen.height);

		var handleKeys = (keyCode: string, keyDown: boolean) => {
			switch (keyCode) {
				case "Numpad8": this.state.clientKeys.panUp = keyDown; break;
				case "Numpad2": this.state.clientKeys.panDown = keyDown; break;
				case "Numpad4": this.state.clientKeys.panLeft = keyDown; break;
				case "Numpad6": this.state.clientKeys.panRight = keyDown; break;
				case "NumpadSubtract": this.state.clientKeys.zoomOut = keyDown; break;
				case "NumpadAdd": this.state.clientKeys.zoomIn = keyDown; break;

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
			.withUrl("http://localhost:8080/hub", { accessTokenFactory: () => username })
			.withAutomaticReconnect()
			.build();

		this.subscriptionService = new SubscriptionService(connection);

		// Tell the game we've joined
		connection.start().then(x => {
			console.info("GameClient: Connection started");
			connection.send("ClientConnected", { message: "Hello, World!", user: username });
		});

		connection.onreconnecting(() => {
			console.info("GameClient: Connection lost");
		});

		connection.onreconnected(() => {
			console.info("GameClient: Connection reconnected");
		});

		// Subscribe to position updates
		connection.on("PositionUpdate", (x: GameEntity[]) => {
			this.state.entities = x;
		});

		connection.on("Welcome", (x: { playerIndex: number, connectionId: string, userIdentifier: string }) => {
			console.info(`GameClient: Received Welcome (PlayerIndex: ${x.playerIndex}, ConnectionId: ${x.connectionId}, UserIdentifier: ${x.userIdentifier})`);
		});

		// Ticker for screen refresh
		app.ticker.add(() => this.drawTick());

		// Ticker for local key handling
		app.ticker.add(() => this.keyTick(connection));

		app.ticker.add(() => {
			this.sendSubscription();
		});
	}

	public resizeViewport(width: number, height: number) {
		this.viewport.width = width;
		this.viewport.height = height;
		this.viewport.offset.x = this.viewport.width / 2 - this.state.view.x;
		this.viewport.offset.y = this.viewport.height / 2 - this.state.view.y;
	}

	sendSubscription() {
		let desiredSub = new ClientSubscription({
			rect: {
				x: -this.app.screen.width / 2,
				y: -this.app.screen.height / 2,
				w: this.app.screen.width,
				h: this.app.screen.height
			},
			z: this.state.view.zoom
		});

		this.subscriptionService.subscribeToUpdates(desiredSub);
	}

	keyTick(connection: signalR.HubConnection) {
		const zoomSpeed = 0.01;

		// Viewport adjustment is handling locally
		if (this.state.clientKeys.panUp) this.state.view.y++;
		if (this.state.clientKeys.panDown) this.state.view.y--;
		if (this.state.clientKeys.panLeft) this.state.view.x++;
		if (this.state.clientKeys.panRight) this.state.view.x--;
		if (this.state.clientKeys.zoomIn) this.state.view.zoom *= (1 + zoomSpeed);
		if (this.state.clientKeys.zoomOut) this.state.view.zoom *= (1 - zoomSpeed);

		// Send ship controls to server for processing
		if (connection.state == signalR.HubConnectionState.Connected) {
			if (this.state.keys.fire == this.prevKeys.fire &&
				this.state.keys.rotLeft == this.prevKeys.rotLeft &&
				this.state.keys.rotRight == this.prevKeys.rotRight &&
				this.state.keys.thrust == this.prevKeys.thrust) return;

			connection.send("KeyState", {
				thrust: this.state.keys.thrust,
				rotLeft: this.state.keys.rotLeft,
				rotRight: this.state.keys.rotRight,
				fire: this.state.keys.fire
			});

			this.prevKeys = {
				thrust: this.state.keys.thrust,
				rotLeft: this.state.keys.rotLeft,
				rotRight: this.state.keys.rotRight,
				fire: this.state.keys.fire
			};
		}
	}

	drawTick() {
		this.position.set(this.viewport.offset.x, this.viewport.offset.y);
		this.scale.set(this.state.view.zoom, this.state.view.zoom);

		if (!this.state.entities[0]) return;

		for (let i = 0; i < this.children.length; i++) {
			this.children[i].renderable = false;
		}

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
					let sides = 12;
					for (let angle = 0; angle < Math.PI * 2; angle += (Math.PI * 2 / sides) + (Math.random() * Math.PI * 2) / 10) {
						let r = radius + Math.floor(Math.random() * (radius * 0.5));
						let x = Math.cos(angle) * r;
						let y = Math.sin(angle) * r;
						points.push(x);
						points.push(y);
					}

					thing.drawPolygon(points);
					thing.endFill();

					thing.pivot = new Point(0, 0);
				}
			}

			thing.position.set(entity.x, entity.y);
			thing.angle = entity.r;
			thing.renderable = true;
		}
	}
}
