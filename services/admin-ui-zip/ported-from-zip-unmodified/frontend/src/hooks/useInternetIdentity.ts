import type { ReactNode } from "react";

/**
 * Stub hook for Internet Identity in offline/dev mode.
 * Returns a shape compatible with what components expect.
 */

export type LoginStatus =
  | "idle"
  | "logging-in"
  | "success"
  | "loginError"
  | "error";

export interface IdentityLike {
  getPrincipal: () => { toString: () => string };
}

export interface UseInternetIdentityReturn {
  login: () => Promise<void>;
  clear: () => Promise<void>;
  loginStatus: LoginStatus;
  identity: IdentityLike | null;
  isInitializing: boolean;
}

export function InternetIdentityProvider({ children }: { children: ReactNode }) {
  return children;
}

export function useInternetIdentity(): UseInternetIdentityReturn {
  return {
    login: async () => {
      console.info("[InternetIdentity] Login not available in offline mode.");
    },
    clear: async () => {
      console.info("[InternetIdentity] Clear not available in offline mode.");
    },
    loginStatus: "idle",
    identity: null,
    isInitializing: false,
  };
}
