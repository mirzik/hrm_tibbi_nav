import Link from "next/link";
import { ApiError, fetchEmployees } from "@/lib/api";
import { getDevUserEmail } from "@/lib/session";
import { EMPLOYEE_STATUS_LABELS, label } from "@/lib/labels";

export default async function EmployeesPage() {
  const email = (await getDevUserEmail())!; // layout уже гарантирует наличие

  let employees;
  try {
    employees = await fetchEmployees(email);
  } catch (err) {
    if (err instanceof ApiError && (err.status === 403 || err.status === 401)) {
      return (
        <main style={{ padding: 24 }}>
          <h1>Сотрудники — Tibbi Nav HRM</h1>
          <p style={{ color: "#b91c1c", background: "#fef2f2", border: "1px solid #fecaca", borderRadius: 6, padding: 12, maxWidth: 560 }}>
            У пользователя <code>{email}</code> нет прав на просмотр списка сотрудников
            (RBAC: нужна роль SuperAdmin, HRManager или DepartmentManager). Сменить
            пользователя можно кнопкой в шапке.
          </p>
        </main>
      );
    }
    throw err;
  }

  return (
    <main style={{ padding: 24 }}>
      <h1>Сотрудники — Tibbi Nav HRM</h1>
      <table style={{ width: "100%", borderCollapse: "collapse", marginTop: 16, background: "#fff" }}>
        <thead>
          <tr style={{ textAlign: "left", borderBottom: "1px solid #ddd" }}>
            <th style={{ padding: 8 }}>Employee ID</th>
            <th style={{ padding: 8 }}>ФИО</th>
            <th style={{ padding: 8 }}>Статус</th>
          </tr>
        </thead>
        <tbody>
          {employees.map((e) => (
            <tr key={e.id} style={{ borderBottom: "1px solid #f0f0f0" }}>
              <td style={{ padding: 8 }}>{e.employeeCode}</td>
              <td style={{ padding: 8 }}>
                <Link href={`/employees/${e.id}`} style={{ color: "#2563eb", textDecoration: "none" }}>
                  {e.fullName}
                </Link>
              </td>
              <td style={{ padding: 8 }}>{label(EMPLOYEE_STATUS_LABELS, e.status)}</td>
            </tr>
          ))}
        </tbody>
      </table>
      {employees.length === 0 && <p style={{ color: "#888", marginTop: 12 }}>Сотрудников не найдено (в scope этой роли).</p>}
    </main>
  );
}
