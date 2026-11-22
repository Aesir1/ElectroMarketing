// web/src/components/ImportWizard.tsx
"use client";

import { useEffect, useState } from "react";
import { graphql } from "@/gql";
import type { PreviewImportMutation } from "@/gql/graphql";
import { formErrors } from "@/lib/errors";
import { execute } from "@/lib/graphql";
import { uploadWithProgress } from "@/lib/upload";

const ImportCategoriesDocument = graphql(`
  query ImportCategories {
    categories {
      id
      name
    }
  }
`);

const CreateImportCategoryDocument = graphql(`
  mutation CreateImportCategory($input: CreateCategoryInput!) {
    createCategory(input: $input) {
      category {
        id
        name
      }
      errors {
        __typename
        ... on Error {
          message
        }
        ... on ValidationError {
          fields {
            field
            messages
          }
        }
      }
    }
  }
`);

const RequestCsvUploadDocument = graphql(`
  mutation RequestCsvUpload($input: RequestDocumentUploadInput!) {
    requestDocumentUpload(input: $input) {
      documentUploadPayload {
        uploadUrl
        document {
          id
        }
      }
      errors {
        __typename
        ... on Error {
          message
        }
        ... on ValidationError {
          fields {
            field
            messages
          }
        }
      }
    }
  }
`);

const ConfirmCsvUploadDocument = graphql(`
  mutation ConfirmCsvUpload($input: ConfirmDocumentUploadInput!) {
    confirmDocumentUpload(input: $input) {
      errors {
        __typename
        ... on Error {
          message
        }
      }
    }
  }
`);

const PreviewImportDocument = graphql(`
  mutation PreviewImport($input: PreviewImportInput!) {
    previewImport(input: $input) {
      csvPreview {
        delimiter
        encoding
        headers
        rows
        suggestedMapping {
          name
          email
          phone
          street
          postalCode
          city
          country
          obtainedOn
        }
      }
      errors {
        __typename
        ... on Error {
          message
        }
      }
    }
  }
`);

const StartImportDocument = graphql(`
  mutation StartImport($input: StartImportInput!) {
    startImport(input: $input) {
      importBatch {
        id
      }
      errors {
        __typename
        ... on Error {
          message
        }
        ... on ValidationError {
          fields {
            field
            messages
          }
        }
      }
    }
  }
`);

const fields = [
  ["name", "Name"],
  ["email", "E-Mail"],
  ["phone", "Phone"],
  ["street", "Street"],
  ["postalCode", "Postal code"],
  ["city", "City"],
  ["country", "Country"],
  ["obtainedOn", "Date obtained"],
] as const;
type Field = (typeof fields)[number][0];
type Mapping = Record<Field, string | null>;
type Preview = NonNullable<PreviewImportMutation["previewImport"]["csvPreview"]>;

const cultures = ["de-DE", "de-AT", "de-CH", "en-GB", "en-US"];

/** One line from typed mutation errors (summary plus field messages), or null when there are none. */
export function messageOf(errors: Parameters<typeof formErrors>[0]): string | null {
  const e = formErrors(errors);
  return e ? [e.message, ...Object.values(e.fields)].filter(Boolean).join(" ") : null;
}

export function ImportWizard({ onStarted }: { onStarted: () => void }) {
  const [categories, setCategories] = useState<{ id: string; name: string }[]>([]);
  const [categoryId, setCategoryId] = useState("");
  const [newCategory, setNewCategory] = useState("");
  const [documentId, setDocumentId] = useState<string | null>(null);
  const [preview, setPreview] = useState<Preview | null>(null);
  const [mapping, setMapping] = useState<Mapping | null>(null);
  const [culture, setCulture] = useState("de-DE");
  const [progress, setProgress] = useState<number | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [fileKey, setFileKey] = useState(0);

  const loadCategories = () =>
    execute(ImportCategoriesDocument).then(
      (d) => setCategories(d.categories),
      (e: Error) => setError(e.message),
    );

  useEffect(() => {
    loadCategories();
  }, []);

  async function run(action: () => Promise<string | null>) {
    setBusy(true);
    setError(null);
    try {
      setError(await action());
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }

  const upload = (picked: File) =>
    run(async () => {
      // Excel on Windows reports .csv files as application/vnd.ms-excel; the Api accepts text/csv only.
      const file = new File([picked], picked.name, { type: "text/csv" });
      const { requestDocumentUpload } = await execute(RequestCsvUploadDocument, {
        input: { fileName: file.name, contentType: file.type, sizeBytes: file.size },
      });
      const payload = requestDocumentUpload.documentUploadPayload;
      if (!payload) return messageOf(requestDocumentUpload.errors) ?? "Upload refused.";
      await uploadWithProgress(payload.uploadUrl, file, setProgress);
      const { confirmDocumentUpload } = await execute(ConfirmCsvUploadDocument, { input: { id: payload.document.id } });
      if (confirmDocumentUpload.errors?.length) return messageOf(confirmDocumentUpload.errors) ?? "Upload could not be confirmed.";
      const { previewImport } = await execute(PreviewImportDocument, { input: { documentId: payload.document.id } });
      const result = previewImport.csvPreview;
      if (!result) return messageOf(previewImport.errors) ?? "The file could not be read.";
      setDocumentId(payload.document.id);
      setPreview(result);
      setMapping(Object.fromEntries(fields.map(([f]) => [f, result.suggestedMapping[f] ?? null])) as Mapping);
      return null;
    });

  const start = () =>
    run(async () => {
      let id = categoryId;
      if (!id) {
        if (!newCategory.trim()) return "Pick or create a category.";
        const { createCategory } = await execute(CreateImportCategoryDocument, { input: { name: newCategory } });
        if (!createCategory.category) return messageOf(createCategory.errors) ?? "Category could not be created.";
        id = createCategory.category.id;
      }
      const { startImport } = await execute(StartImportDocument, {
        input: { documentId: documentId!, categoryId: id, culture, mapping: mapping! },
      });
      if (!startImport.importBatch) return messageOf(startImport.errors) ?? "Import could not be started.";
      setPreview(null);
      setMapping(null);
      setDocumentId(null);
      setProgress(null);
      setNewCategory("");
      setCategoryId("");
      setFileKey((k) => k + 1);
      await loadCategories();
      onStarted();
      return null;
    });

  return (
    <section aria-label="New import" className="space-y-4 rounded-lg border border-border p-4">
      <h2 className="text-lg font-semibold">New import</h2>
      <label className="block text-sm">
        CSV file
        <input
          key={fileKey}
          type="file"
          accept=".csv,text/csv"
          disabled={busy}
          onChange={(e) => e.target.files?.[0] && upload(e.target.files[0])}
          className="mt-1 block"
        />
      </label>
      {progress !== null && !preview && <progress value={progress} max={100} className="w-full" />}
      {preview && mapping && (
        <>
          <p className="text-sm text-muted">
            Delimiter “{preview.delimiter}”, encoding {preview.encoding}, {preview.headers.length} columns.
          </p>
          <div className="grid gap-2 sm:grid-cols-4">
            {fields.map(([field, label]) => (
              <label key={field} className="text-sm">
                {label} column
                <select
                  aria-label={`${label} column`}
                  value={mapping[field] ?? ""}
                  onChange={(e) => setMapping({ ...mapping, [field]: e.target.value || null })}
                  className="mt-1 block w-full rounded-md border border-border bg-background px-2 py-1"
                >
                  <option value="">— not in file —</option>
                  {preview.headers.map((h) => (
                    <option key={h} value={h}>
                      {h}
                    </option>
                  ))}
                </select>
              </label>
            ))}
          </div>
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr>
                  {preview.headers.map((h) => (
                    <th key={h} className="px-2 py-1 text-left">
                      {h}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {preview.rows.map((row, i) => (
                  <tr key={i} className="border-t border-border">
                    {row.map((cell, j) => (
                      <td key={j} className="px-2 py-1">
                        {cell}
                      </td>
                    ))}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="flex flex-wrap items-end gap-3">
            <label className="text-sm">
              Category
              <select
                value={categoryId}
                onChange={(e) => setCategoryId(e.target.value)}
                className="mt-1 block rounded-md border border-border bg-background px-2 py-1"
              >
                <option value="">— new category —</option>
                {categories.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.name}
                  </option>
                ))}
              </select>
            </label>
            {!categoryId && (
              <label className="text-sm">
                New category
                <input
                  value={newCategory}
                  onChange={(e) => setNewCategory(e.target.value)}
                  className="mt-1 block rounded-md border border-border bg-background px-2 py-1"
                />
              </label>
            )}
            <label className="text-sm">
              Date and phone format
              <select
                value={culture}
                onChange={(e) => setCulture(e.target.value)}
                className="mt-1 block rounded-md border border-border bg-background px-2 py-1"
              >
                {cultures.map((c) => (
                  <option key={c}>{c}</option>
                ))}
              </select>
            </label>
            <button
              type="button"
              disabled={busy}
              onClick={start}
              className="rounded-md bg-primary px-4 py-1.5 text-on-primary disabled:opacity-50"
            >
              Start import
            </button>
          </div>
        </>
      )}
      {error && (
        <p role="alert" className="text-sm text-error">
          {error}
        </p>
      )}
    </section>
  );
}
