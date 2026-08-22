import { logoutAction } from "./actions";

/**
 * Показывается, когда текущий dev-пользователь (cookie tibbinav_dev_user)
 * аутентифицирован, но не имеет RBAC-разрешения "SelfService" — например,
 * если по ошибке залогинились под HR-аккаунтом (admin/hr.dus/manager.dus)
 * вместо сотрудника. Это ожидаемое поведение RBAC (Employee Portal — только
 * для роли Employee), но раньше падало необработанным крашем ("Application
 * error") вместо понятного сообщения.
 */
export default function AccessDenied({ email }: { email: string }) {
  return (
    <div style={{ maxWidth: 560 }}>
      <h1 style={{ marginTop: 0 }}>Нет доступа</h1>
      <p style={{ color: "#b91c1c", background: "#fef2f2", border: "1px solid #fecaca", borderRadius: 6, padding: 12 }}>
        У пользователя <code>{email}</code> нет разрешения <code>SelfService</code> — Employee Portal
        предназначен только для роли <b>Employee</b> (рядовой сотрудник). Похоже, вы вошли под HR/менеджерским
        аккаунтом (для них есть отдельный HR Workspace — <code>/employees</code>).
      </p>
      <form action={logoutAction}>
        <button
          type="submit"
          style={{ padding: "8px 12px", background: "#2563eb", color: "#fff", border: "none", borderRadius: 6, fontSize: 14 }}
        >
          Войти под другим пользователем
        </button>
      </form>
    </div>
  );
}
