import React, { createContext, useCallback, useContext, useMemo, useState } from 'react';
import { Principal } from '@dfinity/principal';

type LoginStatus = 'idle' | 'logging-in' | 'success' | 'loginError';

type IdentityLike = {
  getPrincipal: () => Principal;
};

type InternetIdentityContextValue = {
  login: () => Promise<void>;
  clear: () => Promise<void>;
  loginStatus: LoginStatus;
  identity: IdentityLike | null;
  isInitializing: boolean;
};

const STORAGE_KEY = 'dungen.local.principal';
const InternetIdentityContext = createContext<InternetIdentityContextValue | undefined>(undefined);

function createIdentity(principalText: string): IdentityLike {
  const principal = Principal.fromText(principalText);
  return {
    getPrincipal: () => principal,
  };
}

export function InternetIdentityProvider({ children }: { children: React.ReactNode }) {
  const [loginStatus, setLoginStatus] = useState<LoginStatus>('idle');
  const [isInitializing] = useState(false);
  const [identity, setIdentity] = useState<IdentityLike | null>(() => {
    const cachedPrincipal = localStorage.getItem(STORAGE_KEY);
    if (!cachedPrincipal) return null;
    try {
      return createIdentity(cachedPrincipal);
    } catch {
      localStorage.removeItem(STORAGE_KEY);
      return null;
    }
  });

  const login = useCallback(async () => {
    setLoginStatus('logging-in');
    try {
      const generatedPrincipal = Principal.anonymous().toText();
      localStorage.setItem(STORAGE_KEY, generatedPrincipal);
      setIdentity(createIdentity(generatedPrincipal));
      setLoginStatus('success');
    } catch {
      setLoginStatus('loginError');
      throw new Error('Login failed');
    }
  }, []);

  const clear = useCallback(async () => {
    localStorage.removeItem(STORAGE_KEY);
    setIdentity(null);
    setLoginStatus('idle');
  }, []);

  const value = useMemo(
    () => ({ login, clear, loginStatus, identity, isInitializing }),
    [login, clear, loginStatus, identity, isInitializing]
  );

  return <InternetIdentityContext.Provider value={value}>{children}</InternetIdentityContext.Provider>;
}

export function useInternetIdentity() {
  const context = useContext(InternetIdentityContext);
  if (!context) {
    throw new Error('useInternetIdentity must be used within an InternetIdentityProvider');
  }
  return context;
}
