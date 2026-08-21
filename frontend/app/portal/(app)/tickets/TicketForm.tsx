"use client";

import { useActionState } from "react";
import { TICKET_CATEGORY_LABELS } from "@/lib/labels";
import { createTicketAction, type CreateTicketState } from "./actions";

const initialState: CreateTicketState = null;

export default function TicketForm() {
  const [state, formAction, pending] = useActionState(createTicketAction, initialState);

  return (
    <form action={formAction} style={{ display: "flex", flexDirection: "column", gap: 8, maxWidth: 420 }}>
      <label style={labelStyle}>
        Категория
        <select name="category" required style={inputStyle} defaultValue="">
          <option value="" disabled>
            Выберите категорию
          </option>
          {Object.entries(TICKET_CATEGORY_LABELS).map(([value, text]) => (
            <option key={value} value={value}>
              {text}
            </option>
          ))}
        </select>
      </label>
      <label style={labelStyle}>
        Тема
        <input type="text" name="subject" required style={inputStyle} />
      </label>
      <label style={labelStyle}>
        Описание
        <textarea name="description" rows={4} style={inputStyle} />
      </label>

      <button
        type="submit"
        disabled={pending}
        style={{ marginTop: 4, padding: "8px 12px", background: "#2563eb", color: "#fff", border: "none", borderRadius: 6, opacity: pending ? 0.6 : 1 }}
      >
        {pending ? "Отправка..." : "Создать тикет"}
      </button>

      {state?.error && <p style={{ color: "#b91c1c", fontSize: 13 }}>{state.error}</p>}
    </form>
  );
}

const labelStyle: React.CSSProperties = { display: "flex", flexDirection: "column", gap: 4, fontSize: 13, color: "#374151" };
const inputStyle: React.CSSProperties = { padding: "6px 8px", border: "1px solid #ccc", borderRadius: 6, fontSize: 14 };
