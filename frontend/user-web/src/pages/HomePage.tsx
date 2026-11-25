import { useState, type FormEvent } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";

const destinations = [
  {
    name: "Vịnh Hạ Long",
    location: "Quảng Ninh, Việt Nam",
    category: "Thiên nhiên",
    image:
      "https://images.unsplash.com/photo-1528127269322-539801943592?auto=format&fit=crop&w=900&q=85",
    className: "destination-card destination-card-tall"
  },
  {
    name: "Phố cổ Hội An",
    location: "Quảng Nam, Việt Nam",
    category: "Văn hóa",
    image:
      "https://images.unsplash.com/photo-1559592413-7cec4d0cae2b?auto=format&fit=crop&w=900&q=85",
    className: "destination-card"
  },
  {
    name: "Ruộng bậc thang",
    location: "Sa Pa, Việt Nam",
    category: "Thiên nhiên",
    image:
      "https://images.unsplash.com/photo-1464822759023-fed622ff2c3b?auto=format&fit=crop&w=900&q=85",
    className: "destination-card"
  }
];

const languages = [
  { code: "vi", label: "Tiếng Việt", flag: "🇻🇳" },
  { code: "en", label: "English", flag: "🇬🇧" },
  { code: "ko", label: "한국어", flag: "🇰🇷" },
  { code: "zh", label: "中文", flag: "🇨🇳" },
  { code: "ja", label: "日本語", flag: "🇯🇵" }
];

function ArrowIcon() {
  return (
    <svg viewBox="0 0 20 20" aria-hidden="true">
      <path d="M4 10h11m-4-4 4 4-4 4" />
    </svg>
  );
}

function SearchIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <circle cx="10.8" cy="10.8" r="6.8" />
      <path d="m16 16 4 4" />
    </svg>
  );
}

function PlayIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <path d="m9 6 10 6-10 6z" />
    </svg>
  );
}

export function HomePage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [query, setQuery] = useState("");

  function handleSearch(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    navigate(`/explore${query.trim() ? `?q=${encodeURIComponent(query.trim())}` : ""}`);
  }

  return (
    <div className="home-page">
      <section className="hero-panel">
        <div className="hero-photo" aria-hidden="true" />
        <div className="hero-content">
          <span className="hero-kicker">
            <span className="kicker-dot" />
            {t("home.eyebrow")}
          </span>
          <h1>
            {t("home.titleTop")}
            <br />
            <span>{t("home.titleHighlight")}</span>
          </h1>
          <p>{t("home.description")}</p>
          <form className="hero-search" onSubmit={handleSearch}>
            <SearchIcon />
            <label className="sr-only" htmlFor="destination-search">
              Tìm địa điểm
            </label>
            <input
              id="destination-search"
              onChange={(event) => setQuery(event.target.value)}
              placeholder={t("home.searchPlaceholder")}
              value={query}
            />
            <button type="submit">
              {t("home.search")} <ArrowIcon />
            </button>
          </form>
          <div className="hero-note">
            <span className="avatar-stack" aria-hidden="true">
              <span>👩🏻</span>
              <span>🧑🏽</span>
              <span>👨🏻</span>
            </span>
            <span>{t("home.communityNote")}</span>
          </div>
        </div>
        <div className="hero-location-card">
          <span className="location-pin">⌖</span>
          <span>
            <strong>{t("home.locationVietnam")}</strong>
            <small>{t("home.locationHint")}</small>
          </span>
          <span className="location-card-arrow">↗</span>
        </div>
        <span className="hero-caption">{t("home.heroCaption")}</span>
      </section>

      <section className="language-strip" aria-label="Ngôn ngữ hỗ trợ">
        <div>
          <span className="section-overline">{t("home.languageEyebrow")}</span>
          <strong>{t("home.languageIntro")}</strong>
        </div>
        <div className="language-pills">
          {languages.map((language) => (
            <span className="language-pill" key={language.code}>
              <span aria-hidden="true">{language.flag}</span> {language.label}
            </span>
          ))}
          <Link className="language-more" to="/languages" aria-label="Xem các ngôn ngữ">
            <ArrowIcon />
          </Link>
        </div>
      </section>

      <section className="section-block destinations-section">
        <div className="section-heading">
          <div>
            <span className="section-overline">{t("home.destinationsEyebrow")}</span>
            <h2>{t("home.destinationsTitle")}</h2>
            <p>{t("home.destinationsDescription")}</p>
          </div>
          <Link className="text-link" to="/explore">
            {t("home.viewAll")} <ArrowIcon />
          </Link>
        </div>

        <div className="destination-grid">
          {destinations.map((destination, index) => (
            <Link
              className={destination.className}
              key={destination.name}
              to={`/explore?q=${encodeURIComponent(destination.name)}`}
            >
              <img alt="" loading="lazy" src={destination.image} />
              <span className="destination-shade" />
              <span className="destination-index">0{index + 1}</span>
              <span className="destination-info">
                <span className="destination-category">
                  {destination.category === "Thiên nhiên"
                    ? t("home.categoryNature")
                    : t("home.categoryCulture")}
                </span>
                <strong>{destination.name}</strong>
                <small>{destination.location}</small>
              </span>
              <span className="destination-arrow">
                <ArrowIcon />
              </span>
            </Link>
          ))}
        </div>
        <p className="demo-note">
          {t("home.demoNote")}
        </p>
      </section>

      <section className="story-section">
        <div className="story-image-wrap">
          <img
            alt="Khung cảnh thiên nhiên xanh mát tại Việt Nam"
            loading="lazy"
            src="https://images.unsplash.com/photo-1501785888041-af3ef285b470?auto=format&fit=crop&w=1200&q=85"
          />
          <div className="audio-preview-card">
            <span className="audio-art">
              <PlayIcon />
            </span>
            <span className="audio-preview-copy">
              <strong>{t("home.audioTitle")}</strong>
              <small>{t("home.audioSubtitle")}</small>
            </span>
            <span className="audio-wave" aria-hidden="true">
              {Array.from({ length: 15 }, (_, index) => (
                <i key={index} style={{ height: `${10 + ((index * 17) % 20)}px` }} />
              ))}
            </span>
          </div>
          <span className="story-stamp">QOL<br />AUDIO<br />GUIDE</span>
        </div>
        <div className="story-copy">
          <span className="section-overline">{t("home.storyEyebrow")}</span>
          <h2>{t("home.storyTitle")}</h2>
          <p>{t("home.storyDescription")}</p>
          <ul className="story-benefits">
            <li><span>01</span> {t("home.benefitOne")}</li>
            <li><span>02</span> {t("home.benefitTwo")}</li>
            <li><span>03</span> {t("home.benefitThree")}</li>
          </ul>
          <Link className="button-primary" to="/explore">
            {t("home.storyCta")} <ArrowIcon />
          </Link>
        </div>
      </section>

      <section className="membership-section">
        <div className="membership-copy">
          <span className="section-overline">{t("home.membershipEyebrow")}</span>
          <h2>{t("home.membershipTitle")}</h2>
          <p>{t("home.membershipDescription")}</p>
          <Link className="button-light" to="/plans">
            {t("home.membershipCta")} <ArrowIcon />
          </Link>
        </div>
        <Link className="membership-card" to="/plans">
          <span className="membership-icon">✳</span>
          <span className="section-overline">{t("home.membershipCardEyebrow")}</span>
          <strong>{t("home.membershipCardTitle")}</strong>
          <span>{t("home.membershipCardDescription")}</span>
          <div className="membership-card-bottom">
            <span>{t("home.membershipCardFooter")}</span>
            <ArrowIcon />
          </div>
        </Link>
      </section>

      <section className="closing-cta">
        <span className="section-overline">{t("home.closingEyebrow")}</span>
        <h2>{t("home.closingTitle")}</h2>
        <Link className="button-primary" to="/explore">
          {t("home.closingCta")} <ArrowIcon />
        </Link>
      </section>
    </div>
  );
}
