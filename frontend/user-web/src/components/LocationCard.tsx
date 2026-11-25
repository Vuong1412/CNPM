import { Link } from "react-router-dom";
import type { LocationSummary } from "@qol/shared";
import { getMediaUrl } from "../services/api";

interface LocationCardProps {
  location: LocationSummary;
}

export function LocationCard({ location }: LocationCardProps) {
  const image = location.images[0];
  return (
    <Link className="location-card" to={`/locations/${encodeURIComponent(location.id)}`}>
      <img
        alt={image?.altText ?? ""}
        className="location-card-image"
        loading="lazy"
        onError={(event) => {
          if (!event.currentTarget.src.endsWith("/destination-placeholder.svg")) {
            event.currentTarget.src = "/destination-placeholder.svg";
          }
        }}
        src={image ? getMediaUrl(image.imageUrl) : "/destination-placeholder.svg"}
      />
      <span className="location-card-content">
        <strong>{location.name}</strong>
        <span>{location.address}</span>
      </span>
    </Link>
  );
}
