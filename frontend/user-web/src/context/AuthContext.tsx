import { createContext, useCallback, useContext, useEffect, useMemo, useState } from "react";
import type { ReactNode } from "react";
import { ApiError, type AuthUser } from "@qol/shared";
import { apiClient } from "../services/api";

interface AuthContextValue {
  user: AuthUser | null;
  isLoading: boolean;
  error: string | null;
  login: (email: string, password: string, rememberMe: boolean) => Promise<AuthUser>;
  register: (email: string, displayName: string, password: string) => Promise<AuthUser>;
  bootstrapAdmin: (
    email: string,
    displayName: string,
    password: string,
    bootstrapToken: string
  ) => Promise<AuthUser>;
  updateProfile: (displayName: string) => Promise<void>;
  changePassword: (currentPassword: string, newPassword: string) => Promise<void>;
  logout: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | null>(null);

interface AuthProviderProps {
  children: ReactNode;
}

export function AuthProvider({ children }: AuthProviderProps) {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    try {
      const currentUser = await apiClient.get<AuthUser>("auth/me");
      setUser(currentUser);
      setError(null);
    } catch (cause) {
      if (cause instanceof ApiError && cause.status === 401) {
        setUser(null);
        setError(null);
      } else {
        setError(cause instanceof Error ? cause.message : "Could not load the account.");
      }
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  const login = useCallback(
    async (email: string, password: string, rememberMe: boolean) => {
      const signedInUser = await apiClient.post<AuthUser>("auth/login", {
        email,
        password,
        rememberMe
      });
      setUser(signedInUser);
      setError(null);
      return signedInUser;
    },
    []
  );

  const register = useCallback(async (email: string, displayName: string, password: string) => {
    const signedInUser = await apiClient.post<AuthUser>("auth/register", {
      email,
      displayName,
      password
    });
    setUser(signedInUser);
    setError(null);
    return signedInUser;
  }, []);

  const bootstrapAdmin = useCallback(
    async (
      email: string,
      displayName: string,
      password: string,
      bootstrapToken: string
    ) => {
      const signedInUser = await apiClient.post<AuthUser>("auth/bootstrap-admin", {
        email,
        displayName,
        password,
        bootstrapToken
      });
      setUser(signedInUser);
      setError(null);
      return signedInUser;
    },
    []
  );

  const updateProfile = useCallback(async (displayName: string) => {
    const updatedUser = await apiClient.patch<AuthUser>("auth/me", { displayName });
    setUser(updatedUser);
  }, []);

  const changePassword = useCallback(async (currentPassword: string, newPassword: string) => {
    await apiClient.post<void>("auth/me/password", { currentPassword, newPassword });
  }, []);

  const logout = useCallback(async () => {
    try {
      await apiClient.post<void>("auth/logout", {});
    } catch (cause) {
      if (cause instanceof ApiError && cause.status === 401) {
        setUser(null);
        return;
      }
      throw cause;
    }
    setUser(null);
  }, []);

  const value = useMemo(
    () => ({
      user,
      isLoading,
      error,
      login,
      register,
      bootstrapAdmin,
      updateProfile,
      changePassword,
      logout
    }),
    [user, isLoading, error, login, register, bootstrapAdmin, updateProfile, changePassword, logout]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth must be used within AuthProvider.");
  }
  return context;
}
