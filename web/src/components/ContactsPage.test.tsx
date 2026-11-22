import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { graphql, HttpResponse } from "msw";
import { describe, expect, it, vi } from "vitest";
import { server } from "@/test/server";
import { ContactsPage } from "./ContactsPage";

const params = vi.hoisted(() => ({ q: "" }));
vi.mock("next/navigation", () => ({ useSearchParams: () => new URLSearchParams(params.q) }));

const contacts = [
  { id: "c1", name: "Jörg Müller", email: "j@x.de", phone: "0170 1234567", city: "Köln", obtainedOn: "2026-03-01", emailOptOut: false, smsOptOut: false },
  { id: "c2", name: "Max Mustermann", email: "max@x.de", phone: null, city: "Köln", obtainedOn: null, emailOptOut: false, smsOptOut: false },
];

describe("ContactsPage", () => {
  it("searches with a debounce and adds the selection to an existing list", async () => {
    const searches: unknown[] = [];
    let added: Record<string, unknown> | null = null;
    server.use(
      graphql.query("ContactsPageData", () =>
        HttpResponse.json({ data: { categories: [], contactLists: { nodes: [{ id: "l1", name: "Fair" }] } } }),
      ),
      graphql.query("Contacts", ({ variables }) => {
        searches.push(variables.filter);
        return HttpResponse.json({
          data: { contacts: { totalCount: 2, pageInfo: { hasNextPage: false, endCursor: null }, nodes: contacts } },
        });
      }),
      graphql.mutation("AddToList", ({ variables }) => {
        added = variables.input as Record<string, unknown>;
        return HttpResponse.json({ data: { addToContactList: { count: 1, errors: null } } });
      }),
    );
    render(<ContactsPage />);

    await screen.findByText("Jörg Müller");
    fireEvent.change(screen.getByLabelText("Search contacts"), { target: { value: "köln" } });
    await waitFor(() => expect(searches.at(-1)).toMatchObject({ search: "köln" }));

    fireEvent.click(screen.getByLabelText("Select Jörg Müller"));
    fireEvent.change(screen.getByLabelText("Target list"), { target: { value: "l1" } });
    fireEvent.click(screen.getByRole("button", { name: "Add selected (1)" }));

    await waitFor(() => expect(added).toEqual({ listId: "l1", contactIds: ["c1"] }));
    expect(await screen.findByText("1 contact added to Fair.")).toBeTruthy();
  });

  function mockApi(onFilter: (f: unknown) => void, onAdd: (i: Record<string, unknown>) => void = () => {}) {
    server.use(
      graphql.query("ContactsPageData", () =>
        HttpResponse.json({ data: { categories: [], contactLists: { nodes: [{ id: "l1", name: "Fair" }] } } }),
      ),
      graphql.query("Contacts", ({ variables }) => {
        onFilter(variables.filter);
        return HttpResponse.json({
          data: { contacts: { totalCount: 2, pageInfo: { hasNextPage: false, endCursor: null }, nodes: contacts } },
        });
      }),
      graphql.mutation("AddToList", ({ variables }) => {
        onAdd(variables.input as Record<string, unknown>);
        return HttpResponse.json({ data: { addToContactList: { count: 2, errors: null } } });
      }),
    );
  }

  it("adds all matching by filter, without contact ids", async () => {
    let added: Record<string, unknown> | null = null;
    mockApi(() => {}, (i) => (added = i));
    params.q = "";
    render(<ContactsPage />);
    await screen.findByText("Jörg Müller");
    fireEvent.change(screen.getByLabelText("Target list"), { target: { value: "l1" } });
    fireEvent.click(await screen.findByRole("button", { name: "Add all 2 matching" }));
    await waitFor(() => expect(added).not.toBeNull());
    expect(added).toMatchObject({ listId: "l1", filter: { batchIds: [] } });
    expect(added).not.toHaveProperty("contactIds");
  });

  it("honours ?batch= in the filter", async () => {
    const filters: unknown[] = [];
    mockApi((f) => filters.push(f));
    params.q = "batch=b1";
    render(<ContactsPage />);
    await screen.findByText("Jörg Müller");
    expect(filters.at(-1)).toMatchObject({ batchIds: ["b1"] });
    params.q = "";
  });
});
