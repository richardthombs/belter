import "./style.css";
import "@radix-ui/themes/styles.css";
import { isAuthenticated } from "./network/RestClient";
import { AuthScreen } from "./ui/AuthScreen";
import { app } from "./app";

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
    app().catch((err: unknown) => {
        console.error(err);
        const msg = err instanceof Error ? err.message : "Unknown error";
        showFatalError(container, msg);
    });
} else {
    const screen = new AuthScreen(async () => {
        screen.destroy();
        await app().catch((err: unknown) => {
            console.error(err);
            const msg = err instanceof Error ? err.message : "Unknown error";
            showFatalError(container, msg);
        });
    });
    screen.render(container);
}
