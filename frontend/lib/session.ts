import { cookies } from "next/headers";

/**
 * Раздел 53: пока не поднят Keycloak/OIDC (roadmap-пункт 10 в README backend),
 * Employee Portal использует тот же dev-режим, что и backend
 * (DevHeaderAuthenticationHandler, заголовок X-Dev-User) — "вход" здесь просто
 * запоминает email в httpOnly-cookie на сервере Next.js, и каждый server
 * component/action читает его и подставляет как X-Dev-User при вызове API.
 * Это не настоящая аутентификация, только для локальной разработки/демо.
 */
export const DEV_USER_COOKIE = "tibbinav_dev_user";

export async function getDevUserEmail(): Promise<string | null> {
  const store = await cookies();
  return store.get(DEV_USER_COOKIE)?.value ?? null;
}
