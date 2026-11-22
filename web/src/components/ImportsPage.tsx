// web/src/components/ImportsPage.tsx
"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { graphql } from "@/gql";
import type { ImportBatchesQuery } from "@/gql/graphql";
import { execute } from "@/lib/graphql";
import { ImportWizard } from "./ImportWizard";

const ImportBatchesDocument = graphql(`
  query ImportBatches {
    importBatches(first: 50, order: [{ createdAtUtc: DESC }]) {
      nodes {
        id
        fileName
        documentId
        status
        culture
        rowCount
        createdCount
        updatedCount
        unchangedCount
        skippedCount
        errorCount
        createdAtUtc
        createdBy
        category {
          name
        }
      }
    }
  }
`);

const ImportErrorsDocument = graphql(`
  query ImportErrors($id: UUID!) {
    importBatches(where: { id: { eq: $id } }) {
      nodes {
        errors {
          row
          message
        }
      }
    }
  }
`);

type Batch = NonNullable<NonNullable<ImportBatchesQuery["importBatches"]>["nodes"]>[number];

const dateTime = new Intl.DateTimeFormat(undefined, { dateStyle: "medium", timeStyle: "short" });

export function ImportsPage() {
  const [batches, setBatches] = useState<Batch[] | null>(null);
  const [openErrors, setOpenErrors] = useState<{ id: string; errors: { row: number; message: string }[] } | null>(null);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(
    () =>
      execute(ImportBatchesDocument).then(
        (d) => {
          setBatches(d.importBatches?.nodes ?? []);
          setError(null);
        },
        (e: Error) => setError(e.message),
      ),
    [],
  );

  useEffect(() => {
    load();
  }, [load]);

  // Poll while an import is queued or running.
  const busy = batches?.some((b) => b.status === "PENDING" || b.status === "RUNNING");
  useEffect(() => {
    if (!busy) return;
    const timer = setInterval(load, 3000);
    return () => clearInterval(timer);
  }, [busy, load]);

  async function showErrors(id: string) {
    try {
      const d = await execute(ImportErrorsDocument, { id });
      setOpenErrors({ id, errors: d.importBatches?.nodes?.[0]?.errors ?? [] });
    } catch (e) {
      setError((e as Error).message);
    }
  }

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-semibold">Imports</h1>
      <ImportWizard onStarted={load} />
      {error && (
        <p role="alert" className="text-sm text-error">
          {error}
        </p>
      )}
      <table className="w-full text-sm">
        <thead>
          <tr className="text-left">
            <th className="px-2 py-1">File</th>
            <th className="px-2 py-1">Category</th>
            <th className="px-2 py-1">Status</th>
            <th className="px-2 py-1">Rows</th>
            <th className="px-2 py-1">New / updated / unchanged / skipped</th>
            <th className="px-2 py-1">Imported</th>
            <th className="px-2 py-1" />
          </tr>
        </thead>
        <tbody>
          {batches?.map((b) => (
            <tr key={b.id} className="border-t border-border align-top">
              <td className="px-2 py-1">
                <a href={`/api/documents/${b.documentId}/content`} className="underline">
                  {b.fileName}
                </a>
              </td>
              <td className="px-2 py-1">{b.category?.name}</td>
              <td className="px-2 py-1">{b.status.toLowerCase()}</td>
              <td className="px-2 py-1">{b.rowCount}</td>
              <td className="px-2 py-1">
                {b.createdCount} / {b.updatedCount} / {b.unchangedCount} / {b.skippedCount}
              </td>
              <td className="px-2 py-1">
                {dateTime.format(new Date(b.createdAtUtc))} · {b.createdBy}
              </td>
              <td className="space-x-2 px-2 py-1 whitespace-nowrap">
                <Link href={`/app/contacts?batch=${b.id}`} className="underline">
                  Contacts
                </Link>
                {b.errorCount > 0 && (
                  <button type="button" onClick={() => showErrors(b.id)} className="underline">
                    {b.errorCount} warnings
                  </button>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      {openErrors && (
        <section aria-label="Import warnings" className="rounded-lg border border-border p-4">
          <button type="button" onClick={() => setOpenErrors(null)} className="float-right underline">
            Close
          </button>
          <ul className="space-y-1 text-sm">
            {openErrors.errors.map((e, i) => (
              <li key={i}>
                {e.row > 0 ? `Row ${e.row}: ` : ""}
                {e.message}
              </li>
            ))}
          </ul>
        </section>
      )}
    </div>
  );
}
