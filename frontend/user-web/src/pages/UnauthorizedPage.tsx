import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";

export function UnauthorizedPage() {
  const { t } = useTranslation();

  return (
    <section className="content-page">
      <h1>{t("admin.noPermission")}</h1>
      <Link className="button-primary" to="/">{t("notFound.home")}</Link>
    </section>
  );
}
