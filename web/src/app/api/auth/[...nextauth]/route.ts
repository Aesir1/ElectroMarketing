import type { NextRequest } from "next/server";
import { handlers } from "@/auth";

/** Same as the Auth.js handler, but the session JSON never carries the access or ID token to browser JavaScript. */
async function stripTokens(req: NextRequest, res: Response) {
  if (!req.nextUrl.pathname.endsWith("/session") || !res.ok) return res;

  const {
    accessToken: _accessToken,
    idToken: _idToken,
    ...session
  } = ((await res.json()) ?? {}) as Record<string, unknown>;
  const headers = new Headers(res.headers);
  headers.delete("content-length");
  return Response.json(session, { status: res.status, headers });
}

export async function GET(req: NextRequest) {
  return stripTokens(req, await handlers.GET(req));
}

export async function POST(req: NextRequest) {
  return stripTokens(req, await handlers.POST(req));
}
