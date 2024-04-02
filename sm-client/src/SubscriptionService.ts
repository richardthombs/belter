import { HubConnection, HubConnectionState } from "@microsoft/signalr";

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

	constructor(private connection: HubConnection) {
		this.current = ClientSubscription.Blank;

		console.info(`SubscriptionService: Started (${connection.state})`);

		connection.on("Welcome", () => {
			console.info(`SubscriptionService: Received server welcome (${connection.state})`);
		});

		connection.onreconnecting(() => {
			console.info(`SubscriptionService: Reconnecting (${connection.state})`);
		});

		connection.onreconnected(() => {
			if (this.current.equals(ClientSubscription.Blank)) return;

			console.info(`SubscriptionService: Reconnected (${connection.state})`);
			console.info(`SubscriptionService: Re-subscribing after disconnect to (${this.current.rect.x},${this.current.rect.y}) + (${this.current.rect.w}, ${this.current.rect.h}) @ ${this.current.z}`);
			this.subscribe(this.current);
		});
	}

	public subscribeToUpdates(sub: ClientSubscription) {
		if (sub.equals(this.current)) {
			//console.info("Already requested updates for this area");
			return;
		}

		console.info(`SubscriptionService: Subscribing to updates for (${sub.rect.x},${sub.rect.y}) + (${sub.rect.w}, ${sub.rect.h}) @ ${sub.z}`);
		this.subscribe(sub);

	}

	public invalidateSubscription() {
		this.current = ClientSubscription.Blank;
	}

	private subscribe(sub: ClientSubscription) {
		if (this.connection.state != HubConnectionState.Connected) {
			console.info(`SubscriptionService: Unable to send subscription request (${this.connection.state})`);
			return;
		}

		var payload = { x: sub.rect.x, y: sub.rect.y, w: sub.rect.w, h: sub.rect.h, z: sub.z };

		this.connection.send("Subscribe", payload);

		console.info("SubscriptionService: Sent subscription request to server", payload);

		this.current = sub;
	}
}
