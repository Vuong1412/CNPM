import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";
import type { SubscriptionOrder, UserSubscription } from "@qol/shared";
import { useAuth } from "../context/AuthContext";
import { apiClient } from "../services/api";

function formatPrice(price: number, currency: string) {
  return new Intl.NumberFormat(undefined, {
    style: "currency",
    currency,
    maximumFractionDigits: 2
  }).format(price);
}

export function OrdersPage() {
  const { t } = useTranslation();
  const { user, isLoading: isAuthLoading } = useAuth();
  const [orders, setOrders] = useState<SubscriptionOrder[]>([]);
  const [subscriptions, setSubscriptions] = useState<UserSubscription[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (isAuthLoading || !user) {
      setIsLoading(false);
      return;
    }

    const controller = new AbortController();
    Promise.all([
      apiClient.get<SubscriptionOrder[]>("me/orders", controller.signal),
      apiClient.get<UserSubscription[]>("me/subscriptions", controller.signal)
    ])
      .then(([userOrders, userSubscriptions]) => {
        setOrders(userOrders);
        setSubscriptions(userSubscriptions);
      })
      .catch((cause: unknown) => {
        if (!controller.signal.aborted) {
          setError(cause instanceof Error ? cause.message : t("orders.loadError"));
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) {
          setIsLoading(false);
        }
      });

    return () => controller.abort();
  }, [isAuthLoading, t, user]);

  if (isAuthLoading || (user && isLoading)) {
    return <section className="content-page"><p role="status">{t("common.loading")}</p></section>;
  }

  return (
    <section className="content-page commerce-page">
      <p className="eyebrow">{t("orders.eyebrow")}</p>
      <h1>{t("orders.title")}</h1>
      {!user ? (
        <>
          <p>{t("orders.signIn")}</p>
          <Link className="button-primary" to="/login">{t("nav.login")}</Link>
        </>
      ) : (
        <>
          <p>{t("orders.description")}</p>
          {error && <p className="form-error" role="alert">{error}</p>}
          {!error && (
            <section className="subscription-summary" aria-labelledby="active-subscriptions-title">
              <h2 id="active-subscriptions-title">{t("orders.activeSubscription")}</h2>
              {subscriptions.some((subscription) => subscription.isActive) ? (
                <div className="activity-list">
                  {subscriptions.filter((subscription) => subscription.isActive).map((subscription) => (
                    <article className="activity-card" key={subscription.id}>
                      <h3>{subscription.planName}</h3>
                      <p>
                        {t("orders.expiresAt", {
                          date: new Date(subscription.endsAt).toLocaleDateString()
                        })}
                      </p>
                    </article>
                  ))}
                </div>
              ) : (
                <p>
                  {t("orders.noActiveSubscription")}{" "}
                  <Link to="/plans">{t("nav.plans")}</Link>
                </p>
              )}
            </section>
          )}
          {!error && orders.length === 0 && (
            <p className="admin-empty">{t("orders.empty")}</p>
          )}
          <div className="activity-list">
            {orders.map((order) => (
              <article className="activity-card" key={order.id}>
                <div className="activity-card-heading">
                  <div>
                    <p className="eyebrow">{new Date(order.createdAt).toLocaleString()}</p>
                    <h2>{order.planName}</h2>
                    <p>{formatPrice(order.amount, order.currency)}</p>
                    <small>{t("orders.reference", { id: order.id })}</small>
                  </div>
                  <span className={`status-badge ${order.status === "Verified" ? "active" : "inactive"}`}>
                    {t(`orders.status.${order.status}`)}
                  </span>
                </div>
                {order.status === "Pending" && <p className="form-hint">{t("orders.pendingNotice")}</p>}
                {order.reviewNote && <p>{t("orders.reviewNote", { note: order.reviewNote })}</p>}
              </article>
            ))}
          </div>
        </>
      )}
    </section>
  );
}
