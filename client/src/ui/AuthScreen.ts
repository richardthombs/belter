import { login, register, AuthError } from "../network/RestClient";

export class AuthScreen {
    private el: HTMLDivElement | null = null;
    private readonly onSuccess: () => Promise<void>;

    constructor(onSuccess: () => Promise<void>) {
        this.onSuccess = onSuccess;
    }

    render(container: HTMLElement): void {
        // Full-screen overlay
        const overlay = document.createElement("div");
        overlay.className =
            "fixed inset-0 flex items-center justify-center bg-[#0a0a1a]";

        // Card
        const card = document.createElement("div");
        card.className =
            "w-full max-w-sm bg-zinc-900 border border-zinc-700 rounded-xl p-8 shadow-2xl";

        // Title
        const title = document.createElement("h1");
        title.className = "text-white text-2xl font-bold mb-6 text-center";
        title.textContent = "Belter Life";

        // Tab bar
        const tabBar = document.createElement("div");
        tabBar.className = "flex mb-6 border-b border-zinc-700";

        const loginTab = document.createElement("button");
        loginTab.textContent = "Login";
        loginTab.className =
            "flex-1 pb-2 text-sm font-medium border-b-2 border-indigo-400 text-white";

        const registerTab = document.createElement("button");
        registerTab.textContent = "Register";
        registerTab.className =
            "flex-1 pb-2 text-sm font-medium text-zinc-400 hover:text-zinc-200 border-b-2 border-transparent";

        tabBar.appendChild(loginTab);
        tabBar.appendChild(registerTab);

        // Form container
        const formContainer = document.createElement("div");

        // Build login form
        const loginForm = this.buildForm("Login", "current-password");
        // Build register form
        const registerForm = this.buildForm("Register", "new-password");
        registerForm.className += " hidden";

        formContainer.appendChild(loginForm);
        formContainer.appendChild(registerForm);

        // Tab switching
        loginTab.addEventListener("click", () => {
            loginTab.className =
                "flex-1 pb-2 text-sm font-medium border-b-2 border-indigo-400 text-white";
            registerTab.className =
                "flex-1 pb-2 text-sm font-medium text-zinc-400 hover:text-zinc-200 border-b-2 border-transparent";
            loginForm.classList.remove("hidden");
            registerForm.classList.add("hidden");
        });

        registerTab.addEventListener("click", () => {
            registerTab.className =
                "flex-1 pb-2 text-sm font-medium border-b-2 border-indigo-400 text-white";
            loginTab.className =
                "flex-1 pb-2 text-sm font-medium text-zinc-400 hover:text-zinc-200 border-b-2 border-transparent";
            registerForm.classList.remove("hidden");
            loginForm.classList.add("hidden");
        });

        // Wire form submissions
        loginForm.addEventListener("submit", async (e) => {
            e.preventDefault();
            const { username, password, submitBtn, errorEl } =
                this.getFormFields(loginForm);
            submitBtn.disabled = true;
            errorEl.classList.add("hidden");
            try {
                await login(username, password);
                await this.onSuccess();
            } catch (err) {
                if (err instanceof AuthError) {
                    errorEl.textContent =
                        err.problem.detail ?? err.problem.title;
                    errorEl.classList.remove("hidden");
                }
                submitBtn.disabled = false;
            }
        });

        registerForm.addEventListener("submit", async (e) => {
            e.preventDefault();
            const { username, password, submitBtn, errorEl } =
                this.getFormFields(registerForm);
            submitBtn.disabled = true;
            errorEl.classList.add("hidden");
            try {
                await register(username, password);
                await this.onSuccess();
            } catch (err) {
                if (err instanceof AuthError) {
                    errorEl.textContent =
                        err.problem.detail ?? err.problem.title;
                    errorEl.classList.remove("hidden");
                }
                submitBtn.disabled = false;
            }
        });

        card.appendChild(title);
        card.appendChild(tabBar);
        card.appendChild(formContainer);
        overlay.appendChild(card);
        container.appendChild(overlay);

        this.el = overlay;
    }

    destroy(): void {
        this.el?.remove();
        this.el = null;
    }

    private buildForm(
        submitLabel: string,
        passwordAutocomplete: AutoFill,
    ): HTMLFormElement {
        const form = document.createElement("form");

        const inputClasses =
            "w-full bg-zinc-800 border border-zinc-600 rounded px-3 py-2 text-white placeholder-zinc-500 focus:outline-none focus:border-zinc-400";

        // Username field
        const usernameLabel = document.createElement("label");
        usernameLabel.className = "block mb-3";
        const usernameInput = document.createElement("input");
        usernameInput.type = "text";
        usernameInput.placeholder = "Username";
        usernameInput.autocomplete = "username";
        usernameInput.className = inputClasses;
        usernameInput.dataset.field = "username";
        usernameLabel.appendChild(usernameInput);

        // Password field
        const passwordLabel = document.createElement("label");
        passwordLabel.className = "block mb-4";
        const passwordInput = document.createElement("input");
        passwordInput.type = "password";
        passwordInput.placeholder = "Password";
        passwordInput.autocomplete = passwordAutocomplete;
        passwordInput.className = inputClasses;
        passwordInput.dataset.field = "password";
        passwordLabel.appendChild(passwordInput);

        // Submit button
        const submitBtn = document.createElement("button");
        submitBtn.type = "submit";
        submitBtn.textContent = submitLabel;
        submitBtn.className =
            "w-full bg-indigo-600 hover:bg-indigo-500 text-white rounded px-4 py-2 font-medium disabled:opacity-50 disabled:cursor-not-allowed transition-colors";

        // Error display
        const errorEl = document.createElement("p");
        errorEl.className = "text-red-400 text-sm mt-3 hidden";

        form.appendChild(usernameLabel);
        form.appendChild(passwordLabel);
        form.appendChild(submitBtn);
        form.appendChild(errorEl);

        return form;
    }

    private getFormFields(form: HTMLFormElement): {
        username: string;
        password: string;
        submitBtn: HTMLButtonElement;
        errorEl: HTMLParagraphElement;
    } {
        const usernameInput = form.querySelector<HTMLInputElement>(
            'input[data-field="username"]',
        )!;
        const passwordInput = form.querySelector<HTMLInputElement>(
            'input[data-field="password"]',
        )!;
        const submitBtn = form.querySelector<HTMLButtonElement>(
            "button[type='submit']",
        )!;
        const errorEl = form.querySelector<HTMLParagraphElement>("p")!;

        return {
            username: usernameInput.value,
            password: passwordInput.value,
            submitBtn,
            errorEl,
        };
    }
}
