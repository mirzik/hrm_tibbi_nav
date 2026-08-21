"use client";

import { useActionState } from "react";
import { createLeaveRequestAction, type CreateLeaveState } from "./actions";

const initialState: CreateLeaveState = null;

export default function LeaveRequestForm() {
  const [state, formAction, pending] = useActionState(createLeaveRequestAction, initialState);

  return (
    <form action={formAction} style={{ display: "flex", flexDirection: "column", gap: 8, maxWidth: 360 }}>
      <label style={labelStyle}>
        Дата начала
        <input type="date" name="startDate" required style={inputStyle} />
      </label>
      <label style={labelStyle}>
        Дата окончания
        <input type="date" name="endDate" required style={inputStyle} />
      </label>
      <label style={labelStyle}>
        Количество дней
        <input type="number" name="days" min={1} required style={inputStyle} />
      </label>
      <label style={labelStyle}>
        Комментарий (необязательно)
        <textarea name="comment" rows={2} style={inputStyle} />
      </label>

      <button
        type="submit"
        disabled={pending}
        style={{ marginTop: 4, padding: "8px 12px", background: "#2563eb", color: "#fff", border: "none", borderRadius: 6, opacity: pending ? 0.6 : 1 }}
      >
        {pending ? "Отправка..." : "Подать заявку"}
      </button>

      {state?.error && <p style={{ color: "#b91c1c", fontSize: 13 }}>{state.error}</p>}
      {state?.warning && (
        <p style={{ color: "#92400e", fontSize: 13, background: "#fffbeb", border: "1px solid #fde68a", borderRadius: 6, padding: 8 }}>
          ⚠ {state.warning}
        </p>
      )}
      {!state && pending === false && null}
    </form>
  );
}

const labelStyle: React.CSSProperties = { display: "flex", flexDirection: "column", gap: 4, fontSize: 13, color: "#374151" };
const inputStyle: React.CSSProperties = { padding: "6px 8px", border: "1px solid #ccc", borderRadius: 6, fontSize: 14 };
