import { fetchEmployees } from "@/lib/api";

export default async function EmployeesPage() {
  const employees = await fetchEmployees();

  return (
    <main style={{ padding: 24, fontFamily: "system-ui" }}>
      <h1>Сотрудники — Tibbi Nav HRM</h1>
      <table style={{ width: "100%", borderCollapse: "collapse", marginTop: 16 }}>
        <thead>
          <tr style={{ textAlign: "left", borderBottom: "1px solid #ddd" }}>
            <th>Employee ID</th>
            <th>ФИО</th>
            <th>Статус</th>
          </tr>
        </thead>
        <tbody>
          {employees.map((e) => (
            <tr key={e.id} style={{ borderBottom: "1px solid #f0f0f0" }}>
              <td>{e.employeeCode}</td>
              <td>{e.fullName}</td>
              <td>{e.status}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </main>
  );
}
