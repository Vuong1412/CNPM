import { lazy, Suspense } from "react";
import { Route, Routes } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { FavoritesPage } from "../pages/FavoritesPage";
import { HomePage } from "../pages/HomePage";
import { LanguagesPage } from "../pages/LanguagesPage";
import { ListeningHistoryPage } from "../pages/ListeningHistoryPage";
import { ProfilePage } from "../pages/ProfilePage";
import { NotFoundPage } from "../pages/NotFoundPage";
import { AuthPage } from "../pages/AuthPage";
import { BootstrapAdminPage } from "../pages/BootstrapAdminPage";
import { AdminPage } from "../pages/AdminPage";
import { LocationDetailPage } from "../pages/locations/LocationDetailPage";
import { LocationListPage } from "../pages/locations/LocationListPage";
import { PlansPage } from "../pages/PlansPage";
import { OrdersPage } from "../pages/OrdersPage";
import { UnauthorizedPage } from "../pages/UnauthorizedPage";
import { AccessPage } from "../pages/AccessPage";
import { PrivacyPage } from "../pages/PrivacyPage";
import { AdminAccessQrPage } from "../pages/AdminAccessQrPage";
import { PaymentResultPage } from "../pages/PaymentResultPage";
import { ProtectedRoute } from "./ProtectedRoute";

const MapPage = lazy(() =>
  import("../pages/MapPage").then(({ MapPage: page }) => ({ default: page }))
);

function MapPageFallback() {
  const { t } = useTranslation();
  return <section className="content-page"><p role="status">{t("common.loading")}</p></section>;
}

export function AppRoutes() {
  return (
    <Routes>
      <Route path="/" element={<HomePage />} />
      <Route path="/access" element={<AccessPage />} />
      <Route path="/privacy" element={<PrivacyPage />} />
      <Route path="/explore" element={<LocationListPage />} />
      <Route
        path="/map"
        element={(
          <Suspense fallback={<MapPageFallback />}>
            <MapPage />
          </Suspense>
        )}
      />
      <Route path="/locations/:id" element={<LocationDetailPage />} />
      <Route path="/languages" element={<LanguagesPage />} />
      <Route path="/favorites" element={<ProtectedRoute><FavoritesPage /></ProtectedRoute>} />
      <Route path="/listening-history" element={<ProtectedRoute><ListeningHistoryPage /></ProtectedRoute>} />
      <Route path="/plans" element={<PlansPage />} />
      <Route path="/orders" element={<ProtectedRoute><OrdersPage /></ProtectedRoute>} />
      <Route
        path="/payment/result"
        element={<ProtectedRoute><PaymentResultPage /></ProtectedRoute>}
      />
      <Route path="/profile" element={<ProtectedRoute><ProfilePage /></ProtectedRoute>} />
      <Route path="/login" element={<AuthPage mode="login" />} />
      <Route path="/register" element={<AuthPage mode="register" />} />
      <Route path="/bootstrap-admin" element={<BootstrapAdminPage />} />
      <Route path="/admin" element={<ProtectedRoute requireAdmin><AdminPage /></ProtectedRoute>} />
      <Route
        path="/admin/access-qr"
        element={<ProtectedRoute requireAdmin><AdminAccessQrPage /></ProtectedRoute>}
      />
      <Route path="/not-authorized" element={<UnauthorizedPage />} />
      <Route path="*" element={<NotFoundPage />} />
    </Routes>
  );
}
