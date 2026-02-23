/**
 * Simple toast notification — no animation, inline styles only (avoids Tailwind purge conflicts).
 * z-index 200: above joystick (100), below future modals (≥300).
 */
export function showNotification(message: string): void {
	const el = document.createElement("div");
	el.textContent = message;
	el.setAttribute("role", "status");
	el.setAttribute("aria-live", "polite");
	el.style.position = "fixed";
	el.style.top = "16px";
	el.style.left = "50%";
	el.style.transform = "translateX(-50%)";
	el.style.background = "rgba(0,0,0,0.8)";
	el.style.color = "#fff";
	el.style.padding = "12px 20px";
	el.style.borderRadius = "6px";
	el.style.zIndex = "200";
	el.style.fontSize = "14px";
	document.body.appendChild(el);
	setTimeout(() => el.remove(), 5000);
}
