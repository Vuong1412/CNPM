export interface AuthUser {
  id: string;
  email: string;
  displayName: string;
  role: "User" | "Admin";
  createdAt: string;
}
