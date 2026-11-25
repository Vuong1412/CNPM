export interface LocationReview {
  id: string;
  displayName: string;
  rating: number;
  comment: string | null;
  createdAt: string;
}

export interface AdminLocationReview extends LocationReview {
  locationId: string;
  locationName: string;
  userEmail: string;
  isVisible: boolean;
}
