import { useCallback, useEffect, useState, type ChangeEvent, type FormEvent } from "react";
import { Link, Navigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import {
  ApiError,
  type LocationDetails,
  type LocationImage,
  type LocationStatus
} from "@qol/shared";
import { useAuth } from "../context/AuthContext";
import { useLanguages } from "../hooks/useLanguages";
import { AudioManagementPanel } from "../components/AudioManagementPanel";
import { AdminCommercePanel } from "../components/AdminCommercePanel";
import { TranslationManagementPanel } from "../components/TranslationManagementPanel";
import { apiClient, getMediaUrl } from "../services/api";

type ManagedLocation = LocationDetails;

interface LocationFormState {
  name: string;
  address: string;
  latitude: string;
  longitude: string;
  radiusMeters: string;
  priority: string;
  originalDescription: string;
  directionLink: string;
  status: LocationStatus;
}

const locationStatuses: LocationStatus[] = ["ACTIVE", "INACTIVE", "PENDING", "REJECTED"];

function emptyLocationForm(): LocationFormState {
  return {
    name: "",
    address: "",
    latitude: "",
    longitude: "",
    radiusMeters: "100",
    priority: "0",
    originalDescription: "",
    directionLink: "",
    status: "ACTIVE"
  };
}

function locationRequest(form: LocationFormState) {
  return {
    name: form.name,
    address: form.address,
    latitude: form.latitude.trim() ? Number(form.latitude) : null,
    longitude: form.longitude.trim() ? Number(form.longitude) : null,
    radiusMeters: Number(form.radiusMeters),
    priority: Number(form.priority),
    originalDescription: form.originalDescription.trim() || null,
    directionLink: form.directionLink.trim() || null,
    status: form.status
  };
}

function toLocationForm(location: ManagedLocation): LocationFormState {
  return {
    name: location.name,
    address: location.address,
    latitude: location.latitude?.toString() ?? "",
    longitude: location.longitude?.toString() ?? "",
    radiusMeters: location.radiusMeters.toString(),
    priority: location.priority.toString(),
    originalDescription: location.originalDescription ?? "",
    directionLink: location.directionLink ?? "",
    status: location.status
  };
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
  const [uploadingLocationId, setUploadingLocationId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [newLocation, setNewLocation] = useState(emptyLocationForm);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editLocation, setEditLocation] = useState(emptyLocationForm);

  const loadLocations = useCallback(async (signal?: AbortSignal) => {
    try {
      const items = await apiClient.get<ManagedLocation[]>("admin/locations", signal);
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

  function updateForm(
    setter: (value: LocationFormState | ((current: LocationFormState) => LocationFormState)) => void,
    field: keyof LocationFormState,
    value: string
  ) {
    setter((current) => ({
      ...current,
      [field]: field === "status" ? value as LocationStatus : value
    }));
  }

  async function handleCreate(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsSaving(true);
    setError(null);
    try {
      await apiClient.post<ManagedLocation>("admin/locations", locationRequest(newLocation));
      setNewLocation(emptyLocationForm());
      await loadLocations();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t("admin.saveError"));
    } finally {
      setIsSaving(false);
    }
  }

  function startEditing(location: ManagedLocation) {
    setEditingId(location.id);
    setEditLocation(toLocationForm(location));
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
        locationRequest(editLocation)
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
      await apiClient.delete<void>(`admin/locations/${encodeURIComponent(location.id)}`);
      await loadLocations();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t("admin.saveError"));
    }
  }

  async function handleUploadImage(
    event: ChangeEvent<HTMLInputElement>,
    location: ManagedLocation
  ) {
    const file = event.currentTarget.files?.[0];
    event.currentTarget.value = "";
    if (!file) {
      return;
    }

    const formData = new FormData();
    formData.append("File", file);
    formData.append("AltText", location.name);
    setUploadingLocationId(location.id);
    setError(null);
    try {
      await apiClient.postForm<LocationImage>(
        `admin/locations/${encodeURIComponent(location.id)}/images`,
        formData
      );
      await loadLocations();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t("admin.imageError"));
    } finally {
      setUploadingLocationId(null);
    }
  }

  async function handleDeleteImage(location: ManagedLocation, image: LocationImage) {
    if (!window.confirm(t("admin.confirmDeleteImage", { name: location.name }))) {
      return;
    }

    setError(null);
    try {
      await apiClient.delete<void>(
        `admin/locations/${encodeURIComponent(location.id)}/images/${encodeURIComponent(image.id)}`
      );
      await loadLocations();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t("admin.imageError"));
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

  const renderLocationFields = (
    form: LocationFormState,
    setter: (value: LocationFormState | ((current: LocationFormState) => LocationFormState)) => void
  ) => (
    <>
      <label>
        {t("admin.locationName")}
        <input
          maxLength={200}
          onChange={(event) => updateForm(setter, "name", event.target.value)}
          required
          value={form.name}
        />
      </label>
      <label>
        {t("admin.locationAddress")}
        <input
          maxLength={500}
          onChange={(event) => updateForm(setter, "address", event.target.value)}
          required
          value={form.address}
        />
      </label>
      <label>
        {t("admin.latitude")}
        <input
          max={90}
          min={-90}
          onChange={(event) => updateForm(setter, "latitude", event.target.value)}
          step="any"
          type="number"
          value={form.latitude}
        />
      </label>
      <label>
        {t("admin.longitude")}
        <input
          max={180}
          min={-180}
          onChange={(event) => updateForm(setter, "longitude", event.target.value)}
          step="any"
          type="number"
          value={form.longitude}
        />
      </label>
      <label>
        {t("admin.radiusMeters")}
        <input
          max={100000}
          min={1}
          onChange={(event) => updateForm(setter, "radiusMeters", event.target.value)}
          required
          type="number"
          value={form.radiusMeters}
        />
      </label>
      <label>
        {t("admin.priority")}
        <input
          onChange={(event) => updateForm(setter, "priority", event.target.value)}
          type="number"
          value={form.priority}
        />
      </label>
      <label className="full-row">
        {t("admin.originalDescription")}
        <textarea
          maxLength={4000}
          onChange={(event) => updateForm(setter, "originalDescription", event.target.value)}
          rows={3}
          value={form.originalDescription}
        />
      </label>
      <label className="full-row">
        {t("admin.directionLink")}
        <input
          maxLength={2048}
          onChange={(event) => updateForm(setter, "directionLink", event.target.value)}
          type="url"
          value={form.directionLink}
        />
      </label>
      <label>
        {t("admin.locationStatus")}
        <select
          onChange={(event) => updateForm(setter, "status", event.target.value)}
          value={form.status}
        >
          {locationStatuses.map((status) => (
            <option key={status} value={status}>{t(`admin.status.${status.toLowerCase()}`)}</option>
          ))}
        </select>
      </label>
    </>
  );

  return (
    <section className="content-page admin-page">
      <p className="eyebrow">{t("admin.eyebrow")}</p>
      <div className="admin-heading">
        <div>
          <h1>{t("admin.title")}</h1>
          <p>{t("admin.description")}</p>
        </div>
        <div className="admin-heading-actions">
          <span className="admin-role-badge">{user.displayName} · Admin</span>
          <Link className="button-secondary" to="/admin/access-qr">
            {t("admin.accessQrTitle")}
          </Link>
        </div>
      </div>

      <div className="admin-panel">
        <div className="admin-panel-heading">
          <div>
            <h2>{t("admin.addLocation")}</h2>
            <p>{t("admin.locationHelp")}</p>
          </div>
        </div>
        <form className="admin-location-form" onSubmit={(event) => void handleCreate(event)}>
          {renderLocationFields(newLocation, setNewLocation)}
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
                <form className="admin-location-form" onSubmit={(event) => void handleUpdate(event)}>
                  {renderLocationFields(editLocation, setEditLocation)}
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
                      <span className={location.status === "ACTIVE"
                        ? "status-badge active"
                        : "status-badge inactive"}>
                        {t(`admin.status.${location.status.toLowerCase()}`)}
                      </span>
                    </div>
                    <span>{location.address}</span>
                    <span>
                      {location.latitude !== null && location.longitude !== null
                        ? `${location.latitude}, ${location.longitude}`
                        : t("admin.coordinatesMissing")}
                    </span>
                    <div className="admin-location-images">
                      {location.images.length > 0 && (
                        <div className="admin-location-image-list">
                          {location.images.map((image) => (
                            <div className="admin-location-image-item" key={image.id}>
                              <img alt={image.altText ?? location.name} src={getMediaUrl(image.imageUrl)} />
                              <button
                                className="button-danger"
                                onClick={() => void handleDeleteImage(location, image)}
                                type="button"
                              >
                                {t("admin.deleteImage")}
                              </button>
                            </div>
                          ))}
                        </div>
                      )}
                      <label className="admin-location-image-form">
                        {t("admin.uploadImage")}
                        <input
                          accept="image/png,image/jpeg,image/webp"
                          disabled={uploadingLocationId === location.id}
                          onChange={(event) => void handleUploadImage(event, location)}
                          type="file"
                        />
                        {uploadingLocationId === location.id && <span role="status">{t("auth.working")}</span>}
                      </label>
                    </div>
                  </div>
                  <div className="admin-row-actions">
                    <button
                      className="button-secondary"
                      onClick={() => startEditing(location)}
                      type="button"
                    >
                      {t("admin.edit")}
                    </button>
                    {location.status !== "INACTIVE" && (
                      <button
                        className="button-danger"
                        onClick={() => void handleDeactivate(location)}
                        type="button"
                      >
                        {t("admin.deactivate")}
                      </button>
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
      <TranslationManagementPanel locations={locations} languages={languages} />
      <AdminCommercePanel />
    </section>
  );
}
