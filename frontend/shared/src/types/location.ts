export type LocationStatus = "ACTIVE" | "INACTIVE" | "PENDING" | "REJECTED";

export interface LocationImage {
  id: string;
  imageUrl: string;
  altText: string | null;
  displayOrder: number;
  createdAt: string;
}

export interface LocationSummary {
  id: string;
  name: string;
  address: string;
  latitude: number | null;
  longitude: number | null;
  radiusMeters: number;
  priority: number;
  originalDescription: string | null;
  directionLink: string | null;
  status: LocationStatus;
  images: LocationImage[];
}

export interface LocationDetails extends LocationSummary {
  createdAt: string;
  updatedAt: string;
}
