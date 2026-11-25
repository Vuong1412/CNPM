import { useState, type FormEvent } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useAuth } from "../context/AuthContext";

export function BootstrapAdminPage() {
  const { t } = useTranslation();
  const { bootstrapAdmin, user } = useAuth();
  const navigate = useNavigate();
  const [email, setEmail] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [password, setPassword] = useState("");
  const [bootstrapToken, setBootstrapToken] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setIsSubmitting(true);

    try {
      await bootstrapAdmin(email, displayName, password, bootstrapToken);
      navigate("/admin", { replace: true });
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t("auth.formError"));
    } finally {
      setIsSubmitting(false);
    }
  }

  if (user?.role === "Admin") {
    return (
      <section className="content-page">
        <p className="eyebrow">{t("auth.adminSetupEyebrow")}</p>
        <h1>{t("auth.adminAlreadySignedIn")}</h1>
        <Link className="button-primary" to="/admin">{t("nav.admin")}</Link>
      </section>
    );
  }

  return (
    <section className="content-page auth-page">
      <div className="auth-card">
        <p className="eyebrow">{t("auth.adminSetupEyebrow")}</p>
        <h1>{t("auth.adminSetupTitle")}</h1>
        <p>{t("auth.adminSetupDescription")}</p>
        <form className="auth-form" onSubmit={(event) => void handleSubmit(event)}>
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
              autoComplete="new-password"
              maxLength={128}
              minLength={12}
              onChange={(event) => setPassword(event.target.value)}
              required
              type="password"
              value={password}
            />
          </label>
          <label>
            {t("auth.bootstrapToken")}
            <input
              autoComplete="off"
              minLength={32}
              onChange={(event) => setBootstrapToken(event.target.value)}
              required
              type="password"
              value={bootstrapToken}
            />
          </label>
          {error && <p className="form-error" role="alert">{error}</p>}
          <button className="button-primary" disabled={isSubmitting} type="submit">
            {isSubmitting ? t("auth.working") : t("auth.createAdmin")}
          </button>
        </form>
        <p className="form-hint">{t("auth.adminSetupHint")}</p>
        <Link className="bootstrap-link" to="/login">{t("nav.login")}</Link>
      </div>
    </section>
  );
}
