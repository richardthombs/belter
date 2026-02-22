/**
 * Tracks keyboard state for ship flight controls.
 * Uses event.code (not event.key) for layout-independence.
 * Attach on construction, dispose() removes listeners.
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

    /** Returns -1 (left), 0 (neutral), or 1 (right). */
    getThrustX(): number {
        const left  = this.held.has("KeyA")     || this.held.has("ArrowLeft");
        const right = this.held.has("KeyD")     || this.held.has("ArrowRight");
        return right ? 1 : left ? -1 : 0;
    }

    /** Returns -1 (up on screen), 0 (neutral), or 1 (down on screen).
     *  Up = -1 because PixiJS Y increases downward. */
    getThrustY(): number {
        const up   = this.held.has("KeyW") || this.held.has("ArrowUp");
        const down = this.held.has("KeyS") || this.held.has("ArrowDown");
        return down ? 1 : up ? -1 : 0;
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
