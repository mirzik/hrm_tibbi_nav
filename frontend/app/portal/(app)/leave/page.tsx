import { fetchMyLeaveBalance, fetchMyLeaveRequests } from "@/lib/api";
import { getDevUserEmail } from "@/lib/session";
import { LEAVE_STATUS_LABELS, label } from "@/lib/labels";
import LeaveRequestForm from "./LeaveRequestForm";

export default async function LeavePage() {
  const email = (await getDevUserEmail())!;
  const [balance, requests] = await Promise.all([fetchMyLeaveBalance(email), fetchMyLeaveRequests(email)]);

  return (
    <div>
      <h1 style={{ marginTop: 0 }}>Мои отпуска</h1>

      <section style={cardStyle}>
        <h2 style={h2Style}>
          Баланс на {balance.year} год ({balance.leaveType === "Annual" ? "ежегодный" : balance.leaveType})
        </h2>
        <div style={{ display: "flex", gap: 24, fontSize: 14 }}>
          <Stat label="Положено" value={balance.entitlementDays} />
          <Stat label="Использовано" value={balance.usedDays} />
          <Stat label="Ожидает решения" value={balance.pendingDays} />
          <Stat label="Остаток" value={balance.remainingDays} highlight />
        </div>
      </section>

      <section style={cardStyle}>
        <h2 style={h2Style}>Подать заявку на отпуск</h2>
        <LeaveRequestForm />
      </section>

      <section style={cardStyle}>
        <h2 style={h2Style}>История заявок</h2>
        {requests.length === 0 ? (
          <p style={{ color: "#888" }}>Заявок пока нет.</p>
        ) : (
          <table style={{ width: "100%" }}>
            <thead>
              <tr style={{ textAlign: "left", borderBottom: "1px solid #e5e7eb" }}>
                <th style={thStyle}>Период</th>
                <th style={thStyle}>Дней</th>
                <th style={thStyle}>Статус</th>
                <th style={thStyle}>Комментарий</th>
              </tr>
            </thead>
            <tbody>
              {requests.map((r) => (
                <tr key={r.id} style={{ borderTop: "1px solid #f0f0f0" }}>
                  <td style={tdStyle}>
                    {r.startDate} — {r.endDate}
                  </td>
                  <td style={tdStyle}>{r.days}</td>
                  <td style={tdStyle}>{label(LEAVE_STATUS_LABELS, r.status)}</td>
                  <td style={tdStyle}>{r.comment ?? "—"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>
    </div>
  );
}

function Stat({ label: text, value, highlight }: { label: string; value: number; highlight?: boolean }) {
  return (
    <div>
      <div style={{ fontSize: 12, color: "#6b7280" }}>{text}</div>
      <div style={{ fontSize: 22, fontWeight: 700, color: highlight ? "#2563eb" : "#111827" }}>{value}</div>
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
