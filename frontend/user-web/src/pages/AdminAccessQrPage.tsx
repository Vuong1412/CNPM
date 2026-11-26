import { useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { QRCodeSVG } from "qrcode.react";
import { useTranslation } from "react-i18next";

export function AdminAccessQrPage() {
  const { t } = useTranslation();
  const [copied, setCopied] = useState(false);
  const [copyError, setCopyError] = useState(false);
  const accessUrl = useMemo(
    () => new URL("/access", window.location.origin).toString(),
    []
  );

  async function copyUrl() {
    try {
      await navigator.clipboard.writeText(accessUrl);
      setCopied(true);
      setCopyError(false);
    } catch {
      setCopied(false);
      setCopyError(true);
    }
  }

  return (
    <section className="content-page qr-page">
      <p className="eyebrow">{t("admin.eyebrow")}</p>
      <h1>{t("admin.accessQrTitle")}</h1>
      <p>{t("admin.accessQrDescription")}</p>
      <div className="qr-card">
        <QRCodeSVG value={accessUrl} size={280} level="M" title={t("admin.accessQrTitle")} />
        <code>{accessUrl}</code>
        <button className="button-secondary" onClick={() => void copyUrl()} type="button">
          {t(copied ? "admin.accessQrCopied" : "admin.accessQrCopy")}
        </button>
        {copyError && <p className="form-error" role="alert">{t("admin.accessQrCopyError")}</p>}
      </div>
      <Link to="/admin">{t("access.backToAdmin")}</Link>
    </section>
  );
}
