export interface SubscriptionPlan {
  id: string;
  name: string;
  description: string;
  price: number;
  currency: string;
  durationDays: number;
  isActive: boolean;
}

export interface SubscriptionOrder {
  id: string;
  planId: string;
  planName: string;
  amount: number;
  currency: string;
  status: "Pending" | "Verified" | "Rejected";
  createdAt: string;
  reviewedAt: string | null;
  reviewNote: string | null;
  manualQrReportedAt: string | null;
}

export interface UserSubscription {
  id: string;
  planName: string;
  startsAt: string;
  endsAt: string;
  isActive: boolean;
}

export interface AdminSubscriptionOrder extends Omit<SubscriptionOrder, "planId"> {
  userEmail: string;
}

export interface AdminDashboard {
  userCount: number;
  activeLocationCount: number;
  activeAudioCount: number;
  activePlanCount: number;
  pendingOrderCount: number;
}
