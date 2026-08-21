import Link from "next/link";
import { redirect } from "next/navigation";
import { getDevUserEmail } from "@/lib/session";
import { logoutAction } from "./actions";

const NAV_ITEMS = [
  { href: "/portal/profile", label: "Мой профиль" },
  { href: "/portal/documents", label: "Мои документы" },
  { href: "/portal/leave", label: "Мои отпуска" },
  { href: "/portal/tickets", label: "Мои тикеты" },
];

/**
 * Раздел 53: Employee Portal — отдельный layout от HR Workspace (/employees),
 * рассчитан на рядового сотрудника (Scope=Self, см. backend MeController).
 */
export default async function PortalLayout({ children }: { children: React.ReactNode }) {
  const email = await getDevUserEmail();
  if (!email) redirect("/portal/login");

  return (
    <div style={{ display: "flex", minHeight: "100vh", fontFamily: "system-ui" }}>
      <aside style={{ width: 220, background: "#111827", color: "#e5e7eb", padding: "24px 16px", flexShrink: 0 }}>
        <div style={{ fontWeight: 700, fontSize: 16, marginBottom: 4 }}>Tibbi Nav</div>
        <div style={{ fontSize: 12, color: "#9ca3af", marginBottom: 24 }}>Employee Portal</div>

        <nav style={{ display: "flex", flexDirection: "column", gap: 4 }}>
          {NAV_ITEMS.map((item) => (
            <Link
              key={item.href}
              href={item.href}
              style={{ color: "#e5e7eb", textDecoration: "none", padding: "8px 10px", borderRadius: 6, fontSize: 14 }}
            >
              {item.label}
            </Link>
          ))}
        </nav>

        <div style={{ marginTop: 32, paddingTop: 16, borderTop: "1px solid #374151" }}>
          <div style={{ fontSize: 12, color: "#9ca3af", marginBottom: 8, wordBreak: "break-all" }}>{email}</div>
          <form action={logoutAction}>
            <button
              type="submit"
              style={{ background: "none", border: "1px solid #4b5563", color: "#e5e7eb", borderRadius: 6, padding: "6px 10px", fontSize: 13 }}
            >
              Выйти
            </button>
          </form>
        </div>
      </aside>

      <main style={{ flex: 1, padding: 32, background: "#f9fafb" }}>{children}</main>
    </div>
  );
}
