import { notFound } from "next/navigation";

export const dynamic = "force-dynamic";
export const metadata = { title: "More information", robots: { index: false } };

/** Public page behind every short link and QR code. Content comes from the campaign; the layout gets customized later. */
export default async function Landing({ params }: { params: Promise<{ token: string }> }) {
  const { token } = await params;
  if (!/^[0-9A-Za-z]{7}$/.test(token)) notFound();
  const res = await fetch(`${process.env.API_URL}/landing/${token}`, {
    cache: "no-store",
    headers: { "user-agent": "electromarketing-web" },
  });
  if (!res.ok) notFound();
  const page = (await res.json()) as { title: string; body: string };
  return (
    <main className="mx-auto max-w-2xl px-4 py-12">
      <h1 className="text-3xl font-semibold">{page.title}</h1>
      <p className="mt-6 whitespace-pre-line">{page.body}</p>
    </main>
  );
}
