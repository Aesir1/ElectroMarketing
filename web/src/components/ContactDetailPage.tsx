"use client";

import { useEffect, useState } from "react";
import { graphql } from "@/gql";
import type { ContactDetailQuery } from "@/gql/graphql";
import { execute } from "@/lib/graphql";

const ContactDetailDocument = graphql(`
  query ContactDetail($id: UUID!) {
    contact(id: $id) {
      id
      name
      email
      phone
      phoneE164
      street
      postalCode
      city
      country
      obtainedOn
      emailOptOut
      smsOptOut
      createdAtUtc
    }
    contactHistory(contactId: $id, first: 100) {
      nodes {
        id
        rowNumber
        change
        changes {
          field
          old
          new
        }
        importBatch {
          id
          fileName
          documentId
          createdAtUtc
          createdBy
          category {
            name
          }
        }
      }
    }
  }
`);

const dateTime = new Intl.DateTimeFormat(undefined, { dateStyle: "medium", timeStyle: "short" });

export function ContactDetailPage({ id }: { id: string }) {
  const [data, setData] = useState<ContactDetailQuery | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    execute(ContactDetailDocument, { id }).then(setData, (e: Error) => setError(e.message));
  }, [id]);

  if (error) return <p role="alert">{error}</p>;
  if (!data) return <p>Loading…</p>;
  const c = data.contact;
  if (!c) return <p>Contact not found.</p>;

  const rows: [string, string | null | undefined][] = [
    ["E-Mail", c.email + (c.emailOptOut ? " (unsubscribed)" : "")],
    ["Phone", c.phone ? `${c.phone} (${c.phoneE164})${c.smsOptOut ? " (unsubscribed)" : ""}` : null],
    ["Address", [c.street, [c.postalCode, c.city].filter(Boolean).join(" "), c.country].filter(Boolean).join(", ")],
    ["Obtained", c.obtainedOn == null ? null : String(c.obtainedOn)],
  ];

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-semibold">{c.name ?? c.email ?? c.phone}</h1>
      <dl className="grid grid-cols-[max-content_1fr] gap-x-4 gap-y-1 text-sm">
        {rows.map(([label, value]) => (
          <div key={label} className="contents">
            <dt className="text-muted">{label}</dt>
            <dd>{value || "—"}</dd>
          </div>
        ))}
      </dl>
      <section>
        <h2 className="text-lg font-semibold">Import history</h2>
        <table className="w-full text-sm">
          <thead>
            <tr className="text-left">
              <th className="px-2 py-1">File</th>
              <th className="px-2 py-1">Category</th>
              <th className="px-2 py-1">Imported</th>
              <th className="px-2 py-1">Result</th>
              <th className="px-2 py-1">Changes</th>
            </tr>
          </thead>
          <tbody>
            {data.contactHistory?.nodes?.map((h) => (
              <tr key={h.id} className="border-t border-border align-top">
                <td className="px-2 py-1">
                  {h.importBatch && (
                    <a href={`/api/documents/${h.importBatch.documentId}/content`} className="underline">
                      {h.importBatch.fileName}
                    </a>
                  )}{" "}
                  (row {h.rowNumber})
                </td>
                <td className="px-2 py-1">{h.importBatch?.category?.name}</td>
                <td className="px-2 py-1">
                  {h.importBatch && dateTime.format(new Date(h.importBatch.createdAtUtc))} · {h.importBatch?.createdBy}
                </td>
                <td className="px-2 py-1">{h.change.toLowerCase()}</td>
                <td className="px-2 py-1">
                  {h.changes.map((ch) => (
                    <div key={ch.field}>
                      {ch.field}: {ch.old ?? "—"} → {ch.new ?? "—"}
                    </div>
                  ))}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>
    </div>
  );
}
