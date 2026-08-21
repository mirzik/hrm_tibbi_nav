"use server";

import { revalidatePath } from "next/cache";
import { createMyLeaveRequest } from "@/lib/api";
import { getDevUserEmail } from "@/lib/session";

export type CreateLeaveState = { error?: string; warning?: string } | null;

export async function createLeaveRequestAction(_prev: CreateLeaveState, formData: FormData): Promise<CreateLeaveState> {
  const email = await getDevUserEmail();
  if (!email) return { error: "Не авторизован." };

  const startDate = String(formData.get("startDate") ?? "");
  const endDate = String(formData.get("endDate") ?? "");
  const daysRaw = String(formData.get("days") ?? "");
  const comment = String(formData.get("comment") ?? "");
  const days = Number(daysRaw);

  if (!startDate || !endDate || !Number.isFinite(days) || days <= 0) {
    return { error: "Заполните дату начала, окончания и количество дней." };
  }

  try {
    const result = await createMyLeaveRequest(email, {
      leaveType: "Annual",
      startDate,
      endDate,
      days,
      comment: comment || undefined,
    });
    revalidatePath("/portal/leave");
    return result.conflictWarning.hasConflict ? { warning: result.conflictWarning.message } : null;
  } catch (err) {
    return { error: err instanceof Error ? err.message : "Не удалось создать заявку." };
  }
}
