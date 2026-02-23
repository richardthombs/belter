import { KeyboardInput } from "./KeyboardInput";
import { TouchInput } from "./TouchInput";
import type { GameHubClient } from "../network/GameHubClient";

/**
 * Event-driven input manager — sends to the server only when key state changes.
 *
 * Reliability: the server broadcasts its view of each ship's input ~once/s.
 * reconcile() compares that against current keyboard state and re-sends if they
 * diverge (guards against lost key-released messages).
 */
export class InputManager {
    private keyboard: KeyboardInput;
    private touch: TouchInput;
    private hubClient: GameHubClient;
    private lastSent = { thrust: 0, torque: 0 };
    private touchThrust = 0;
    private touchTorque = 0;
    private onKeyEvent: () => void;

    constructor(hubClient: GameHubClient) {
        this.hubClient = hubClient;
        this.keyboard = new KeyboardInput();
        this.touch = new TouchInput((thrust, torque) => {
            this.touchThrust = thrust;
            this.touchTorque = torque;
            this.sendIfChanged();
        });

        // Fire after KeyboardInput's own handlers (registered first) update `held`.
        this.onKeyEvent = () => this.sendIfChanged();
        window.addEventListener("keydown", this.onKeyEvent);
        window.addEventListener("keyup", this.onKeyEvent);
    }

    start(): void {
        // Send initial state so the server has a baseline.
        this.sendNow();
    }

    /**
     * Called when the server's reconciliation tick includes its view of our input.
     * Re-sends current input state if it disagrees with the server.
     */
    reconcile(serverThrust: number, serverTorque: number): void {
        const thrust = this.currentThrust();
        const torque = this.currentTorque();
        if (serverThrust !== thrust || serverTorque !== torque) {
            this.sendNow();
        }
    }

    private currentThrust(): number {
        return Math.max(
            -1,
            Math.min(1, this.keyboard.getThrust() + this.touchThrust),
        );
    }

    private currentTorque(): number {
        return Math.max(
            -1,
            Math.min(1, this.keyboard.getTorque() + this.touchTorque),
        );
    }

    private sendIfChanged(): void {
        const thrust = this.currentThrust();
        const torque = this.currentTorque();
        if (
            thrust !== this.lastSent.thrust ||
            torque !== this.lastSent.torque
        ) {
            this.sendNow();
        }
    }

    private sendNow(): void {
        const thrust = this.currentThrust();
        const torque = this.currentTorque();
        this.lastSent = { thrust, torque };
        this.hubClient.sendInput({ thrust, torque, brake: false });
    }

    stop(): void {
        window.removeEventListener("keydown", this.onKeyEvent);
        window.removeEventListener("keyup", this.onKeyEvent);
        this.keyboard.dispose();
        this.touch.dispose();
    }
}
