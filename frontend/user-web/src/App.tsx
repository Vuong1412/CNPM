import { BrowserRouter } from "react-router-dom";
import { AppRoutes } from "./routes/AppRoutes";
import { SiteLayout } from "./components/SiteLayout";
import { AuthProvider } from "./context/AuthContext";

export function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <SiteLayout>
          <AppRoutes />
        </SiteLayout>
      </AuthProvider>
    </BrowserRouter>
  );
}
