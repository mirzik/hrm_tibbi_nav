"use server";

import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { DEV_USER_COOKIE } from "@/lib/session";

export async function logoutAction() {
  const store = await cookies();
  store.delete(DEV_USER_COOKIE);
  redirect("/portal/login");
}
