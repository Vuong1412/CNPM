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
  const [qrOrderId, setQrOrderId] = useState<string | null>(null);
  const [claimingOrderId, setClaimingOrderId] = useState<string | null>(null);
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

  async function reportQrPayment(orderId: string) {
    setClaimingOrderId(orderId);
    setError(null);
    try {
      const claim = await apiClient.post<{ reportedAt: string }>(
        `me/orders/${encodeURIComponent(orderId)}/qr-payment-claim`,
        {}
      );
      setOrders((current) => current.map((order) => order.id === orderId
        ? { ...order, manualQrReportedAt: claim.reportedAt }
        : order));
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t("orders.qrClaimError"));
    } finally {
      setClaimingOrderId(null);
    }
  }

  if (isAuthLoading || (user && isLoading)) {
    return <section className="content-page"><p role="status">{t("common.loading")}</p></section>;
  }

  const qrOrder = orders.find((order) => order.id === qrOrderId) ?? null;

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
                {order.status === "Pending" && (
                  <>
                    <p className="form-hint">{t("orders.pendingNotice")}</p>
                    <button
                      className="button-primary"
                      onClick={() => {
                        setError(null);
                        setQrOrderId(order.id);
                      }}
                      type="button"
                    >
                      {t("orders.payWithQr")}
                    </button>
                  </>
                )}
                {order.manualQrReportedAt && (
                  <p className="qr-payment-confirmation" role="status">
                    {t("orders.qrClaimedAt", {
                      date: new Date(order.manualQrReportedAt).toLocaleString()
                    })}
                  </p>
                )}
                {order.reviewNote && <p>{t("orders.reviewNote", { note: order.reviewNote })}</p>}
              </article>
            ))}
          </div>
          {qrOrder && (
            <div className="qr-payment-overlay">
              <section
                aria-labelledby="qr-payment-title"
                aria-modal="true"
                className="qr-payment-dialog"
                role="dialog"
              >
                <div className="qr-payment-heading">
                  <h2 id="qr-payment-title">{t("orders.qrTitle")}</h2>
                  <button
                    aria-label={t("orders.qrClose")}
                    className="button-secondary"
                    onClick={() => setQrOrderId(null)}
                    type="button"
                  >
                    {t("orders.qrClose")}
                  </button>
                </div>
                <img
                  alt={t("orders.qrImageAlt")}
                  className="qr-payment-image"
                  src="/payment-qr.jpg"
                />
                <p>
                  {t("orders.qrInstructions", {
                    amount: formatPrice(qrOrder.amount, qrOrder.currency),
                    id: qrOrder.id
                  })}
                </p>
                {qrOrder.manualQrReportedAt ? (
                  <p className="qr-payment-confirmation" role="status">
                    {t("orders.qrClaimedAt", {
                      date: new Date(qrOrder.manualQrReportedAt).toLocaleString()
                    })}
                  </p>
                ) : (
                  <button
                    className="button-primary"
                    disabled={claimingOrderId === qrOrder.id}
                    onClick={() => void reportQrPayment(qrOrder.id)}
                    type="button"
                  >
                    {claimingOrderId === qrOrder.id
                      ? t("auth.working")
                      : t("orders.qrReportPaid")}
                  </button>
                )}
              </section>
            </div>
          )}
        </>
      )}
    </section>
  );
}
