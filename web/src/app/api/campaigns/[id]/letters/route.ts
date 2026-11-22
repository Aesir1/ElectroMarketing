import { auth } from "@/auth";

/** Letters PDF: browser → here → Api → S3, like document content. */
export const GET = auth(async (req, { params }) => {
  const session = req.auth;
  if (!session?.accessToken || session.error) return new Response("Not authenticated", { status: 401 });
  const id = ((await params) as { id: string }).id;
  if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(id)) return new Response("Not found", { status: 404 });
  const res = await fetch(`${process.env.API_URL}/campaigns/${encodeURIComponent(id)}/letters`, {
    headers: { authorization: `Bearer ${session.accessToken}` },
    cache: "no-store",
  });
  const headers = new Headers({ "cache-control": "private, no-store" });
  for (const name of ["content-type", "content-length", "content-disposition"]) {
    const value = res.headers.get(name);
    if (value) headers.set(name, value);
  }
  return new Response(res.body, { status: res.status, headers });
});
