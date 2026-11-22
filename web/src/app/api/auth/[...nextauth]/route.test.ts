import { NextRequest } from "next/server";
import { describe, expect, it, vi } from "vitest";

vi.mock("@/auth", () => {
  const session = () => Response.json({ user: { name: "A" }, accessToken: "a", idToken: "i" });
  return { handlers: { GET: session, POST: session } };
});

import { GET, POST } from "./route";

describe("auth route", () => {
  it.each([
    ["GET", GET],
    ["POST", POST],
  ])("%s /session never returns tokens", async (method, handler) => {
    const res = await handler(new NextRequest("http://x/api/auth/session", { method }));
    expect(await res.json()).toEqual({ user: { name: "A" } });
  });

  it("leaves other auth responses alone", async () => {
    const res = await POST(new NextRequest("http://x/api/auth/signout", { method: "POST" }));
    expect((await res.json()).accessToken).toBe("a");
  });
});
