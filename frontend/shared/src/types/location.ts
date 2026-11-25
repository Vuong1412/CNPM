export interface LocationSummary {
  id: string;
  name: string;
  address: string;
  isActive: boolean;
}

export interface LocationDetails extends LocationSummary {
  createdAt: string;
}
