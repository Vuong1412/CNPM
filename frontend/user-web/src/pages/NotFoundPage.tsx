import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";

export function NotFoundPage() {
  const { t } = useTranslation();

  return (
    <section className="content-page">
      <h1>{t("notFound.title")}</h1>
      <p>{t("notFound.description")}</p>
      <Link className="button-link" to="/">{t("notFound.home")}</Link>
    </section>
  );
}
