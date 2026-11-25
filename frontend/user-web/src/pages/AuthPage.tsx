import { useState, type FormEvent } from "react";
import { Link, Navigate, useLocation, useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useAuth } from "../context/AuthContext";

interface AuthPageProps {
  mode: "login" | "register";
}

export function AuthPage({ mode }: AuthPageProps) {
  const { t } = useTranslation();
  const { user, isLoading, login, register } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [email, setEmail] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [password, setPassword] = useState("");
  const [rememberMe, setRememberMe] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const isRegister = mode === "register";

  if (!isLoading && user) {
    return <Navigate to={user.role === "Admin" ? "/admin" : "/profile"} replace />;
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setIsSubmitting(true);

    try {
      const signedInUser = isRegister
        ? await register(email, displayName, password)
        : await login(email, password, rememberMe);
      const requestedPath = (
        location.state as { from?: { pathname?: string } } | null
      )?.from?.pathname;
      const destination =
        requestedPath ?? (signedInUser.role === "Admin" ? "/admin" : "/profile");
      navigate(destination, { replace: true });
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t("auth.formError"));
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <section className="content-page auth-page">
      <div className="auth-card">
        <p className="eyebrow">{t(isRegister ? "auth.registerEyebrow" : "auth.loginEyebrow")}</p>
        <h1>{t(isRegister ? "auth.registerTitle" : "auth.loginTitle")}</h1>
        <p>{t(isRegister ? "auth.registerDescription" : "auth.loginDescription")}</p>
        <form className="auth-form" onSubmit={(event) => void handleSubmit(event)}>
          {isRegister && (
            <label>
              {t("auth.displayName")}
              <input
                autoComplete="name"
                maxLength={150}
                onChange={(event) => setDisplayName(event.target.value)}
                required
                value={displayName}
              />
            </label>
          )}
          <label>
            {t("auth.email")}
            <input
              autoComplete="email"
              maxLength={320}
              onChange={(event) => setEmail(event.target.value)}
              required
              type="email"
              value={email}
            />
          </label>
          <label>
            {t("auth.password")}
            <input
              autoComplete={isRegister ? "new-password" : "current-password"}
              maxLength={128}
              minLength={isRegister ? 12 : 1}
              onChange={(event) => setPassword(event.target.value)}
              required
              type="password"
              value={password}
            />
          </label>
          {isRegister ? (
            <p className="form-hint">{t("auth.passwordHint")}</p>
          ) : (
            <label className="checkbox-label">
              <input
                checked={rememberMe}
                onChange={(event) => setRememberMe(event.target.checked)}
                type="checkbox"
              />
              {t("auth.rememberMe")}
            </label>
          )}
          {error && <p className="form-error" role="alert">{error}</p>}
          <button className="button-primary" disabled={isSubmitting} type="submit">
            {isSubmitting
              ? t("auth.working")
              : t(isRegister ? "auth.registerAction" : "auth.loginAction")}
          </button>
        </form>
        <p className="auth-switch">
          {t(isRegister ? "auth.haveAccount" : "auth.noAccount")}{" "}
          <Link to={isRegister ? "/login" : "/register"}>
            {t(isRegister ? "nav.login" : "nav.register")}
          </Link>
        </p>
        <Link className="bootstrap-link" to="/bootstrap-admin">
          {t("auth.firstAdmin")}
        </Link>
      </div>
    </section>
  );
}
