import { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import type { SubscriptionOrder, SubscriptionPlan } from "@qol/shared";
import { useAuth } from "../context/AuthContext";
import { apiClient } from "../services/api";

function formatPrice(price: number, currency: string) {
  return new Intl.NumberFormat(undefined, {
    style: "currency",
    currency,
    maximumFractionDigits: 2
  }).format(price);
}

export function PlansPage() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const navigate = useNavigate();
  const [plans, setPlans] = useState<SubscriptionPlan[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [isSavingId, setIsSavingId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [pendingOrder, setPendingOrder] = useState<SubscriptionOrder | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    apiClient
      .get<SubscriptionPlan[]>("plans", controller.signal)
      .then(setPlans)
      .catch((cause: unknown) => {
        if (!controller.signal.aborted) {
          setError(cause instanceof Error ? cause.message : t("plans.loadError"));
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) {
          setIsLoading(false);
        }
      });
    return () => controller.abort();
  }, [t]);

  async function createOrder(plan: SubscriptionPlan) {
    if (!user) {
      navigate("/login", { state: { from: { pathname: "/plans" } } });
      return;
    }

    setIsSavingId(plan.id);
    setError(null);
    try {
      const order = await apiClient.post<SubscriptionOrder>("me/orders", { planId: plan.id });
      setPendingOrder(order);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t("plans.orderError"));
    } finally {
      setIsSavingId(null);
    }
  }

  return (
    <section className="content-page commerce-page">
      <p className="eyebrow">{t("plans.eyebrow")}</p>
      <h1>{t("plans.title")}</h1>
      <p>{t("plans.description")}</p>
      <p className="form-hint">{t("plans.paymentNotice")}</p>
      {error && <p className="form-error" role="alert">{error}</p>}
      {pendingOrder && (
        <p className="form-success" role="status">
          {t("plans.orderPending", { id: pendingOrder.id })}
          {" "}
          <Link to="/orders">{t("plans.viewOrders")}</Link>
        </p>
      )}
      {isLoading && <p role="status">{t("common.loading")}</p>}
      {!isLoading && plans.length === 0 && <p className="admin-empty">{t("plans.empty")}</p>}
      <div className="plan-grid">
        {plans.map((plan) => (
          <article className="plan-card" key={plan.id}>
            <p className="eyebrow">{t("plans.duration", { days: plan.durationDays })}</p>
            <h2>{plan.name}</h2>
            <p>{plan.description}</p>
            <strong className="plan-price">{formatPrice(plan.price, plan.currency)}</strong>
            <button
              className="button-primary"
              disabled={isSavingId !== null}
              onClick={() => void createOrder(plan)}
              type="button"
            >
              {isSavingId === plan.id ? t("auth.working") : t("plans.order")}
            </button>
          </article>
        ))}
      </div>
    </section>
  );
}
