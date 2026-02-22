import { KeyboardInput } from "./KeyboardInput";
import type { GameHubClient } from "../network/GameHubClient";

/**
 * Polls KeyboardInput on a fixed interval and forwards InputEvents to the server
 * via GameHubClient.sendInput().
 *
 * Sends every poll tick (including zero-thrust) so the shard always has fresh state;
 * a zero-thrust event triggers assisted braking on the server.
 *
 * Story 1.6 will extend this with touch/virtual joystick via the same interface.
 */
export class InputManager {
    private keyboard: KeyboardInput;
    private intervalId: ReturnType<typeof setInterval> | null = null;
    private hubClient: GameHubClient;

    constructor(hubClient: GameHubClient) {
        this.hubClient = hubClient;
        this.keyboard = new KeyboardInput();
    }

    start(intervalMs = 50): void {
        this.intervalId = setInterval(() => {
            this.hubClient.sendInput({
                thrustX: this.keyboard.getThrustX(),
                thrustY: this.keyboard.getThrustY(),
                brake:   false,
            });
        }, intervalMs);
    }

    stop(): void {
        if (this.intervalId !== null) {
            clearInterval(this.intervalId);
            this.intervalId = null;
        }
        this.keyboard.dispose();
    }
}
