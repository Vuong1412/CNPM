import { useCallback, useEffect, useState, type FormEvent } from "react";
import { Link, Navigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { ApiError, type LocationSummary } from "@qol/shared";
import { useAuth } from "../context/AuthContext";
import { useLanguages } from "../hooks/useLanguages";
import { AudioManagementPanel } from "../components/AudioManagementPanel";
import { AdminCommercePanel } from "../components/AdminCommercePanel";
import { apiClient } from "../services/api";

interface ManagedLocation extends LocationSummary {
  createdAt: string;
  isActive: boolean;
}

export function AdminPage() {
  const { t } = useTranslation();
  const { user, isLoading: isAuthLoading } = useAuth();
  const {
    languages,
    isLoading: isLoadingLanguages,
    error: languageError
  } = useLanguages();
  const [locations, setLocations] = useState<ManagedLocation[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [name, setName] = useState("");
  const [address, setAddress] = useState("");
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editName, setEditName] = useState("");
  const [editAddress, setEditAddress] = useState("");

  const loadLocations = useCallback(async (signal?: AbortSignal) => {
    try {
      const items = await apiClient.get<ManagedLocation[]>(
        "admin/locations",
        signal
      );
      setLocations(items);
      setError(null);
    } catch (cause) {
      if (signal?.aborted) {
        return;
      }
      if (cause instanceof ApiError && cause.status === 401) {
        setError(t("admin.sessionExpired"));
      } else if (cause instanceof ApiError && cause.status === 403) {
        setError(t("admin.noPermission"));
      } else {
        setError(cause instanceof Error ? cause.message : t("admin.loadError"));
      }
    } finally {
      if (!signal?.aborted) {
        setIsLoading(false);
      }
    }
  }, [t]);

  useEffect(() => {
    if (user?.role !== "Admin") {
      setIsLoading(false);
      return;
    }
    const controller = new AbortController();
    void loadLocations(controller.signal);
    return () => controller.abort();
  }, [user?.role, loadLocations]);

  async function handleCreate(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsSaving(true);
    setError(null);
    try {
      await apiClient.post<ManagedLocation>("admin/locations", { name, address });
      setName("");
      setAddress("");
      await loadLocations();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t("admin.saveError"));
    } finally {
      setIsSaving(false);
    }
  }

  function startEditing(location: ManagedLocation) {
    if (!location.isActive) {
      return;
    }
    setEditingId(location.id);
    setEditName(location.name);
    setEditAddress(location.address);
  }

  async function handleUpdate(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!editingId) {
      return;
    }

    setIsSaving(true);
    setError(null);
    try {
      await apiClient.put<ManagedLocation>(
        `admin/locations/${encodeURIComponent(editingId)}`,
        { name: editName, address: editAddress }
      );
      setEditingId(null);
      await loadLocations();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t("admin.saveError"));
    } finally {
      setIsSaving(false);
    }
  }

  async function handleDeactivate(location: ManagedLocation) {
    if (!window.confirm(t("admin.confirmDeactivate", { name: location.name }))) {
      return;
    }

    setError(null);
    try {
      await apiClient.delete<void>(
        `admin/locations/${encodeURIComponent(location.id)}`
      );
      await loadLocations();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t("admin.saveError"));
    }
  }

  if (isAuthLoading) {
    return <section className="content-page"><p role="status">{t("common.loading")}</p></section>;
  }

  if (!user) {
    return <Navigate to="/login" state={{ from: { pathname: "/admin" } }} replace />;
  }

  if (user.role !== "Admin") {
    return (
      <section className="content-page">
        <p className="eyebrow">{t("admin.eyebrow")}</p>
        <h1>{t("admin.noPermission")}</h1>
        <Link className="button-primary" to="/">{t("notFound.home")}</Link>
      </section>
    );
  }

  return (
    <section className="content-page admin-page">
      <p className="eyebrow">{t("admin.eyebrow")}</p>
      <div className="admin-heading">
        <div>
          <h1>{t("admin.title")}</h1>
          <p>{t("admin.description")}</p>
        </div>
        <span className="admin-role-badge">{user.displayName} · Admin</span>
      </div>

      <div className="admin-panel">
        <div className="admin-panel-heading">
          <div>
            <h2>{t("admin.addLocation")}</h2>
            <p>{t("admin.locationHelp")}</p>
          </div>
        </div>
        <form className="admin-create-form" onSubmit={(event) => void handleCreate(event)}>
          <label>
            {t("admin.locationName")}
            <input
              maxLength={200}
              onChange={(event) => setName(event.target.value)}
              required
              value={name}
            />
          </label>
          <label>
            {t("admin.locationAddress")}
            <input
              maxLength={500}
              onChange={(event) => setAddress(event.target.value)}
              required
              value={address}
            />
          </label>
          <button className="button-primary" disabled={isSaving} type="submit">
            {isSaving ? t("auth.working") : t("admin.addLocation")}
          </button>
        </form>
      </div>

      <div className="admin-panel">
        <div className="admin-panel-heading">
          <div>
            <h2>{t("admin.locations")}</h2>
            <p>{t("admin.locationCount", { count: locations.length })}</p>
          </div>
          <button
            className="button-secondary"
            disabled={isLoading}
            onClick={() => void loadLocations()}
            type="button"
          >
            {t("admin.refresh")}
          </button>
        </div>
        {error && <p className="form-error" role="alert">{error}</p>}
        {isLoading && <p role="status">{t("common.loading")}</p>}
        {!isLoading && locations.length === 0 && (
          <p className="admin-empty">{t("admin.noLocations")}</p>
        )}
        <div className="admin-location-list">
          {locations.map((location) => (
            <article className="admin-location-row" key={location.id}>
              {editingId === location.id ? (
                <form className="admin-edit-form" onSubmit={(event) => void handleUpdate(event)}>
                  <label>
                    {t("admin.locationName")}
                    <input
                      maxLength={200}
                      onChange={(event) => setEditName(event.target.value)}
                      required
                      value={editName}
                    />
                  </label>
                  <label>
                    {t("admin.locationAddress")}
                    <input
                      maxLength={500}
                      onChange={(event) => setEditAddress(event.target.value)}
                      required
                      value={editAddress}
                    />
                  </label>
                  <div className="admin-row-actions">
                    <button className="button-primary" disabled={isSaving} type="submit">
                      {t("admin.save")}
                    </button>
                    <button
                      className="button-secondary"
                      onClick={() => setEditingId(null)}
                      type="button"
                    >
                      {t("admin.cancel")}
                    </button>
                  </div>
                </form>
              ) : (
                <>
                  <div className="admin-location-info">
                    <div className="admin-location-title">
                      <strong>{location.name}</strong>
                      <span className={location.isActive ? "status-badge active" : "status-badge inactive"}>
                        {t(location.isActive ? "admin.active" : "admin.inactive")}
                      </span>
                    </div>
                    <span>{location.address}</span>
                  </div>
                  <div className="admin-row-actions">
                    {location.isActive && (
                      <>
                        <button
                          className="button-secondary"
                          onClick={() => startEditing(location)}
                          type="button"
                        >
                          {t("admin.edit")}
                        </button>
                        <button
                          className="button-danger"
                          onClick={() => void handleDeactivate(location)}
                          type="button"
                        >
                          {t("admin.deactivate")}
                        </button>
                      </>
                    )}
                  </div>
                </>
              )}
            </article>
          ))}
        </div>
      </div>

      <AudioManagementPanel
        isLoadingLanguages={isLoadingLanguages}
        languageError={languageError}
        languages={languages}
        locations={locations}
      />
      <AdminCommercePanel />
    </section>
  );
}
