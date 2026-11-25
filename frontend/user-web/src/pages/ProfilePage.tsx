import { useEffect, useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useAuth } from "../context/AuthContext";

export function ProfilePage() {
  const { t } = useTranslation();
  const { user, isLoading, updateProfile, changePassword } = useAuth();
  const [displayName, setDisplayName] = useState(user?.displayName ?? "");
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  useEffect(() => {
    setDisplayName(user?.displayName ?? "");
  }, [user?.displayName]);

  if (isLoading) {
    return <section className="content-page"><p role="status">{t("common.loading")}</p></section>;
  }

  if (!user) {
    return (
      <section className="content-page">
        <p className="eyebrow">{t("profile.eyebrow")}</p>
        <h1>{t("profile.title")}</h1>
        <p>{t("profile.description")}</p>
        <div className="profile-actions">
          <Link className="button-primary" to="/login">{t("nav.login")}</Link>
          <Link className="button-secondary" to="/register">{t("nav.register")}</Link>
        </div>
      </section>
    );
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setMessage(null);
    setIsSaving(true);
    try {
      await updateProfile(displayName);
      setMessage(t("profile.saved"));
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t("profile.saveError"));
    } finally {
      setIsSaving(false);
    }
  }

  async function handlePasswordSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setMessage(null);

    if (newPassword !== confirmPassword) {
      setError(t("profile.passwordMismatch"));
      return;
    }

    setIsSaving(true);
    try {
      await changePassword(currentPassword, newPassword);
      setCurrentPassword("");
      setNewPassword("");
      setConfirmPassword("");
      setMessage(t("profile.passwordChanged"));
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t("profile.passwordError"));
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <section className="content-page profile-page">
      <p className="eyebrow">{t("profile.eyebrow")}</p>
      <h1>{t("profile.title")}</h1>
      <p>{t("profile.description")}</p>
      <div className="profile-card">
        <div className="profile-avatar" aria-hidden="true">
          {user.displayName.slice(0, 1).toLocaleUpperCase()}
        </div>
        <div>
          <strong>{user.displayName}</strong>
          <span>{user.email}</span>
          <span className="status-badge active">{user.role}</span>
        </div>
      </div>
      <form className="profile-form" onSubmit={(event) => void handleSubmit(event)}>
        <label>
          {t("auth.displayName")}
          <input
            maxLength={150}
            onChange={(event) => setDisplayName(event.target.value)}
            required
            value={displayName}
          />
        </label>
        {error && <p className="form-error" role="alert">{error}</p>}
        {message && <p className="form-success" role="status">{message}</p>}
        <button className="button-primary" disabled={isSaving} type="submit">
          {isSaving ? t("auth.working") : t("profile.save")}
        </button>
      </form>
      <form className="profile-form" onSubmit={(event) => void handlePasswordSubmit(event)}>
        <h2>{t("profile.passwordTitle")}</h2>
        <label>
          {t("profile.currentPassword")}
          <input
            autoComplete="current-password"
            onChange={(event) => setCurrentPassword(event.target.value)}
            required
            type="password"
            value={currentPassword}
          />
        </label>
        <label>
          {t("profile.newPassword")}
          <input
            autoComplete="new-password"
            maxLength={128}
            minLength={12}
            onChange={(event) => setNewPassword(event.target.value)}
            required
            type="password"
            value={newPassword}
          />
        </label>
        <label>
          {t("profile.confirmPassword")}
          <input
            autoComplete="new-password"
            maxLength={128}
            onChange={(event) => setConfirmPassword(event.target.value)}
            required
            type="password"
            value={confirmPassword}
          />
        </label>
        {error && <p className="form-error" role="alert">{error}</p>}
        {message && <p className="form-success" role="status">{message}</p>}
        <button className="button-secondary" disabled={isSaving} type="submit">
          {isSaving ? t("auth.working") : t("profile.changePassword")}
        </button>
      </form>
      <div className="profile-shortcuts">
        <Link to="/favorites">{t("nav.favorites")}</Link>
        <Link to="/listening-history">{t("nav.history")}</Link>
      </div>
    </section>
  );
}
