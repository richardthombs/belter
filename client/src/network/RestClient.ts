/**
 * REST API client — authentication and future game REST endpoints.
 * JSON fields: camelCase throughout.
 * Error responses: RFC 9457 Problem Details.
 * Timestamps: ISO 8601 UTC.
 */

const TOKEN_KEY = 'belter_jwt';
const BASE = '/api/v1';

export interface ProblemDetails {
    title: string;
    detail?: string;
    status: number;
}

export class AuthError extends Error {
    readonly status: number;
    readonly problem: ProblemDetails;

    constructor(status: number, problem: ProblemDetails) {
        super(problem.detail ?? problem.title);
        this.status = status;
        this.problem = problem;
    }
}

async function throwIfError(res: Response): Promise<void> {
    if (!res.ok) {
        const problem = await res.json() as ProblemDetails;
        throw new AuthError(res.status, problem);
    }
}

/** Register a new player account and store the returned JWT. */
export async function register(username: string, password: string): Promise<void> {
    const res = await fetch(`${BASE}/auth/register`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ username, password }),
    });
    await throwIfError(res);
    const { token } = await res.json() as { token: string };
    localStorage.setItem(TOKEN_KEY, token);
}

/** Login with existing credentials and store the returned JWT. */
export async function login(username: string, password: string): Promise<void> {
    const res = await fetch(`${BASE}/auth/login`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ username, password }),
    });
    await throwIfError(res);
    const { token } = await res.json() as { token: string };
    localStorage.setItem(TOKEN_KEY, token);
}

/** Logout — revoke the current JWT on the server and clear local storage. */
export async function logout(): Promise<void> {
    const token = getToken();
    if (token) {
        const res = await fetch(`${BASE}/auth/logout`, {
            method: 'POST',
            headers: { Authorization: `Bearer ${token}` },
        });
        // 401 means already expired/revoked — treat as successful logout
        if (!res.ok && res.status !== 401) {
            await throwIfError(res);
        }
    }
    localStorage.removeItem(TOKEN_KEY);
}

/** Returns the stored JWT, or null if not authenticated. */
export function getToken(): string | null {
    return localStorage.getItem(TOKEN_KEY);
}

/** Returns true if a token is currently stored (does not validate expiry). */
export function isAuthenticated(): boolean {
    return getToken() !== null;
}
