import { HubConnectionBuilder, HubConnection } from "@microsoft/signalr";
import { MessagePackHubProtocol } from "@microsoft/signalr-protocol-msgpack";
import { getToken } from "./RestClient";
import type { WorldStateUpdate, InputEvent } from "../types";

/**
 * SignalR + MessagePack hub client.
 * JWT passed as query param: ?access_token=... on WebSocket upgrade.
 * Server→Client messages: PascalCase  (e.g. WorldStateUpdate)
 * Client→Server methods: PascalCase  (e.g. SendInput)
 */
export class GameHubClient {
    private connection: HubConnection;

    constructor() {
        this.connection = new HubConnectionBuilder()
            .withUrl("/hubs/game", {
                accessTokenFactory: () => getToken() ?? "",
            })
            .withHubProtocol(new MessagePackHubProtocol())
            .withAutomaticReconnect()
            .build();
    }

    getConnection(): HubConnection {
        return this.connection;
    }

    async start(): Promise<void> {
        await this.connection.start();
    }

    onWorldStateUpdate(handler: (update: WorldStateUpdate) => void): void {
        this.connection.on("WorldStateUpdate", handler);
    }

    /**
     * Sends player input to the server via the SendInput hub method.
     * ContractlessStandardResolver serialises C# record fields as PascalCase on the wire,
     * so we map to PascalCase keys here before invoking.
     */
    sendInput(input: InputEvent): void {
        this.connection.invoke("SendInput", {
            ThrustX: input.thrustX,
            ThrustY: input.thrustY,
            Brake:   input.brake,
        }).catch(() => { /* swallow — input loss tolerable at 50ms poll rate */ });
    }
}
