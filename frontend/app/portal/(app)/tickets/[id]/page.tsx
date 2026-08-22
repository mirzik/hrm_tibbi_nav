import Link from "next/link";
import { notFound } from "next/navigation";
import { ApiError, fetchMyTicket } from "@/lib/api";
import { getDevUserEmail } from "@/lib/session";
import { TICKET_CATEGORY_LABELS, TICKET_STATUS_LABELS, label } from "@/lib/labels";
import CommentForm from "./CommentForm";
import AccessDenied from "../../AccessDenied";

export default async function TicketDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const email = (await getDevUserEmail())!;

  let ticket;
  try {
    ticket = await fetchMyTicket(email, id);
  } catch (err) {
    // Раздел 53/54: чужой тикет (не принадлежащий Self) backend отдаёт 404 — не 403, чтобы не подтверждать его существование.
    if (err instanceof ApiError && err.status === 404) notFound();
    if (err instanceof ApiError && err.status === 403) return <AccessDenied email={email} />;
    throw err;
  }

  return (
    <div>
      <p>
        <Link href="/portal/tickets" style={{ color: "#2563eb", textDecoration: "none", fontSize: 13 }}>
          ← Мои тикеты
        </Link>
      </p>
      <h1 style={{ marginTop: 8 }}>{ticket.subject}</h1>

      <section style={cardStyle}>
        <div style={{ display: "flex", gap: 24, fontSize: 14, marginBottom: 12 }}>
          <Field label="Категория" value={label(TICKET_CATEGORY_LABELS, ticket.category)} />
          <Field label="Статус" value={label(TICKET_STATUS_LABELS, ticket.status)} />
          <Field label="Создан" value={new Date(ticket.createdAtUtc).toLocaleString("ru-RU")} />
        </div>
        {ticket.description && (
          <div>
            <div style={{ fontSize: 12, color: "#6b7280", marginBottom: 4 }}>Описание</div>
            <p style={{ margin: 0, fontSize: 14, whiteSpace: "pre-wrap" }}>{ticket.description}</p>
          </div>
        )}
      </section>

      <section style={cardStyle}>
        <h2 style={h2Style}>Комментарии</h2>
        {ticket.comments.length === 0 ? (
          <p style={{ color: "#888", fontSize: 14 }}>Комментариев пока нет.</p>
        ) : (
          <div style={{ display: "flex", flexDirection: "column", gap: 10, marginBottom: 16 }}>
            {ticket.comments.map((c) => (
              <div key={c.id} style={{ background: "#f9fafb", border: "1px solid #f0f0f0", borderRadius: 6, padding: 10 }}>
                <div style={{ fontSize: 12, color: "#6b7280", marginBottom: 4 }}>
                  {new Date(c.createdAtUtc).toLocaleString("ru-RU")}
                </div>
                <div style={{ fontSize: 14, whiteSpace: "pre-wrap" }}>{c.text}</div>
              </div>
            ))}
          </div>
        )}

        <CommentForm ticketId={ticket.id} />
      </section>
    </div>
  );
}

function Field({ label: text, value }: { label: string; value: string }) {
  return (
    <div>
      <div style={{ fontSize: 12, color: "#6b7280" }}>{text}</div>
      <div style={{ fontSize: 14, fontWeight: 500 }}>{value}</div>
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
