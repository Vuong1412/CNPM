import { useCallback, useEffect, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { useTranslation } from "react-i18next";
import type { SubscriptionOrder } from "@qol/shared";
import { apiClient } from "../services/api";

export function PaymentResultPage() {
  const { t } = useTranslation();
  const [searchParams] = useSearchParams();
  const orderId = searchParams.get("orderId");
  const [order, setOrder] = useState<SubscriptionOrder | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  const loadOrder = useCallback(async (signal?: AbortSignal) => {
    if (!orderId) {
      setError(t("paymentResult.missingOrder"));
      setIsLoading(false);
      return;
    }

    try {
      const orders = await apiClient.get<SubscriptionOrder[]>("me/orders", signal);
      setOrder(orders.find((item) => item.id === orderId) ?? null);
      setError(null);
    } catch (cause) {
      if (!signal?.aborted) {
        setError(cause instanceof Error ? cause.message : t("paymentResult.loadError"));
      }
    } finally {
      if (!signal?.aborted) {
        setIsLoading(false);
      }
    }
  }, [orderId, t]);

  useEffect(() => {
    const controller = new AbortController();
    void loadOrder(controller.signal);
    return () => controller.abort();
  }, [loadOrder]);

  return (
    <section className="content-page payment-result-page">
      <p className="eyebrow">{t("paymentResult.eyebrow")}</p>
      <h1>{t("paymentResult.title")}</h1>
      <p>{t("paymentResult.explanation")}</p>
      {isLoading && <p role="status">{t("common.loading")}</p>}
      {error && <p className="form-error" role="alert">{error}</p>}
      {!isLoading && order && (
        <div className="access-panel">
          <h2>{order.planName}</h2>
          <p>{t("paymentResult.status", { status: t(`orders.status.${order.status}`) })}</p>
          {order.status === "Verified"
            ? <p>{t("paymentResult.verified")}</p>
            : <p>{t("paymentResult.pending")}</p>}
          <button className="button-secondary" onClick={() => void loadOrder()} type="button">
            {t("orders.refresh")}
          </button>
        </div>
      )}
      {!isLoading && !error && !order && (
        <p className="form-error" role="alert">{t("paymentResult.notFound")}</p>
      )}
      <Link to="/orders">{t("nav.orders")}</Link>
    </section>
  );
}
