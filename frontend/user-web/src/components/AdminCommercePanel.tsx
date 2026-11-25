import { useCallback, useEffect, useState, type FormEvent } from "react";
import { useTranslation } from "react-i18next";
import type {
  AdminDashboard,
  AdminLocationReview,
  AdminSubscriptionOrder,
  SubscriptionPlan
} from "@qol/shared";
import { apiClient } from "../services/api";

interface PlanDraft {
  name: string;
  description: string;
  price: string;
  currency: string;
  durationDays: string;
}

const emptyDraft: PlanDraft = {
  name: "",
  description: "",
  price: "0",
  currency: "VND",
  durationDays: "30"
};

function formatPrice(price: number, currency: string) {
  return new Intl.NumberFormat(undefined, {
    style: "currency",
    currency,
    maximumFractionDigits: 2
  }).format(price);
}

export function AdminCommercePanel() {
  const { t } = useTranslation();
  const [dashboard, setDashboard] = useState<AdminDashboard | null>(null);
  const [plans, setPlans] = useState<SubscriptionPlan[]>([]);
  const [orders, setOrders] = useState<AdminSubscriptionOrder[]>([]);
  const [reviews, setReviews] = useState<AdminLocationReview[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [reviewingId, setReviewingId] = useState<string | null>(null);
  const [editingPlanId, setEditingPlanId] = useState<string | null>(null);
  const [draft, setDraft] = useState<PlanDraft>(emptyDraft);
  const [reviewNotes, setReviewNotes] = useState<Record<string, string>>({});
  const [error, setError] = useState<string | null>(null);

  const loadData = useCallback(async (signal?: AbortSignal) => {
    setIsLoading(true);
    try {
      const [dashboardData, planData, orderData, reviewData] = await Promise.all([
        apiClient.get<AdminDashboard>("admin/dashboard", signal),
        apiClient.get<SubscriptionPlan[]>("admin/plans", signal),
        apiClient.get<AdminSubscriptionOrder[]>("admin/orders", signal),
        apiClient.get<AdminLocationReview[]>("admin/reviews", signal)
      ]);
      setDashboard(dashboardData);
      setPlans(planData);
      setOrders(orderData);
      setReviews(reviewData);
      setError(null);
    } catch (cause) {
      if (!signal?.aborted) {
        setError(cause instanceof Error ? cause.message : t("admin.commerceLoadError"));
      }
    } finally {
      if (!signal?.aborted) {
        setIsLoading(false);
      }
    }
  }, [t]);

  useEffect(() => {
    const controller = new AbortController();
    void loadData(controller.signal);
    return () => controller.abort();
  }, [loadData]);

  async function savePlan(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsSaving(true);
    setError(null);
    const payload = {
      name: draft.name,
      description: draft.description,
      price: Number(draft.price),
      currency: draft.currency.toUpperCase(),
      durationDays: Number(draft.durationDays)
    };

    try {
      if (editingPlanId) {
        await apiClient.put<SubscriptionPlan>(
          `admin/plans/${encodeURIComponent(editingPlanId)}`,
          payload
        );
      } else {
        await apiClient.post<SubscriptionPlan>("admin/plans", payload);
      }
      setEditingPlanId(null);
      setDraft(emptyDraft);
      await loadData();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t("admin.commerceSaveError"));
    } finally {
      setIsSaving(false);
    }
  }

  function editPlan(plan: SubscriptionPlan) {
    setEditingPlanId(plan.id);
    setDraft({
      name: plan.name,
      description: plan.description,
      price: String(plan.price),
      currency: plan.currency,
      durationDays: String(plan.durationDays)
    });
  }

  async function togglePlan(plan: SubscriptionPlan) {
    setError(null);
    try {
      await apiClient.patch<void>(
        `admin/plans/${encodeURIComponent(plan.id)}/visibility`,
        { isActive: !plan.isActive }
      );
      await loadData();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t("admin.commerceSaveError"));
    }
  }

  async function reviewOrder(order: AdminSubscriptionOrder, status: "Verified" | "Rejected") {
    setReviewingId(order.id);
    setError(null);
    try {
      await apiClient.patch<void>(
        `admin/orders/${encodeURIComponent(order.id)}/review`,
        { status, note: reviewNotes[order.id] ?? "" }
      );
      await loadData();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t("admin.orderReviewError"));
    } finally {
      setReviewingId(null);
    }
  }

  async function toggleReviewVisibility(review: AdminLocationReview) {
    setError(null);
    try {
      await apiClient.patch<void>(
        `admin/reviews/${encodeURIComponent(review.id)}/visibility`,
        { isVisible: !review.isVisible }
      );
      await loadData();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t("admin.reviewModerationError"));
    }
  }

  return (
    <>
      <div className="admin-panel">
        <div className="admin-panel-heading">
          <div>
            <h2>{t("admin.dashboardTitle")}</h2>
            <p>{t("admin.dashboardDescription")}</p>
          </div>
          <button
            className="button-secondary"
            disabled={isLoading}
            onClick={() => void loadData()}
            type="button"
          >
            {t("admin.refresh")}
          </button>
        </div>
        {error && <p className="form-error" role="alert">{error}</p>}
        {isLoading && <p role="status">{t("common.loading")}</p>}
        {dashboard && (
          <div className="dashboard-stat-grid">
            <div><strong>{dashboard.userCount}</strong><span>{t("admin.statUsers")}</span></div>
            <div><strong>{dashboard.activeLocationCount}</strong><span>{t("admin.statLocations")}</span></div>
            <div><strong>{dashboard.activeAudioCount}</strong><span>{t("admin.statAudio")}</span></div>
            <div><strong>{dashboard.activePlanCount}</strong><span>{t("admin.statPlans")}</span></div>
            <div><strong>{dashboard.pendingOrderCount}</strong><span>{t("admin.statPendingOrders")}</span></div>
          </div>
        )}
      </div>

      <div className="admin-panel">
        <div className="admin-panel-heading">
          <div>
            <h2>{t("admin.plansTitle")}</h2>
            <p>{t("admin.plansDescription")}</p>
          </div>
        </div>
        <form className="plan-admin-form" onSubmit={(event) => void savePlan(event)}>
          <label>
            {t("plans.name")}
            <input
              maxLength={120}
              onChange={(event) => setDraft({ ...draft, name: event.target.value })}
              required
              value={draft.name}
            />
          </label>
          <label className="plan-description-field">
            {t("plans.planDescription")}
            <textarea
              maxLength={1000}
              onChange={(event) => setDraft({ ...draft, description: event.target.value })}
              required
              rows={3}
              value={draft.description}
            />
          </label>
          <label>
            {t("plans.price")}
            <input
              min={0}
              onChange={(event) => setDraft({ ...draft, price: event.target.value })}
              required
              step="0.01"
              type="number"
              value={draft.price}
            />
          </label>
          <label>
            {t("plans.currency")}
            <input
              maxLength={3}
              minLength={3}
              onChange={(event) => setDraft({ ...draft, currency: event.target.value })}
              pattern="[A-Za-z]{3}"
              required
              value={draft.currency}
            />
          </label>
          <label>
            {t("plans.durationDays")}
            <input
              max={3650}
              min={1}
              onChange={(event) => setDraft({ ...draft, durationDays: event.target.value })}
              required
              type="number"
              value={draft.durationDays}
            />
          </label>
          <div className="activity-actions">
            <button className="button-primary" disabled={isSaving} type="submit">
              {isSaving
                ? t("auth.working")
                : t(editingPlanId ? "admin.save" : "admin.createPlan")}
            </button>
            {editingPlanId && (
              <button
                className="button-secondary"
                onClick={() => {
                  setEditingPlanId(null);
                  setDraft(emptyDraft);
                }}
                type="button"
              >
                {t("admin.cancel")}
              </button>
            )}
          </div>
        </form>
        <div className="admin-location-list">
          {plans.map((plan) => (
            <article className="admin-location-row" key={plan.id}>
              <div className="admin-location-info">
                <div className="admin-location-title">
                  <strong>{plan.name}</strong>
                  <span className={plan.isActive ? "status-badge active" : "status-badge inactive"}>
                    {t(plan.isActive ? "admin.active" : "admin.inactive")}
                  </span>
                </div>
                <span>
                  {formatPrice(plan.price, plan.currency)} · {t("plans.duration", { days: plan.durationDays })}
                </span>
              </div>
              <div className="admin-row-actions">
                <button className="button-secondary" onClick={() => editPlan(plan)} type="button">
                  {t("audio.edit")}
                </button>
                <button
                  className={plan.isActive ? "button-danger" : "button-secondary"}
                  onClick={() => void togglePlan(plan)}
                  type="button"
                >
                  {t(plan.isActive ? "admin.deactivatePlan" : "admin.activatePlan")}
                </button>
              </div>
            </article>
          ))}
          {!isLoading && plans.length === 0 && <p className="admin-empty">{t("plans.empty")}</p>}
        </div>
      </div>

      <div className="admin-panel">
        <div className="admin-panel-heading">
          <div>
            <h2>{t("admin.ordersTitle")}</h2>
            <p>{t("admin.ordersDescription")}</p>
          </div>
        </div>
        {!isLoading && orders.length === 0 && <p className="admin-empty">{t("admin.noOrders")}</p>}
        <div className="admin-location-list">
          {orders.map((order) => (
            <article className="admin-location-row admin-order-row" key={order.id}>
              <div className="admin-location-info">
                <div className="admin-location-title">
                  <strong>{order.planName}</strong>
                  <span className={`status-badge ${order.status === "Verified" ? "active" : "inactive"}`}>
                    {t(`orders.status.${order.status}`)}
                  </span>
                </div>
                <span>
                  {order.userEmail} · {formatPrice(order.amount, order.currency)} · {new Date(order.createdAt).toLocaleString()}
                </span>
                <small>{t("orders.reference", { id: order.id })}</small>
                {order.reviewNote && <small>{t("orders.reviewNote", { note: order.reviewNote })}</small>}
              </div>
              {order.status === "Pending" && (
                <div className="admin-review-actions">
                  <label>
                    {t("admin.reviewNote")}
                    <input
                      maxLength={1000}
                      onChange={(event) => setReviewNotes({
                        ...reviewNotes,
                        [order.id]: event.target.value
                      })}
                      value={reviewNotes[order.id] ?? ""}
                    />
                  </label>
                  <button
                    className="button-primary"
                    disabled={reviewingId !== null}
                    onClick={() => void reviewOrder(order, "Verified")}
                    type="button"
                  >
                    {t("admin.verifyPayment")}
                  </button>
                  <button
                    className="button-danger"
                    disabled={reviewingId !== null}
                    onClick={() => void reviewOrder(order, "Rejected")}
                    type="button"
                  >
                    {t("admin.rejectOrder")}
                  </button>
                </div>
              )}
            </article>
          ))}
        </div>
      </div>

      <div className="admin-panel">
        <div className="admin-panel-heading">
          <div>
            <h2>{t("admin.reviewsTitle")}</h2>
            <p>{t("admin.reviewsDescription")}</p>
          </div>
        </div>
        {!isLoading && reviews.length === 0 && <p className="admin-empty">{t("admin.noReviews")}</p>}
        <div className="admin-location-list">
          {reviews.map((review) => (
            <article className="admin-location-row" key={review.id}>
              <div className="admin-location-info">
                <div className="admin-location-title">
                  <strong>{review.locationName}</strong>
                  <span className={review.isVisible ? "status-badge active" : "status-badge inactive"}>
                    {t(review.isVisible ? "admin.reviewVisible" : "admin.reviewHidden")}
                  </span>
                </div>
                <span>{review.userEmail} · {review.rating}/5 · {new Date(review.createdAt).toLocaleString()}</span>
                {review.comment && <small>{review.comment}</small>}
              </div>
              <button
                className={review.isVisible ? "button-danger" : "button-secondary"}
                onClick={() => void toggleReviewVisibility(review)}
                type="button"
              >
                {t(review.isVisible ? "admin.hideReview" : "admin.showReview")}
              </button>
            </article>
          ))}
        </div>
      </div>
    </>
  );
}
