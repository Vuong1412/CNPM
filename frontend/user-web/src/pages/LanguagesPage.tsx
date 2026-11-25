import i18n from "../i18n";
import { useTranslation } from "react-i18next";
import { useLanguages } from "../hooks/useLanguages";

export function LanguagesPage() {
  const { t } = useTranslation();
  const { languages, isLoading, error } = useLanguages();

  return (
    <section className="content-page">
      <p className="eyebrow">{t("languages.eyebrow")}</p>
      <h1>{t("languages.title")}</h1>
      {isLoading && <p role="status">{t("languages.loading")}</p>}
      {error && <p role="alert">{t("languages.error", { message: error })}</p>}
      <ul className="language-list">
        {languages.map((language) => (
          <li key={language.code}>
            <label>
              <input
                type="radio"
                name="interface-language"
                value={language.code}
                checked={i18n.resolvedLanguage === language.code}
                onChange={() => {
                  window.localStorage.setItem("qol.language", language.code);
                  void i18n.changeLanguage(language.code);
                }}
              />
              <span>{language.nativeName}</span>
            </label>
            <span className="muted">{language.name}</span>
          </li>
        ))}
      </ul>
    </section>
  );
}
