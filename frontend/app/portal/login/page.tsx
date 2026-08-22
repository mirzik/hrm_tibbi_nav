import { loginAction } from "./actions";

// Один общий dev-логин на всё приложение (HR Workspace и Employee Portal) —
// в реальности это одна и та же аутентификация (Keycloak, roadmap-пункт 10),
// просто у разных пользователей разные роли/scope в RBAC.
const HR_ACCOUNTS = [
  { email: "admin@tibbinav.local", label: "Суперадминистратор", role: "SuperAdmin — вся организация" },
  { email: "hr.dus@tibbinav.local", label: "HR клиники DUS", role: "HRManager — клиника Душанбе" },
  { email: "manager.dus@tibbinav.local", label: "Фарзона Рахимова", role: "DepartmentManager — свои подчинённые" },
];

const EMPLOYEE_ACCOUNTS = [
  { email: "surgeon1.dus@tibbinav.local", label: "Далер Юсупов (врач-хирург)" },
  { email: "surgeon2.dus@tibbinav.local", label: "Нигора Сафарова (врач-хирург)" },
  { email: "surgeon3.dus@tibbinav.local", label: "Комрон Исмоилов (врач-хирург)" },
  { email: "surgeon4.dus@tibbinav.local", label: "Зарина Холова (врач-хирург)" },
];

export default async function LoginPage({
  searchParams,
}: {
  searchParams: Promise<{ next?: string }>;
}) {
  const { next } = await searchParams;

  return (
    <main style={{ maxWidth: 480, margin: "60px auto", padding: 24, fontFamily: "system-ui" }}>
      <h1 style={{ fontSize: 22 }}>Tibbi Nav HRM — вход</h1>
      <p style={{ color: "#555", fontSize: 14 }}>
        Пока не поднят Keycloak (см. README backend, пункт 10), приложение использует dev-режим
        аутентификации backend&apos;а — введите email пользователя из <code>DevSeedData</code>. Один и
        тот же вход работает и для HR Workspace (<code>/employees</code>), и для Employee Portal
        (<code>/portal</code>) — доступ к разделам определяется ролью пользователя (RBAC).
      </p>

      <form action={loginAction} style={{ display: "flex", flexDirection: "column", gap: 8, marginTop: 16 }}>
        <input type="hidden" name="next" value={next ?? ""} />
        <label htmlFor="email" style={{ fontSize: 13, fontWeight: 600 }}>
          Email пользователя
        </label>
        <input
          id="email"
          name="email"
          type="email"
          required
          placeholder="admin@tibbinav.local"
          style={{ padding: "8px 10px", border: "1px solid #ccc", borderRadius: 6 }}
        />
        <button
          type="submit"
          style={{ marginTop: 8, padding: "8px 12px", background: "#2563eb", color: "#fff", border: "none", borderRadius: 6 }}
        >
          Войти
        </button>
      </form>

      <div style={{ marginTop: 28 }}>
        <p style={{ fontSize: 13, fontWeight: 600, color: "#555" }}>HR Workspace — роли (DevSeedData):</p>
        <ul style={{ paddingLeft: 18, fontSize: 13 }}>
          {HR_ACCOUNTS.map((acc) => (
            <li key={acc.email} style={{ marginBottom: 4 }}>
              <QuickLoginButton email={acc.email} next={next} label={acc.label} />
              {" "}— <code>{acc.email}</code> <span style={{ color: "#888" }}>({acc.role})</span>
            </li>
          ))}
        </ul>
      </div>

      <div style={{ marginTop: 20 }}>
        <p style={{ fontSize: 13, fontWeight: 600, color: "#555" }}>Employee Portal — рядовые сотрудники:</p>
        <ul style={{ paddingLeft: 18, fontSize: 13 }}>
          {EMPLOYEE_ACCOUNTS.map((acc) => (
            <li key={acc.email}>
              <QuickLoginButton email={acc.email} next={next} label={acc.label} /> — <code>{acc.email}</code>
            </li>
          ))}
        </ul>
      </div>
    </main>
  );
}

function QuickLoginButton({ email, next, label }: { email: string; next?: string; label: string }) {
  return (
    <form action={loginAction} style={{ display: "inline" }}>
      <input type="hidden" name="email" value={email} />
      <input type="hidden" name="next" value={next ?? ""} />
      <button type="submit" style={{ border: "none", background: "none", color: "#2563eb", padding: 0, textDecoration: "underline" }}>
        {label}
      </button>
    </form>
  );
}
