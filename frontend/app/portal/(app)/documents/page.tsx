import { fetchMyDocuments } from "@/lib/api";
import { getDevUserEmail } from "@/lib/session";
import { DOCUMENT_STATUS_LABELS, DOCUMENT_TYPE_LABELS, label } from "@/lib/labels";

export default async function DocumentsPage() {
  const email = (await getDevUserEmail())!;
  const documents = await fetchMyDocuments(email);

  return (
    <div>
      <h1 style={{ marginTop: 0 }}>Мои документы</h1>

      {documents.length === 0 ? (
        <p style={{ color: "#888" }}>Документов пока нет — они генерируются автоматически при найме.</p>
      ) : (
        <table style={{ width: "100%", maxWidth: 720, background: "#fff", border: "1px solid #e5e7eb", borderRadius: 8, overflow: "hidden" }}>
          <thead>
            <tr style={{ textAlign: "left", background: "#f3f4f6" }}>
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
                  {/* Через Route Handler-прокси (app/portal/documents/[id]/file/route.ts) —
                      обычная ссылка не может добавить заголовок X-Dev-User сама. */}
                  <a href={`/portal/documents/${doc.id}/file?format=docx`} target="_blank" rel="noreferrer">
                    DOCX
                  </a>{" "}
                  ·{" "}
                  <a href={`/portal/documents/${doc.id}/file?format=pdf`} target="_blank" rel="noreferrer">
                    PDF
                  </a>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}

const thStyle: React.CSSProperties = { padding: "10px 12px", fontSize: 13, color: "#6b7280" };
const tdStyle: React.CSSProperties = { padding: "10px 12px", fontSize: 14 };
