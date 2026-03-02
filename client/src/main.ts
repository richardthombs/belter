import "./style.css";
import "@radix-ui/themes/styles.css";
import { isAuthenticated, SpawnTimeoutError } from "./network/RestClient";
import { AuthScreen } from "./ui/AuthScreen";
import { app } from "./app";

function showStartupLoading(container: HTMLElement): void {
    container.innerHTML = `
        <div class="fixed inset-0 flex items-center justify-center bg-[#0a0a1a]">
            <div class="text-center">
                <h1 class="text-white text-xl font-semibold mb-2">Connecting…</h1>
                <p class="text-zinc-400 text-sm">Contacting gateway server</p>
            </div>
        </div>`;
}

function showFatalError(container: HTMLElement, message: string): void {
    container.innerHTML = `
        <div class="fixed inset-0 flex items-center justify-center bg-[#0a0a1a]">
            <div class="w-full max-w-sm bg-zinc-900 border border-red-800 rounded-xl p-8 shadow-2xl text-center">
                <h1 class="text-white text-xl font-bold mb-3">Unable to connect</h1>
                <p class="text-red-400 text-sm mb-6">${message}</p>
                <button onclick="location.reload()"
                    class="bg-zinc-700 hover:bg-zinc-600 text-white rounded px-4 py-2 text-sm transition-colors">
                    Retry
                </button>
            </div>
        </div>`;
}

const container = document.getElementById("app")!;
if (isAuthenticated()) {
    showStartupLoading(container);
    app().catch((err: unknown) => {
        console.error(err);
        const msg =
            err instanceof SpawnTimeoutError
                ? "Gateway spawn request timed out after 5 seconds. Check the gateway server and retry."
                : err instanceof Error
                  ? err.message
                  : "Unknown error";
        showFatalError(container, msg);
    });
} else {
    const screen = new AuthScreen(async () => {
        screen.destroy();
        showStartupLoading(container);
        await app().catch((err: unknown) => {
            console.error(err);
            const msg =
                err instanceof SpawnTimeoutError
                    ? "Gateway spawn request timed out after 5 seconds. Check the gateway server and retry."
                    : err instanceof Error
                      ? err.message
                      : "Unknown error";
            showFatalError(container, msg);
        });
    });
    screen.render(container);
}
