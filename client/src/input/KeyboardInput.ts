/**
 * Tracks keyboard state for ship flight controls.
 * Uses event.code (not event.key) for layout-independence.
 *
 * Flight controls:
 *   W / ArrowUp    = main engines (forward thrust)
 *   S / ArrowDown  = retro thrusters (backward thrust)
 *   A / ArrowLeft  = rotate left
 *   D / ArrowRight = rotate right
 */
export class KeyboardInput {
    private held = new Set<string>();
    private onDown: (e: KeyboardEvent) => void;
    private onUp:   (e: KeyboardEvent) => void;

    constructor() {
        this.onDown = (e: KeyboardEvent) => {
            if (controlled(e.code)) e.preventDefault();
            this.held.add(e.code);
        };
        this.onUp = (e: KeyboardEvent) => {
            this.held.delete(e.code);
        };
        window.addEventListener("keydown", this.onDown);
        window.addEventListener("keyup",   this.onUp);
    }

    /** 1 = main engines, -1 = retro thrusters, 0 = off. */
    getThrust(): number {
        const fwd  = this.held.has("KeyW") || this.held.has("ArrowUp");
        const back = this.held.has("KeyS") || this.held.has("ArrowDown");
        return fwd ? 1 : back ? -1 : 0;
    }

    /** 1 = rotate right, -1 = rotate left, 0 = off. */
    getTorque(): number {
        const left  = this.held.has("KeyA") || this.held.has("ArrowLeft");
        const right = this.held.has("KeyD") || this.held.has("ArrowRight");
        return right ? 1 : left ? -1 : 0;
    }

    dispose(): void {
        window.removeEventListener("keydown", this.onDown);
        window.removeEventListener("keyup",   this.onUp);
    }
}

function controlled(code: string): boolean {
    return code === "ArrowUp" || code === "ArrowDown" ||
           code === "ArrowLeft" || code === "ArrowRight";
}
