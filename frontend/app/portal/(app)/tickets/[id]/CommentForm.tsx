"use client";

import { useActionState } from "react";
import { addCommentAction, type AddCommentState } from "../actions";

const initialState: AddCommentState = null;

export default function CommentForm({ ticketId }: { ticketId: string }) {
  const boundAction = addCommentAction.bind(null, ticketId);
  const [state, formAction, pending] = useActionState(boundAction, initialState);

  return (
    <form action={formAction} style={{ display: "flex", flexDirection: "column", gap: 8, maxWidth: 480 }}>
      <textarea name="text" rows={3} required style={inputStyle} placeholder="Ваш комментарий..." />
      <button
        type="submit"
        disabled={pending}
        style={{
          alignSelf: "flex-start",
          padding: "6px 12px",
          background: "#2563eb",
          color: "#fff",
          border: "none",
          borderRadius: 6,
          opacity: pending ? 0.6 : 1,
        }}
      >
        {pending ? "Отправка..." : "Отправить"}
      </button>
      {state?.error && <p style={{ color: "#b91c1c", fontSize: 13 }}>{state.error}</p>}
    </form>
  );
}

const inputStyle: React.CSSProperties = { padding: "6px 8px", border: "1px solid #ccc", borderRadius: 6, fontSize: 14 };
