import { loginAction } from "./actions";

const DEMO_ACCOUNTS = [
  { email: "surgeon1.dus@tibbinav.local", label: "Далер Юсупов (врач-хирург)" },
  { email: "surgeon2.dus@tibbinav.local", label: "Нигора Сафарова (врач-хирург)" },
  { email: "surgeon3.dus@tibbinav.local", label: "Комрон Исмоилов (врач-хирург)" },
  { email: "surgeon4.dus@tibbinav.local", label: "Зарина Холова (врач-хирург)" },
];

export default function PortalLoginPage() {
  return (
    <main style={{ maxWidth: 420, margin: "80px auto", padding: 24, fontFamily: "system-ui" }}>
      <h1 style={{ fontSize: 22 }}>Employee Portal — вход</h1>
      <p style={{ color: "#555", fontSize: 14 }}>
        Пока не поднят Keycloak (см. README backend), портал использует dev-режим
        аутентификации backend&apos;а — введите email сотрудника из{" "}
        <code>DevSeedData</code>.
      </p>

      <form action={loginAction} style={{ display: "flex", flexDirection: "column", gap: 8, marginTop: 16 }}>
        <label htmlFor="email" style={{ fontSize: 13, fontWeight: 600 }}>
          Email сотрудника
        </label>
        <input
          id="email"
          name="email"
          type="email"
          required
          placeholder="surgeon1.dus@tibbinav.local"
          style={{ padding: "8px 10px", border: "1px solid #ccc", borderRadius: 6 }}
        />
        <button
          type="submit"
          style={{ marginTop: 8, padding: "8px 12px", background: "#2563eb", color: "#fff", border: "none", borderRadius: 6 }}
        >
          Войти
        </button>
      </form>

      <div style={{ marginTop: 24 }}>
        <p style={{ fontSize: 13, fontWeight: 600, color: "#555" }}>Демо-аккаунты (DevSeedData):</p>
        <ul style={{ paddingLeft: 18, fontSize: 13 }}>
          {DEMO_ACCOUNTS.map((acc) => (
            <li key={acc.email}>
              <form action={loginAction} style={{ display: "inline" }}>
                <input type="hidden" name="email" value={acc.email} />
                <button type="submit" style={{ border: "none", background: "none", color: "#2563eb", padding: 0, textDecoration: "underline" }}>
                  {acc.label}
                </button>
              </form>{" "}
              — <code>{acc.email}</code>
            </li>
          ))}
        </ul>
      </div>
    </main>
  );
}
