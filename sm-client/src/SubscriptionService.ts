import * as signalR from "@microsoft/signalr";

type rect = {
	x: number,
	y: number,
	w: number,
	h: number
};

class Rect implements rect {
	x: number = 0;
	y: number = 0;
	w: number = 0;
	h: number = 0;

	constructor(rect: rect) {
		this.x = rect.x;
		this.y = rect.y;
		this.w = rect.w;
		this.h = rect.h;
	}

	equals(other: Rect): boolean {
		return (
			this.x == other.x &&
			this.y == other.y &&
			this.w == other.w &&
			this.h == other.h
		);
	}
}

type clientSubscription = {
	rect: rect,
	z: number
};

export class ClientSubscription implements clientSubscription {
	rect: Rect = new Rect({ x: 0, y: 0, w: 0, h: 0 });
	z: number = 0;

	constructor(area: clientSubscription) {
		this.rect = new Rect(area.rect);
		this.z = area.z;
	}

	static Blank: ClientSubscription = new ClientSubscription({
		rect: { x: 0, y: 0, w: 0, h: 0 },
		z: 0
	});

	equals(other: ClientSubscription): boolean {
		return (
			this.rect.equals(other.rect) &&
			this.z == other.z
		);
	}
}

export class SubscriptionService {

	private current: ClientSubscription;
	private clientConnected: boolean = false;

	constructor(private connection: signalR.HubConnection) {
		this.current = ClientSubscription.Blank;

		console.info(`SubscriptionService: Started (${connection.state})`);

		connection.on("Welcome", () => {
			console.info(`SubscriptionService: Received server welcome (${connection.state})`);
		});

		connection.onreconnecting(() => {
			console.info(`SubscriptionService: Reconnecting (${connection.state})`);
		});

		connection.onreconnected(() => {
			this.clientConnected = true;
			if (this.current.equals(ClientSubscription.Blank)) return;

			console.info(`SubscriptionService: Reconnected (${connection.state})`);
			console.info(`SubscriptionService: Re-subscribing after disconnect to (${this.current.rect.x},${this.current.rect.y}) + (${this.current.rect.w}, ${this.current.rect.h}) @ ${this.current.z}`);
			this.subscribe(this.current);
		});
	}

	public subscribeToUpdates(sub: ClientSubscription) {
		if (sub.equals(this.current)) return;

		console.info(`SubscriptionService: Subscribing to (${sub.rect.x},${sub.rect.y}) + (${sub.rect.w}, ${sub.rect.h}) @ ${sub.z}`);
		this.subscribe(sub);

		this.current = sub;
	}

	public invalidateSubscription() {
		this.current = ClientSubscription.Blank;
	}

	private subscribe(sub: clientSubscription) {
		if (this.connection.state != signalR.HubConnectionState.Connected) return;

		this.connection.send("Subscribe", { x: sub.rect.x, y: sub.rect.y, w: sub.rect.w, h: sub.rect.h, z: sub.z });
	}
}
