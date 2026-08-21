import Link from "next/link";

/**
 * Корневая страница-развилка: HR Workspace (для HR/менеджеров) vs Employee Portal
 * (Self Service для рядовых сотрудников, раздел 53-54 ТЗ) — два отдельных layout'а.
 */
export default function HomePage() {
  return (
    <div
      style={{
        minHeight: "100vh",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        fontFamily: "system-ui",
        background: "#f9fafb",
      }}
    >
      <div style={{ textAlign: "center" }}>
        <h1 style={{ marginBottom: 8 }}>Tibbi Nav HRM</h1>
        <p style={{ color: "#6b7280", marginBottom: 32 }}>Выберите режим работы</p>

        <div style={{ display: "flex", gap: 16, justifyContent: "center" }}>
          <Link
            href="/employees"
            style={{
              display: "block",
              padding: "16px 24px",
              background: "#fff",
              border: "1px solid #e5e7eb",
              borderRadius: 8,
              textDecoration: "none",
              color: "#111827",
              minWidth: 200,
            }}
          >
            <div style={{ fontWeight: 700, marginBottom: 4 }}>HR Workspace</div>
            <div style={{ fontSize: 13, color: "#6b7280" }}>Для HR-специалистов и менеджеров</div>
          </Link>

          <Link
            href="/portal"
            style={{
              display: "block",
              padding: "16px 24px",
              background: "#fff",
              border: "1px solid #e5e7eb",
              borderRadius: 8,
              textDecoration: "none",
              color: "#111827",
              minWidth: 200,
            }}
          >
            <div style={{ fontWeight: 700, marginBottom: 4 }}>Employee Portal</div>
            <div style={{ fontSize: 13, color: "#6b7280" }}>Личный кабинет сотрудника (Self Service)</div>
          </Link>
        </div>
      </div>
    </div>
  );
}
