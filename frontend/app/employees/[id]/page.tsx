import Link from "next/link";
import { notFound } from "next/navigation";
import { ApiError, fetchEmployeeDetail, fetchEmployeeDocuments } from "@/lib/api";
import { getDevUserEmail } from "@/lib/session";
import {
  CREDENTIAL_STATUS_LABELS,
  CREDENTIAL_TYPE_LABELS,
  DOCUMENT_STATUS_LABELS,
  DOCUMENT_TYPE_LABELS,
  EMPLOYEE_STATUS_LABELS,
  EMPLOYMENT_TYPE_LABELS,
  GENDER_LABELS,
  label,
} from "@/lib/labels";

export default async function EmployeeDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const email = (await getDevUserEmail())!;

  let detail;
  try {
    detail = await fetchEmployeeDetail(email, id);
  } catch (err) {
    // Раздел 65: сотрудник за пределами scope отдаётся как 404 — тот же приём,
    // что и в Employee Portal для чужих тикетов (см. app/portal/(app)/tickets/[id]/page.tsx).
    if (err instanceof ApiError && err.status === 404) notFound();
    throw err;
  }

  const documents = await fetchEmployeeDocuments(email, id).catch(() => []);
  const { employee, employmentRecords, medicalCredentials } = detail;
  const current = employmentRecords.find((r) => r.IsCurrent) ?? employmentRecords[0];
  const history = employmentRecords.filter((r) => r.Id !== current?.Id);

  return (
    <main style={{ padding: 24 }}>
      <p>
        <Link href="/employees" style={{ color: "#2563eb", textDecoration: "none", fontSize: 13 }}>
          ← Сотрудники
        </Link>
      </p>
      <h1 style={{ marginTop: 8 }}>{employee.FullName}</h1>

      <section style={cardStyle}>
        <h2 style={h2Style}>Профиль</h2>
        <dl style={dlStyle}>
          <Row term="Табельный номер" value={employee.EmployeeCode} />
          <Row term="Статус" value={label(EMPLOYEE_STATUS_LABELS, employee.Status)} />
          <Row term="Пол" value={label(GENDER_LABELS, employee.Gender)} />
          <Row term="Дата рождения" value={employee.DateOfBirth} />
          <Row term="Гражданство" value={employee.Citizenship} />
          <Row term="Телефон" value={employee.Phone ?? "—"} />
          <Row term="Личная почта" value={employee.PersonalEmail ?? "—"} />
          <Row term="Корпоративная почта" value={employee.CorporateEmail ?? "—"} />
          <Row term="Адрес" value={employee.Address ?? "—"} />
        </dl>
      </section>

      <section style={cardStyle}>
        <h2 style={h2Style}>Занятость</h2>
        {current ? (
          <>
            <dl style={dlStyle}>
              <Row term="Подразделение" value={current.DepartmentName ?? "—"} />
              <Row term="Должность" value={current.PositionTitle ?? "—"} />
              <Row term="Руководитель" value={current.ManagerFullName ?? "—"} />
              <Row term="Ставка (FTE)" value={String(current.Fte)} />
              <Row term="Вид занятости" value={label(EMPLOYMENT_TYPE_LABELS, current.EmploymentType)} />
              <Row term="Дата приёма" value={current.HireDate} />
              {current.BaseSalary !== undefined && <Row term="Оклад" value={current.BaseSalary != null ? String(current.BaseSalary) : "—"} />}
            </dl>
            {history.length > 0 && (
              <>
                <h3 style={{ fontSize: 13, color: "#6b7280", marginTop: 16, marginBottom: 8 }}>История</h3>
                <table style={{ width: "100%", fontSize: 13 }}>
                  <thead>
                    <tr style={{ textAlign: "left", color: "#6b7280" }}>
                      <th style={thStyle}>Период</th>
                      <th style={thStyle}>Подразделение</th>
                      <th style={thStyle}>Должность</th>
                    </tr>
                  </thead>
                  <tbody>
                    {history.map((r) => (
                      <tr key={r.Id} style={{ borderTop: "1px solid #f0f0f0" }}>
                        <td style={tdStyle}>
                          {r.EffectiveFrom} — {r.EffectiveTo ?? "н.в."}
                        </td>
                        <td style={tdStyle}>{r.DepartmentName ?? "—"}</td>
                        <td style={tdStyle}>{r.PositionTitle ?? "—"}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </>
            )}
          </>
        ) : (
          <p style={{ color: "#888" }}>Записи трудоустройства не найдены.</p>
        )}
      </section>

      <section style={cardStyle}>
        <h2 style={h2Style}>Медицинские допуски</h2>
        {medicalCredentials.length === 0 ? (
          <p style={{ color: "#888" }}>Допусков не найдено.</p>
        ) : (
          <table style={{ width: "100%", fontSize: 14 }}>
            <thead>
              <tr style={{ textAlign: "left", borderBottom: "1px solid #e5e7eb" }}>
                <th style={thStyle}>Название</th>
                <th style={thStyle}>Тип</th>
                <th style={thStyle}>Выдан</th>
                <th style={thStyle}>Истекает</th>
                <th style={thStyle}>Статус</th>
              </tr>
            </thead>
            <tbody>
              {medicalCredentials.map((c) => (
                <tr key={c.Id} style={{ borderTop: "1px solid #f0f0f0" }}>
                  <td style={tdStyle}>{c.Title}</td>
                  <td style={tdStyle}>{label(CREDENTIAL_TYPE_LABELS, c.Type)}</td>
                  <td style={tdStyle}>{c.IssueDate}</td>
                  <td style={tdStyle}>{c.ExpiryDate ?? "бессрочно"}</td>
                  <td style={tdStyle}>{label(CREDENTIAL_STATUS_LABELS, c.Status)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>

      <section style={cardStyle}>
        <h2 style={h2Style}>Документы</h2>
        {documents.length === 0 ? (
          <p style={{ color: "#888" }}>Документов не найдено.</p>
        ) : (
          <table style={{ width: "100%", fontSize: 14 }}>
            <thead>
              <tr style={{ textAlign: "left", borderBottom: "1px solid #e5e7eb" }}>
                <th style={thStyle}>Документ</th>
                <th style={thStyle}>Тип</th>
                <th style={thStyle}>Статус</th>
                <th style={thStyle}>Дата</th>
                <th style={thStyle}></th>
              </tr>
            </thead>
            <tbody>
              {documents.map((doc) => (
                <tr key={doc.id} style={{ borderTop: "1px solid #f0f0f0" }}>
                  <td style={tdStyle}>{doc.title}</td>
                  <td style={tdStyle}>{label(DOCUMENT_TYPE_LABELS, doc.documentType)}</td>
                  <td style={tdStyle}>{label(DOCUMENT_STATUS_LABELS, doc.status)}</td>
                  <td style={tdStyle}>{new Date(doc.generatedAtUtc).toLocaleDateString("ru-RU")}</td>
                  <td style={tdStyle}>
                    {doc.hasDocx && (
                      <a href={`/employees/${id}/documents/${doc.id}/file?format=docx`} target="_blank" rel="noreferrer">
                        DOCX
                      </a>
                    )}
                    {doc.hasDocx && doc.hasPdf && " · "}
                    {doc.hasPdf && (
                      <a href={`/employees/${id}/documents/${doc.id}/file?format=pdf`} target="_blank" rel="noreferrer">
                        PDF
                      </a>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>
    </main>
  );
}

function Row({ term, value }: { term: string; value: string }) {
  return (
    <>
      <dt style={dtStyle}>{term}</dt>
      <dd style={ddStyle}>{value}</dd>
    </>
  );
}

const cardStyle: React.CSSProperties = {
  background: "#fff",
  border: "1px solid #e5e7eb",
  borderRadius: 8,
  padding: 20,
  marginBottom: 20,
  maxWidth: 720,
};
const h2Style: React.CSSProperties = { fontSize: 16, marginTop: 0, marginBottom: 12 };
const dlStyle: React.CSSProperties = { display: "grid", gridTemplateColumns: "200px 1fr", rowGap: 8, columnGap: 12, margin: 0 };
const dtStyle: React.CSSProperties = { color: "#6b7280", fontSize: 13 };
const ddStyle: React.CSSProperties = { margin: 0, fontSize: 14 };
const thStyle: React.CSSProperties = { padding: "6px 8px", fontSize: 13, color: "#6b7280" };
const tdStyle: React.CSSProperties = { padding: "6px 8px", fontSize: 14 };
