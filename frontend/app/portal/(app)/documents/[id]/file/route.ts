import { NextRequest, NextResponse } from "next/server";
import { getDevUserEmail } from "@/lib/session";

const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:8080/api/v1";

/**
 * Раздел 53: браузерная навигация (обычная ссылка <a href>) не может добавить
 * кастомный заголовок X-Dev-User — прокси на стороне Next.js сервера читает
 * dev-сессию из cookie (см. lib/session.ts) и подставляет заголовок сам,
 * прежде чем скачивание дойдёт до пользователя.
 */
export async function GET(request: NextRequest, context: { params: Promise<{ id: string }> }) {
  const email = await getDevUserEmail();
  if (!email) return NextResponse.json({ error: "Не авторизован" }, { status: 401 });

  const { id } = await context.params;
  const format = request.nextUrl.searchParams.get("format") ?? "docx";

  const upstream = await fetch(`${API_URL}/me/documents/${id}/file?format=${format}`, {
    headers: { "X-Dev-User": email },
    cache: "no-store",
  });

  if (!upstream.ok) {
    const text = await upstream.text().catch(() => "");
    return NextResponse.json({ error: text || upstream.statusText }, { status: upstream.status });
  }

  return new NextResponse(upstream.body, {
    status: 200,
    headers: {
      "Content-Type": upstream.headers.get("Content-Type") ?? "application/octet-stream",
      "Content-Disposition": upstream.headers.get("Content-Disposition") ?? "attachment",
    },
  });
}
