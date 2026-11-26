import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { apiClient } from "../services/api";

interface SystemPolicy {
  version: string;
  visitorSessionLifetimeDays: number;
  visitorCookieHttpOnly: boolean;
  visitorCookieSameSite: string;
  visitorCookiePath: string;
  visitorSessionGrantsAudioAccess: boolean;
  audioPlaybackRequiresActiveSubscription: boolean;
  accountOperationsRequireAuthentication: boolean;
  listeningHistoryRecordedOnAuthorizedPlayback: boolean;
}

export function PrivacyPage() {
  const { t } = useTranslation();
  const [policy, setPolicy] = useState<SystemPolicy | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    void apiClient.get<SystemPolicy>("policy", controller.signal)
      .then(setPolicy)
      .catch((cause: unknown) => {
        if (!controller.signal.aborted) {
          setError(cause instanceof Error ? cause.message : t("privacy.loadError"));
        }
      });
    return () => controller.abort();
  }, [t]);

  return (
    <section className="content-page privacy-page">
      <p className="eyebrow">{t("privacy.eyebrow")}</p>
      <h1>{t("privacy.title")}</h1>
      <p>{t("privacy.intro")}</p>
      {error && <p className="form-error" role="alert">{error}</p>}
      {!policy && !error && <p role="status">{t("common.loading")}</p>}
      {policy && (
        <div className="access-panel">
          <p className="muted">{t("privacy.version", { version: policy.version })}</p>
          <h2>{t("privacy.sessionsTitle")}</h2>
          <p>{t("privacy.sessionsDescription", {
            days: policy.visitorSessionLifetimeDays,
            path: policy.visitorCookiePath,
            sameSite: policy.visitorCookieSameSite
          })}</p>
          <p>{t("privacy.cookieHttpOnly", {
            value: policy.visitorCookieHttpOnly ? t("common.yes") : t("common.no")
          })}</p>
          <h2>{t("privacy.audioTitle")}</h2>
          <p>{t(policy.visitorSessionGrantsAudioAccess
            ? "privacy.sessionCanListen"
            : "privacy.sessionCannotListen")}</p>
          <p>{t(policy.audioPlaybackRequiresActiveSubscription
            ? "privacy.subscriptionRequired"
            : "privacy.subscriptionNotRequired")}</p>
          <p>{t(policy.listeningHistoryRecordedOnAuthorizedPlayback
            ? "privacy.historyAuthorized"
            : "privacy.historyUnavailable")}</p>
          <h2>{t("privacy.accountTitle")}</h2>
          <p>{t(policy.accountOperationsRequireAuthentication
            ? "privacy.accountRequiresLogin"
            : "privacy.accountMayBeAnonymous")}</p>
        </div>
      )}
    </section>
  );
}
