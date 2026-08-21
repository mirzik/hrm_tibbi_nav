import Link from "next/link";
import { fetchMyTickets } from "@/lib/api";
import { getDevUserEmail } from "@/lib/session";
import { TICKET_CATEGORY_LABELS, TICKET_STATUS_LABELS, label } from "@/lib/labels";
import TicketForm from "./TicketForm";

export default async function TicketsPage() {
  const email = (await getDevUserEmail())!;
  const tickets = await fetchMyTickets(email);

  return (
    <div>
      <h1 style={{ marginTop: 0 }}>Мои тикеты</h1>

      <section style={cardStyle}>
        <h2 style={h2Style}>Создать тикет в HR Service Desk</h2>
        <TicketForm />
      </section>

      <section style={cardStyle}>
        <h2 style={h2Style}>Мои обращения</h2>
        {tickets.length === 0 ? (
          <p style={{ color: "#888" }}>Тикетов пока нет.</p>
        ) : (
          <table style={{ width: "100%" }}>
            <thead>
              <tr style={{ textAlign: "left", borderBottom: "1px solid #e5e7eb" }}>
                <th style={thStyle}>Тема</th>
                <th style={thStyle}>Категория</th>
                <th style={thStyle}>Статус</th>
                <th style={thStyle}>Создан</th>
              </tr>
            </thead>
            <tbody>
              {tickets.map((t) => (
                <tr key={t.id} style={{ borderTop: "1px solid #f0f0f0" }}>
                  <td style={tdStyle}>
                    <Link href={`/portal/tickets/${t.id}`} style={{ color: "#2563eb", textDecoration: "none" }}>
                      {t.subject}
                    </Link>
                  </td>
                  <td style={tdStyle}>{label(TICKET_CATEGORY_LABELS, t.category)}</td>
                  <td style={tdStyle}>{label(TICKET_STATUS_LABELS, t.status)}</td>
                  <td style={tdStyle}>{new Date(t.createdAtUtc).toLocaleDateString("ru-RU")}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>
    </div>
  );
}

const cardStyle: React.CSSProperties = {
  background: "#fff",
  border: "1px solid #e5e7eb",
  borderRadius: 8,
  padding: 20,
  marginBottom: 20,
  maxWidth: 640,
};
const h2Style: React.CSSProperties = { fontSize: 16, marginTop: 0, marginBottom: 12 };
const thStyle: React.CSSProperties = { padding: "6px 8px", fontSize: 13, color: "#6b7280" };
const tdStyle: React.CSSProperties = { padding: "6px 8px", fontSize: 14 };
