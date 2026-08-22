import { NextRequest, NextResponse } from "next/server";
import { getDevUserEmail } from "@/lib/session";

const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:8080/api/v1";

/**
 * Тот же приём, что и в Employee Portal (app/portal/(app)/documents/[id]/file/route.ts):
 * обычная <a href> не может добавить заголовок X-Dev-User, поэтому Next.js-сервер
 * проксирует запрос, подставляя его из dev-cookie.
 */
export async function GET(request: NextRequest, context: { params: Promise<{ id: string; docId: string }> }) {
  const email = await getDevUserEmail();
  if (!email) return NextResponse.json({ error: "Не авторизован" }, { status: 401 });

  const { docId } = await context.params;
  const format = request.nextUrl.searchParams.get("format") ?? "docx";

  const upstream = await fetch(`${API_URL}/employee-documents/${docId}/file?format=${format}`, {
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
