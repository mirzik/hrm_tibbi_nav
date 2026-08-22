"use server";

import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { DEV_USER_COOKIE } from "@/lib/session";

export async function loginAction(formData: FormData) {
  const email = String(formData.get("email") ?? "").trim();
  if (!email) return;

  const next = String(formData.get("next") ?? "").trim();

  const store = await cookies();
  store.set(DEV_USER_COOKIE, email, { httpOnly: true, sameSite: "lax", path: "/" });
  redirect(next || "/portal");
}
