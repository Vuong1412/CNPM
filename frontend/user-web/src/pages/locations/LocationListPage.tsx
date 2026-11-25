import { useEffect, useState, type FormEvent } from "react";
import { useSearchParams } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { LocationCard } from "../../components/LocationCard";
import { useLocations } from "../../hooks/useLocations";

export function LocationListPage() {
  const { t } = useTranslation();
  const { locations, isLoading, error } = useLocations();
  const [searchParams, setSearchParams] = useSearchParams();
  const query = searchParams.get("q") ?? "";
  const [search, setSearch] = useState(query);
  const filteredLocations = locations.filter((location) =>
    `${location.name} ${location.address}`
      .toLocaleLowerCase()
      .includes(query.toLocaleLowerCase())
  );

  useEffect(() => {
    setSearch(query);
  }, [query]);

  function handleSearch(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const nextParams = new URLSearchParams(searchParams);
    if (search.trim()) {
      nextParams.set("q", search.trim());
    } else {
      nextParams.delete("q");
    }
    setSearchParams(nextParams);
  }

  return (
    <section className="content-page">
      <p className="eyebrow">{t("explore.eyebrow")}</p>
      <h1>{t("explore.title")}</h1>
      <p>{t("explore.description")}</p>
      <form className="explore-search" onSubmit={handleSearch}>
        <label className="sr-only" htmlFor="explore-search">
          {t("explore.searchPlaceholder")}
        </label>
        <input
          id="explore-search"
          onChange={(event) => setSearch(event.target.value)}
          placeholder={t("explore.searchPlaceholder")}
          value={search}
        />
        <button type="submit">{t("home.search")}</button>
      </form>
      {isLoading && <p role="status">{t("common.loading")}</p>}
      {error && <p role="alert">{t("explore.error", { message: error })}</p>}
      {!isLoading && !error && filteredLocations.length === 0 && (
        <p className="explore-empty">
          {locations.length === 0 ? t("explore.empty") : t("explore.noResults")}
        </p>
      )}
      <div className="location-grid">
        {filteredLocations.map((location) => (
          <LocationCard key={location.id} location={location} />
        ))}
      </div>
    </section>
  );
}
