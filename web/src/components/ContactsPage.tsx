"use client";

import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { useEffect, useMemo, useRef, useState } from "react";
import { graphql } from "@/gql";
import type { ContactsQuery, ContactFilterInput } from "@/gql/graphql";
import { execute } from "@/lib/graphql";
import { messageOf } from "./ImportWizard";

const ContactsDocument = graphql(`
  query Contacts($filter: ContactFilterInput, $after: String) {
    contacts(filter: $filter, first: 50, after: $after) {
      totalCount
      pageInfo {
        hasNextPage
        endCursor
      }
      nodes {
        id
        name
        email
        phone
        city
        obtainedOn
        emailOptOut
        smsOptOut
      }
    }
  }
`);

const ContactsPageDataDocument = graphql(`
  query ContactsPageData {
    categories {
      id
      name
    }
    contactLists(first: 100) {
      nodes {
        id
        name
      }
    }
  }
`);

const CreateListForSelectionDocument = graphql(`
  mutation CreateListForSelection($input: CreateContactListInput!) {
    createContactList(input: $input) {
      contactList {
        id
        name
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

const AddToListDocument = graphql(`
  mutation AddToList($input: AddToContactListInput!) {
    addToContactList(input: $input) {
      count
      errors {
        __typename
        ... on Error {
          message
        }
      }
    }
  }
`);

type Contact = NonNullable<NonNullable<ContactsQuery["contacts"]>["nodes"]>[number];

export function ContactsPage() {
  const batch = useSearchParams().get("batch");
  const [search, setSearch] = useState("");
  const [debounced, setDebounced] = useState("");
  const [categoryIds, setCategoryIds] = useState<string[]>([]);
  const [categories, setCategories] = useState<{ id: string; name: string }[]>([]);
  const [lists, setLists] = useState<{ id: string; name: string }[]>([]);
  const [rows, setRows] = useState<Contact[]>([]);
  const [total, setTotal] = useState(0);
  const [cursor, setCursor] = useState<string | null>(null);
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [listId, setListId] = useState("");
  const [newList, setNewList] = useState("");
  const [message, setMessage] = useState<string | null>(null);
  const [loaded, setLoaded] = useState<ContactFilterInput | null>(null); // filter the rows belong to
  const [more, setMore] = useState(false);
  const req = useRef(0); // id of the newest fetch; older responses are dropped

  useEffect(() => {
    execute(ContactsPageDataDocument).then(
      (d) => {
        setCategories(d.categories);
        setLists(d.contactLists?.nodes ?? []);
      },
      (e: Error) => setMessage(e.message),
    );
  }, []);

  useEffect(() => {
    const timer = setTimeout(() => setDebounced(search.trim()), 300);
    return () => clearTimeout(timer);
  }, [search]);

  const filter: ContactFilterInput = useMemo(
    () => ({ search: debounced || null, categoryIds, batchIds: batch ? [batch] : [] }),
    [debounced, categoryIds, batch],
  );

  useEffect(() => {
    const id = ++req.current;
    execute(ContactsDocument, { filter, after: null }).then(
      (d) => {
        if (id !== req.current) return;
        setRows(d.contacts?.nodes ?? []);
        setTotal(d.contacts?.totalCount ?? 0);
        // The selection survives new searches, so contacts from several searches can be added at once.
        setCursor(d.contacts?.pageInfo.hasNextPage ? (d.contacts.pageInfo.endCursor ?? null) : null);
        setLoaded(filter);
      },
      (e: Error) => {
        if (id !== req.current) return;
        setMessage(e.message);
        setLoaded(filter);
      },
    );
  }, [filter]);

  async function loadMore() {
    const id = ++req.current;
    setMore(true);
    try {
      const d = await execute(ContactsDocument, { filter, after: cursor });
      if (id !== req.current) return;
      setRows((r) => [...r, ...(d.contacts?.nodes ?? [])]);
      setCursor(d.contacts?.pageInfo.hasNextPage ? (d.contacts.pageInfo.endCursor ?? null) : null);
    } catch (e) {
      if (id === req.current) setMessage((e as Error).message);
    } finally {
      setMore(false);
    }
  }

  const loading = loaded !== filter || more;

  function toggle(id: string) {
    setSelected((s) => {
      const next = new Set(s);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  async function addTo(source: { contactIds: string[] } | { filter: ContactFilterInput }) {
    setMessage(null);
    try {
      let target = lists.find((l) => l.id === listId);
      if (!target) {
        if (!newList.trim()) return setMessage("Pick a list or enter a name for a new one.");
        const { createContactList } = await execute(CreateListForSelectionDocument, { input: { name: newList } });
        if (!createContactList.contactList) return setMessage(messageOf(createContactList.errors));
        target = createContactList.contactList;
        setLists((l) => [target!, ...l]);
        setListId(target.id);
        setNewList("");
      }
      const { addToContactList } = await execute(AddToListDocument, { input: { listId: target.id, ...source } });
      if (addToContactList.errors?.length) return setMessage(messageOf(addToContactList.errors));
      const n = addToContactList.count ?? 0;
      setMessage(`${n} contact${n === 1 ? "" : "s"} added to ${target.name}.`);
      setSelected(new Set());
    } catch (e) {
      setMessage((e as Error).message);
    }
  }

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold">Contacts</h1>
      <input
        type="search"
        aria-label="Search contacts"
        placeholder="Name, e-mail, phone, street, city…"
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        className="w-full rounded-md border border-border bg-background px-3 py-2"
      />
      <fieldset className="flex flex-wrap gap-3 text-sm">
        <legend className="sr-only">Categories</legend>
        {categories.map((c) => (
          <label key={c.id} className="flex items-center gap-1">
            <input
              type="checkbox"
              checked={categoryIds.includes(c.id)}
              onChange={(e) => setCategoryIds((ids) => (e.target.checked ? [...ids, c.id] : ids.filter((i) => i !== c.id)))}
            />
            {c.name}
          </label>
        ))}
        {batch && (
          <Link href="/app/contacts" className="underline">
            Only one import lot — show all
          </Link>
        )}
      </fieldset>
      <div className="flex flex-wrap items-end gap-2 text-sm">
        <label>
          Target list
          <select
            aria-label="Target list"
            value={listId}
            onChange={(e) => setListId(e.target.value)}
            className="mt-1 block rounded-md border border-border bg-background px-2 py-1"
          >
            <option value="">— new list —</option>
            {lists.map((l) => (
              <option key={l.id} value={l.id}>
                {l.name}
              </option>
            ))}
          </select>
        </label>
        {!listId && (
          <label>
            New list name
            <input
              value={newList}
              onChange={(e) => setNewList(e.target.value)}
              className="mt-1 block rounded-md border border-border bg-background px-2 py-1"
            />
          </label>
        )}
        <button
          type="button"
          disabled={selected.size === 0}
          onClick={() => addTo({ contactIds: [...selected] })}
          className="rounded-md border border-border px-3 py-1 disabled:opacity-50"
        >
          Add selected ({selected.size})
        </button>
        <button
          type="button"
          disabled={total === 0 || loading || debounced !== search.trim()}
          onClick={() => addTo({ filter })}
          className="rounded-md border border-border px-3 py-1 disabled:opacity-50"
        >
          Add all {total} matching
        </button>
      </div>
      {message && <p role="status">{message}</p>}
      <table className="w-full text-sm">
        <thead>
          <tr className="text-left">
            <th className="px-2 py-1" />
            <th className="px-2 py-1">Name</th>
            <th className="px-2 py-1">E-Mail</th>
            <th className="px-2 py-1">Phone</th>
            <th className="px-2 py-1">City</th>
            <th className="px-2 py-1">Obtained</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((c) => (
            <tr key={c.id} className="border-t border-border">
              <td className="px-2 py-1">
                <input
                  type="checkbox"
                  aria-label={`Select ${c.name ?? c.email ?? c.phone}`}
                  checked={selected.has(c.id)}
                  onChange={() => toggle(c.id)}
                />
              </td>
              <td className="px-2 py-1">
                <Link href={`/app/contacts/${c.id}`} className="underline">
                  {c.name ?? "—"}
                </Link>
              </td>
              <td className="px-2 py-1">{c.email}</td>
              <td className="px-2 py-1">{c.phone}</td>
              <td className="px-2 py-1">{c.city}</td>
              <td className="px-2 py-1">{c.obtainedOn as string | null}</td>
            </tr>
          ))}
        </tbody>
      </table>
      <p className="text-sm text-muted">
        {rows.length} of {total}
      </p>
      {cursor && (
        <button type="button" disabled={loading} onClick={loadMore} className="underline disabled:opacity-50">
          Load more
        </button>
      )}
    </div>
  );
}
