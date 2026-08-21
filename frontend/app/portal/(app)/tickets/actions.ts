"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";
import { addMyTicketComment, createMyTicket } from "@/lib/api";
import { getDevUserEmail } from "@/lib/session";

export type CreateTicketState = { error?: string } | null;

export async function createTicketAction(_prev: CreateTicketState, formData: FormData): Promise<CreateTicketState> {
  const email = await getDevUserEmail();
  if (!email) return { error: "Не авторизован." };

  const category = Number(formData.get("category"));
  const subject = String(formData.get("subject") ?? "").trim();
  const description = String(formData.get("description") ?? "").trim();

  if (!subject) return { error: "Укажите тему тикета." };

  let ticketId: string;
  try {
    const ticket = await createMyTicket(email, { category, subject, description });
    ticketId = ticket.id;
  } catch (err) {
    return { error: err instanceof Error ? err.message : "Не удалось создать тикет." };
  }

  revalidatePath("/portal/tickets");
  redirect(`/portal/tickets/${ticketId}`);
}

export type AddCommentState = { error?: string } | null;

export async function addCommentAction(ticketId: string, _prev: AddCommentState, formData: FormData): Promise<AddCommentState> {
  const email = await getDevUserEmail();
  if (!email) return { error: "Не авторизован." };

  const text = String(formData.get("text") ?? "").trim();
  if (!text) return { error: "Введите текст комментария." };

  try {
    await addMyTicketComment(email, ticketId, text);
  } catch (err) {
    return { error: err instanceof Error ? err.message : "Не удалось отправить комментарий." };
  }

  revalidatePath(`/portal/tickets/${ticketId}`);
  return null;
}
