import { Link } from "react-router-dom";
import type { LocationSummary } from "@qol/shared";

interface LocationCardProps {
  location: LocationSummary;
}

export function LocationCard({ location }: LocationCardProps) {
  return (
    <article className="location-card">
      <h2>
        <Link to={`/locations/${encodeURIComponent(location.id)}`}>{location.name}</Link>
      </h2>
      <p>{location.address}</p>
    </article>
  );
}
