import { NavLink } from "react-router-dom";
import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { useState } from "react";
import { useAuth } from "../context/AuthContext";

interface SiteLayoutProps {
  children: ReactNode;
}

const navigation = [
  { to: "/", key: "nav.home" },
  { to: "/explore", key: "nav.explore" },
  { to: "/languages", key: "nav.languages" },
  { to: "/plans", key: "nav.plans" },
  { to: "/favorites", key: "nav.favorites" }
];

export function SiteLayout({ children }: SiteLayoutProps) {
  const { t } = useTranslation();
  const { user, logout, error: authError } = useAuth();
  const [logoutError, setLogoutError] = useState<string | null>(null);

  async function handleLogout() {
    try {
      await logout();
      setLogoutError(null);
    } catch (cause) {
      setLogoutError(cause instanceof Error ? cause.message : t("auth.logoutError"));
    }
  }

  return (
    <div className="app-shell">
      <header className="site-header">
        <NavLink className="brand" to="/" aria-label="QOL - Trang chủ">
          <span className="brand-mark">q</span>
          <span className="brand-name">QOL<span>.</span></span>
        </NavLink>
        <nav aria-label="Điều hướng chính">
          {navigation.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.to === "/"}
              className={({ isActive }) =>
                isActive ? "nav-link active" : "nav-link"
              }
            >
              {t(item.key)}
            </NavLink>
          ))}
        </nav>
        <div className="header-actions">
          {user ? (
            <>
              {user.role === "Admin" && (
                <NavLink className="header-history" to="/admin">
                  {t("nav.admin")}
                </NavLink>
              )}
              <NavLink className="header-history" to="/listening-history">
                {t("nav.history")}
              </NavLink>
              <NavLink className="header-history" to="/orders">
                {t("nav.orders")}
              </NavLink>
              <NavLink className="header-profile" to="/profile">
                {t("nav.profile")}
              </NavLink>
              <button className="header-logout" onClick={() => void handleLogout()} type="button">
                {t("nav.logout")}
              </button>
            </>
          ) : (
            <>
              <NavLink className="header-history" to="/login">
                {t("nav.login")}
              </NavLink>
              <NavLink className="header-profile" to="/register">
                {t("nav.register")}
              </NavLink>
            </>
          )}
        </div>
      </header>
      {(authError || logoutError) && (
        <p className="global-error" role="alert">
          {authError || logoutError}
        </p>
      )}
      <main>{children}</main>
      <footer className="site-footer">
        <NavLink className="brand footer-brand" to="/" aria-label="QOL - Trang chủ">
          <span className="brand-mark">q</span>
          <span className="brand-name">QOL<span>.</span></span>
        </NavLink>
        <span>{t("footer")}</span>
        <div className="footer-links">
          <NavLink to="/languages">{t("nav.languages")}</NavLink>
          <NavLink to="/profile">{t("nav.profile")}</NavLink>
        </div>
      </footer>
    </div>
  );
}
