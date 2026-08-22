import { redirect } from "next/navigation";
import { getDevUserEmail } from "@/lib/session";
import { logoutAction } from "./actions";

/**
 * HR Workspace — использует тот же dev-cookie, что и Employee Portal (см.
 * lib/session.ts), но это отдельная поверхность приложения (не Employee
 * Portal): здесь ходят HR/менеджеры, доступ к разделам решает RBAC на
 * backend'е (ScopeFilterMiddleware), а не этот layout.
 */
export default async function EmployeesLayout({ children }: { children: React.ReactNode }) {
  const email = await getDevUserEmail();
  if (!email) redirect("/portal/login?next=/employees");

  return (
    <div style={{ fontFamily: "system-ui", minHeight: "100vh", background: "#f9fafb" }}>
      <header
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          padding: "10px 24px",
          background: "#111827",
          color: "#e5e7eb",
        }}
      >
        <div style={{ fontWeight: 700 }}>Tibbi Nav HRM — HR Workspace</div>
        <div style={{ display: "flex", alignItems: "center", gap: 16, fontSize: 13 }}>
          <span style={{ color: "#9ca3af" }}>{email}</span>
          <form action={logoutAction}>
            <button
              type="submit"
              style={{ background: "none", border: "1px solid #4b5563", color: "#e5e7eb", borderRadius: 6, padding: "4px 10px", fontSize: 13 }}
            >
              Сменить пользователя
            </button>
          </form>
        </div>
      </header>
      {children}
    </div>
  );
}
