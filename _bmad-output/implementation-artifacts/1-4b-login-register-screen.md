# Story 1.4b: Login & Register Screen

Status: complete

## Story

As a **visitor to the Belter Life URL**,
I want to see a login/register screen when I have no active session,
so that I can authenticate before entering the game world.

## Acceptance Criteria

1. **Given** there is no JWT in localStorage when the app loads,
   **When** `main.ts` initialises,
   **Then** the auth screen is rendered into `#app`; the PixiJS canvas and SignalR connection are NOT started

2. **Given** the auth screen is visible,
   **When** it renders,
   **Then** there are two tabs — **Login** and **Register** — each with username + password fields and a submit button; Login tab is active by default

3. **Given** valid credentials on the Login tab,
   **When** submitted,
   **Then** `RestClient.login()` is called; on success the auth screen is removed and `app()` is called (spawn → canvas → hub)

4. **Given** valid new credentials on the Register tab,
   **When** submitted,
   **Then** `RestClient.register()` is called; on success the auth screen is removed and `app()` is called

5. **Given** a login or register API call fails (AuthError thrown),
   **When** the error is caught,
   **Then** an inline error message is displayed below the form; the submit button is re-enabled; the page does NOT reload

6. **Given** a valid JWT already in localStorage when the app loads,
   **When** `main.ts` initialises,
   **Then** the auth screen is skipped entirely and `app()` is called directly

7. **Given** the auth screen,
   **When** it renders,
   **Then** it is styled with a dark space background (`#0a0a1a`) and a centred card using Tailwind CSS utility classes consistent with the game's visual aesthetic

8. **Given** `app()` calls `spawn()` with a JWT that has expired,
   **When** the 401 AuthError is thrown,
   **Then** the token is cleared via `RestClient.logout()` and the page reloads (forcing re-authentication)

## Tasks / Subtasks

- [x] Task 1 — Set up Tailwind CSS v4 with Vite (AC: 7)
  - [x] `cd client && npm install --save-dev @tailwindcss/vite`
  - [x] Update `client/vite.config.ts`: import `tailwindcss` from `@tailwindcss/vite` and add to `plugins: [tailwindcss()]`
  - [x] Create `client/src/style.css` with a single line: `@import "tailwindcss";`
  - [x] In `client/src/main.ts`: add `import "./style.css";` as the very first import
  - [x] Update `client/index.html`: change `<title>client</title>` → `<title>Belter Life</title>`; add `class="dark"` to `<html>` tag

- [x] Task 2 — Import Radix UI Themes CSS for design tokens (AC: 7)
  - [x] In `client/src/main.ts`: add `import "@radix-ui/themes/styles.css";` (after `style.css` import)
  - [x] **Note:** `@radix-ui/themes` requires React for its component API — do NOT import any component from it. The CSS import provides colour tokens, radius variables, and a baseline reset as CSS custom properties only. The `@radix-ui/themes` package is in `devDependencies` so should be moved to `dependencies` to ensure it is available at runtime:
    - `cd client && npm install @radix-ui/themes` (re-installs as a runtime dependency)
  - [x] **Important:** `@radix-ui/themes` in devDependencies was likely added speculatively — moving it to `dependencies` is correct

- [x] Task 3 — Create `client/src/ui/AuthScreen.ts` (AC: 1, 2, 3, 4, 5, 7)
  - [ ] Export class `AuthScreen`:
    ```typescript
    export class AuthScreen {
        private el: HTMLDivElement | null = null;
        constructor(private readonly onSuccess: () => Promise<void>) {}
        render(container: HTMLElement): void { ... }
        destroy(): void { this.el?.remove(); this.el = null; }
    }
    ```
  - [ ] `render(container)`:
    - Creates a full-screen overlay div with classes: `fixed inset-0 flex items-center justify-center bg-[#0a0a1a]`
    - Inside: a card div with classes: `w-full max-w-sm bg-zinc-900 border border-zinc-700 rounded-xl p-8 shadow-2xl`
    - Card contains: game title `<h1>` (`text-white text-2xl font-bold mb-6 text-center`), tab bar, and form container
    - Appends overlay to `container`; stores as `this.el`
  - [ ] Tab bar: two `<button>` elements (Login / Register) — active tab has `border-b-2 border-indigo-400 text-white`; inactive has `text-zinc-400 hover:text-zinc-200`; clicking updates active tab and swaps form visibility
  - [ ] Login form and Register form are separate `<div>` elements; inactive one has `class="hidden"`
  - [ ] Each form contains:
    - `<label>` + `<input type="text" placeholder="Username" autocomplete="username">` — classes: `w-full bg-zinc-800 border border-zinc-600 rounded px-3 py-2 text-white placeholder-zinc-500 focus:outline-none focus:border-zinc-400 mb-3`
    - `<label>` + `<input type="password" placeholder="Password" autocomplete="current-password">` — same classes + `mb-4`
    - `<button type="submit">` — classes: `w-full bg-indigo-600 hover:bg-indigo-500 text-white rounded px-4 py-2 font-medium disabled:opacity-50 disabled:cursor-not-allowed transition-colors`
    - `<p>` error display — classes: `text-red-400 text-sm mt-3 hidden`
  - [ ] On form submit (add `submit` event listener on each form):
    1. `event.preventDefault()`
    2. Disable submit button; clear error
    3. Read username + password values from inputs
    4. Call `await login(username, password)` or `await register(username, password)` (import from `RestClient`)
    5. On success: call `await this.onSuccess()` (do NOT call `destroy()` — let caller manage this)
    6. On `AuthError` catch: show `problem.detail ?? problem.title` in the error `<p>` (remove `hidden`); re-enable submit button

- [x] Task 4 — Update `client/src/main.ts` to gate on authentication (AC: 1, 6)
  - [x] Import `{ isAuthenticated }` from `./network/RestClient`
  - [x] Import `{ AuthScreen }` from `./ui/AuthScreen`
  - [x] Import `{ app }` from `./app`
  - [x] Replace the current single-line `app().catch(console.error)` with:
    ```typescript
    const container = document.getElementById("app")!;
    if (isAuthenticated()) {
        app().catch(console.error);
    } else {
        const screen = new AuthScreen(async () => {
            screen.destroy();
            await app();
        });
        screen.render(container);
    }
    ```

- [x] Task 5 — Update `client/src/app.ts` to handle 401 mid-session (AC: 8)
  - [ ] Wrap the `await spawn()` call in a try/catch:
    ```typescript
    try {
        await spawn();
    } catch (err) {
        if (err instanceof AuthError && err.status === 401) {
            await logout();
            window.location.reload();
            return;
        }
        throw err;
    }
    ```
  - [ ] Import `{ AuthError, logout }` from `./network/RestClient`

- [x] Task 6 — End-to-end verification (AC: 1–8)
  - [ ] `cd client && npm run build` → 0 TypeScript errors, 0 Vite warnings related to missing plugins
  - [ ] Start docker-compose stack; navigate to `http://localhost:5173` → auth screen appears
  - [ ] Register a new user → game world loads
  - [ ] Refresh page → game world loads directly (token in localStorage)
  - [ ] Clear localStorage → auth screen appears again

## Dev Notes

### Tailwind CSS v4 — Breaking Change from v3

Tailwind v4 fundamentally changes configuration:
- **No `tailwind.config.js`** — content detection is automatic via Vite plugin
- **No PostCSS config** — use `@tailwindcss/vite` Vite plugin instead
- **CSS entry point** — your `style.css` contains `@import "tailwindcss"` (not `@tailwind base/components/utilities`)
- **Theme extension** — done in CSS via `@theme { --color-brand: ... }` not in JS config

```typescript
// client/vite.config.ts — final shape
import { defineConfig } from "vite";
import tailwindcss from "@tailwindcss/vite";

export default defineConfig({
    plugins: [tailwindcss()],
    server: {
        proxy: {
            "/api": "http://localhost:5080",
            "/hubs": {
                target: "http://localhost:5080",
                ws: true,
                changeOrigin: true,
            },
        },
    },
});
```

### @radix-ui/themes — CSS-only Usage

`@radix-ui/themes` is a React component library. In this vanilla TypeScript project, only the CSS is imported. The CSS provides:
- CSS custom properties: `--color-background`, `--gray-1` through `--gray-12`, `--accent-9`, radius tokens, shadow tokens etc.
- A CSS reset suitable for the app

**Do not import** any JS/TS exports from `@radix-ui/themes` — they require React.

After `npm install @radix-ui/themes` (as a runtime dep), the import path for CSS is:
```typescript
import "@radix-ui/themes/styles.css";
```

### Two-Layer UI Architecture

The existing client has two rendering layers, and this story introduces the HTML UI layer properly:

| Layer | Technology | Used for |
|---|---|---|
| Game canvas | PixiJS v8 | Ship, asteroids, effects, in-game HUD (future) |
| HTML overlay | DOM + Tailwind + Radix CSS tokens | Auth screen (this story), panels, marketplace, catalogue |

The HTML overlay layer attaches to `#app`. After auth succeeds, the canvas is also appended to `#app` by `app()`. Both coexist — the canvas fills the viewport, overlays are `fixed` positioned on top.

### main.ts Import Order

Order matters for CSS:
```typescript
import "./style.css";                    // 1. Tailwind base
import "@radix-ui/themes/styles.css";    // 2. Radix design tokens
import { isAuthenticated, ... } from "./network/RestClient";
import { AuthScreen } from "./ui/AuthScreen";
import { app } from "./app";
```

### Why This Story Is Required Before 1.5

Story 1.5 tests ship physics from the browser. Without a login screen, the dev must manually insert JWTs into localStorage during every test session, which is fragile. This story makes local dev runnable end-to-end.

### What sprint-status.yaml Story 1-4b Is

This is an unplanned story discovered during Story 1.4 delivery — the client auth UI was never scoped into the epic definition. It is a prerequisite for 1.5 (Ship Flight: Assisted Newtonian Physics) and all subsequent client-side stories.

### No Server Changes

This story is **client-only**. The backend API (`/api/v1/auth/register`, `/api/v1/auth/login`, `/api/v1/auth/logout`) was fully implemented in Story 1.2. No changes to any `.cs`, `.csproj`, or migration files.

### RestClient Functions Already Exist

`RestClient.ts` already exports `register()`, `login()`, `logout()`, `getToken()`, `isAuthenticated()` — all fully implemented in Story 1.2. This story wires them to a UI; do NOT rewrite them.

### References

- `RestClient.login/register/logout/isAuthenticated`: [client/src/network/RestClient.ts](client/src/network/RestClient.ts)
- `app()` entry point: [client/src/app.ts](client/src/app.ts)
- `main.ts` current state (one-liner): [client/src/main.ts](client/src/main.ts)
- `index.html` current state: [client/index.html](client/index.html)
- Existing UI stubs (pattern reference): [client/src/ui/ContextualPanel.ts](client/src/ui/ContextualPanel.ts)
- Two-layer UI architecture: [Source: architecture.md#Frontend Architecture → UI layers]
- Tailwind v4 Vite integration: https://tailwindcss.com/docs/installation/vite
- `@radix-ui/themes` CSS tokens: https://www.radix-ui.com/themes/docs/theme/color

## Dev Agent Record

### Agent Model Used

Claude Sonnet 4.6 (GitHub Copilot — bmad-agent-bmm-dev mode)

### Debug Log References

### Completion Notes List

- Tailwind CSS v4 configured via `@tailwindcss/vite` plugin (no `tailwind.config.js`, no PostCSS)
- `@radix-ui/themes` moved from devDependencies → dependencies; only CSS imported, no React components
- `AuthScreen` uses field assignment instead of constructor parameter property (required by `erasableSyntaxOnly` TS option)
- `buildForm()` `passwordAutocomplete` typed as `AutoFill` to satisfy strict DOM types
- Build: `tsc && vite build` → 0 TS errors, 0 new Vite warnings (pre-existing SignalR `/*#__PURE__*/` warnings from node_modules are unrelated)

### File List

- `client/package.json` (modified — @tailwindcss/vite added to devDependencies; @radix-ui/themes moved to dependencies)
- `client/vite.config.ts` (modified — tailwindcss plugin added)
- `client/src/style.css` (created)
- `client/src/main.ts` (modified — CSS imports, auth gate)
- `client/src/app.ts` (modified — 401 mid-session handling)
- `client/src/ui/AuthScreen.ts` (created)
- `client/index.html` (modified — title, dark class)
