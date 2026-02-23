import { describe, it, expect, vi, beforeEach } from "vitest";
import { InputManager } from "./InputManager";
import type { GameHubClient } from "../network/GameHubClient";

// ── Mocks ──────────────────────────────────────────────────────────────────

// Prevent TouchInput from touching the DOM
vi.mock("./TouchInput", () => ({
    TouchInput: vi.fn().mockImplementation((cb: (t: number, r: number) => void) => ({
        _cb: cb, // expose for tests that want to invoke the touch callback
        dispose: vi.fn(),
    })),
}));

vi.mock("./KeyboardInput", () => ({
    KeyboardInput: vi.fn().mockImplementation(() => ({
        getThrust: vi.fn().mockReturnValue(0),
        getTorque: vi.fn().mockReturnValue(0),
        dispose: vi.fn(),
    })),
}));

// ── Helpers ────────────────────────────────────────────────────────────────

function makeManager(): {
    manager: InputManager;
    hub: { sendInput: ReturnType<typeof vi.fn> };
    triggerTouch: (thrust: number, torque: number) => void;
    keyboard: { getThrust: ReturnType<typeof vi.fn>; getTorque: ReturnType<typeof vi.fn> };
} {
    const hub = { sendInput: vi.fn() } as unknown as GameHubClient;
    const manager = new InputManager(hub as GameHubClient);
    const touch = (manager as any).touch;
    const keyboard = (manager as any).keyboard;

    return {
        manager,
        hub: hub as any,
        triggerTouch: (t: number, r: number) => touch._cb(t, r),
        keyboard,
    };
}

// ── Tests ──────────────────────────────────────────────────────────────────

describe("InputManager", () => {
    beforeEach(() => {
        vi.clearAllMocks();
    });

    // ── start ────────────────────────────────────────────────────────────────

    it("start() sends initial (0, 0, false) baseline to server", () => {
        const { manager, hub } = makeManager();
        manager.start();
        expect(hub.sendInput).toHaveBeenCalledOnce();
        expect(hub.sendInput).toHaveBeenCalledWith({ thrust: 0, torque: 0, brake: false });
    });

    // ── currentThrust / currentTorque clamping ───────────────────────────────

    it("currentThrust() clamps sum of keyboard + touch to 1", () => {
        const { manager, keyboard } = makeManager();
        keyboard.getThrust.mockReturnValue(1);
        (manager as any).touchThrust = 1; // both pushing forward
        expect((manager as any).currentThrust()).toBe(1);
    });

    it("currentThrust() clamps sum to -1", () => {
        const { manager, keyboard } = makeManager();
        keyboard.getThrust.mockReturnValue(-1);
        (manager as any).touchThrust = -0.8;
        expect((manager as any).currentThrust()).toBe(-1);
    });

    it("currentTorque() clamps sum of inputs to 1", () => {
        const { manager, keyboard } = makeManager();
        keyboard.getTorque.mockReturnValue(0.6);
        (manager as any).touchTorque = 0.8;
        expect((manager as any).currentTorque()).toBe(1);
    });

    it("currentTorque() passes through when sum is within [-1, 1]", () => {
        const { manager, keyboard } = makeManager();
        keyboard.getTorque.mockReturnValue(0);
        (manager as any).touchTorque = 0.5;
        expect((manager as any).currentTorque()).toBe(0.5);
    });

    // ── sendIfChanged via touch callback ──────────────────────────────────────

    it("touch callback triggers sendIfChanged and sends new state", () => {
        const { manager, hub, triggerTouch } = makeManager();
        manager.start();
        hub.sendInput.mockClear();

        triggerTouch(0.8, 0);
        expect(hub.sendInput).toHaveBeenCalledWith({ thrust: 0.8, torque: 0, brake: false });
    });

    it("does NOT send when touch callback reports same state as lastSent", () => {
        const { manager, hub, triggerTouch } = makeManager();
        manager.start();
        triggerTouch(0, 0); // identical to initial lastSent
        hub.sendInput.mockClear();

        triggerTouch(0, 0);
        expect(hub.sendInput).not.toHaveBeenCalled();
    });

    // ── reconcile ─────────────────────────────────────────────────────────────

    it("reconcile() re-sends when server state disagrees with current", () => {
        const { manager, hub, triggerTouch } = makeManager();
        manager.start();
        triggerTouch(1, 0);
        hub.sendInput.mockClear();

        manager.reconcile(0, 0); // server thinks it's 0, client is 1
        expect(hub.sendInput).toHaveBeenCalledWith({ thrust: 1, torque: 0, brake: false });
    });

    it("reconcile() does NOT send when server agrees with current state", () => {
        const { manager, hub } = makeManager();
        manager.start();
        hub.sendInput.mockClear();

        manager.reconcile(0, 0); // server and client both (0, 0)
        expect(hub.sendInput).not.toHaveBeenCalled();
    });

    // ── stop / dispose ────────────────────────────────────────────────────────

    it("stop() calls touch.dispose()", () => {
        const { manager } = makeManager();
        const touchDispose = (manager as any).touch.dispose;
        manager.stop();
        expect(touchDispose).toHaveBeenCalledOnce();
    });

    it("stop() calls keyboard.dispose()", () => {
        const { manager } = makeManager();
        const kbDispose = (manager as any).keyboard.dispose;
        manager.stop();
        expect(kbDispose).toHaveBeenCalledOnce();
    });
});
