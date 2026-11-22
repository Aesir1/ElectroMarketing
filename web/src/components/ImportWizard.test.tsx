// web/src/components/ImportWizard.test.tsx
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { graphql, http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { server } from "@/test/server";
import { ImportWizard } from "./ImportWizard";

describe("ImportWizard", () => {
  it("uploads as text/csv, applies the suggested mapping and starts with a new category", async () => {
    let uploadedType: string | null = null;
    let startInput: Record<string, unknown> | null = null;
    server.use(
      graphql.query("ImportCategories", () => HttpResponse.json({ data: { categories: [] } })),
      graphql.mutation("RequestCsvUpload", () =>
        HttpResponse.json({
          data: { requestDocumentUpload: { documentUploadPayload: { uploadUrl: "/api/documents/d1/content", document: { id: "d1" } }, errors: null } },
        }),
      ),
      http.put("*/api/documents/d1/content", ({ request }) => {
        uploadedType = request.headers.get("content-type");
        return new HttpResponse(null, { status: 204 });
      }),
      graphql.mutation("ConfirmCsvUpload", () => HttpResponse.json({ data: { confirmDocumentUpload: { errors: null } } })),
      graphql.mutation("PreviewImport", () =>
        HttpResponse.json({
          data: {
            previewImport: {
              errors: null,
              csvPreview: {
                delimiter: ";",
                encoding: "windows-1252",
                headers: ["Name", "E-Mail"],
                rows: [["Max", "max@x.de"]],
                suggestedMapping: { name: "Name", email: "E-Mail", phone: null, street: null, postalCode: null, city: null, country: null, obtainedOn: null },
              },
            },
          },
        }),
      ),
      graphql.mutation("CreateImportCategory", () =>
        HttpResponse.json({ data: { createCategory: { category: { id: "c1", name: "Fair" }, errors: null } } }),
      ),
      graphql.mutation("StartImport", ({ variables }) => {
        startInput = variables.input as Record<string, unknown>;
        return HttpResponse.json({ data: { startImport: { importBatch: { id: "b1" }, errors: null } } });
      }),
    );
    let started = false;
    render(<ImportWizard onStarted={() => (started = true)} />);

    const file = new File(["Name;E-Mail\nMax;max@x.de\n"], "contacts.csv", { type: "application/vnd.ms-excel" });
    fireEvent.change(screen.getByLabelText("CSV file"), { target: { files: [file] } });
    expect(await screen.findByText("max@x.de")).toBeTruthy();
    expect((screen.getByLabelText("E-Mail column") as HTMLSelectElement).value).toBe("E-Mail");

    fireEvent.change(screen.getByLabelText("New category"), { target: { value: "Fair" } });
    fireEvent.click(screen.getByRole("button", { name: "Start import" }));

    await waitFor(() => expect(started).toBe(true));
    expect(uploadedType).toBe("text/csv");
    expect(startInput).toEqual({
      documentId: "d1",
      categoryId: "c1",
      culture: "de-DE",
      mapping: { name: "Name", email: "E-Mail", phone: null, street: null, postalCode: null, city: null, country: null, obtainedOn: null },
    });
  });

  it("shows the field message when startImport returns a ValidationError", async () => {
    server.use(
      graphql.query("ImportCategories", () => HttpResponse.json({ data: { categories: [{ id: "c1", name: "Fair" }] } })),
      graphql.mutation("RequestCsvUpload", () =>
        HttpResponse.json({
          data: { requestDocumentUpload: { documentUploadPayload: { uploadUrl: "/api/documents/d1/content", document: { id: "d1" } }, errors: null } },
        }),
      ),
      http.put("*/api/documents/d1/content", () => new HttpResponse(null, { status: 204 })),
      graphql.mutation("ConfirmCsvUpload", () => HttpResponse.json({ data: { confirmDocumentUpload: { errors: null } } })),
      graphql.mutation("PreviewImport", () =>
        HttpResponse.json({
          data: {
            previewImport: {
              errors: null,
              csvPreview: {
                delimiter: ";",
                encoding: "utf-8",
                headers: ["Name"],
                rows: [["Max"]],
                suggestedMapping: { name: "Name", email: null, phone: null, street: null, postalCode: null, city: null, country: null, obtainedOn: null },
              },
            },
          },
        }),
      ),
      graphql.mutation("StartImport", () =>
        HttpResponse.json({
          data: {
            startImport: {
              importBatch: null,
              errors: [{ __typename: "ValidationError", message: "Invalid.", fields: [{ field: "Mapping", messages: ["Map at least the e-mail or the phone column."] }] }],
            },
          },
        }),
      ),
    );
    render(<ImportWizard onStarted={() => {}} />);
    fireEvent.change(screen.getByLabelText("CSV file"), { target: { files: [new File(["Name\nMax\n"], "c.csv")] } });
    await screen.findByText("Max");
    fireEvent.change(screen.getByLabelText("Category"), { target: { value: "c1" } });
    fireEvent.click(screen.getByRole("button", { name: "Start import" }));
    expect((await screen.findByRole("alert")).textContent).toContain("Map at least the e-mail or the phone column.");
  });
});
