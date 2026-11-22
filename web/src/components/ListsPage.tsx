"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { graphql } from "@/gql";
import type { ContactListMembersQuery, ContactListsQuery } from "@/gql/graphql";
import { execute } from "@/lib/graphql";
import { messageOf } from "./ImportWizard";

const ContactListsDocument = graphql(`
  query ContactLists {
    contactLists(first: 100) {
      nodes {
        id
        name
        memberCount
        createdAtUtc
        createdBy
      }
    }
  }
`);

const ContactListMembersDocument = graphql(`
  query ContactListMembers($listId: UUID!, $after: String) {
    contactListMembers(listId: $listId, first: 100, after: $after) {
      totalCount
      pageInfo {
        hasNextPage
        endCursor
      }
      nodes {
        addedAtUtc
        contact {
          id
          name
          email
          phone
          city
        }
      }
    }
  }
`);

const RenameListDocument = graphql(`
  mutation RenameList($input: RenameContactListInput!) {
    renameContactList(input: $input) {
      contactList {
        id
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

const DeleteListDocument = graphql(`
  mutation DeleteList($input: DeleteContactListInput!) {
    deleteContactList(input: $input) {
      errors {
        __typename
        ... on Error {
          message
        }
      }
    }
  }
`);

const RemoveFromListDocument = graphql(`
  mutation RemoveFromList($input: RemoveFromContactListInput!) {
    removeFromContactList(input: $input) {
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

type List = NonNullable<NonNullable<ContactListsQuery["contactLists"]>["nodes"]>[number];
type Member = NonNullable<NonNullable<ContactListMembersQuery["contactListMembers"]>["nodes"]>[number];

export function ListsPage() {
  const [lists, setLists] = useState<List[]>([]);
  const [open, setOpen] = useState<List | null>(null);
  const [members, setMembers] = useState<Member[]>([]);
  const [cursor, setCursor] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  const load = useCallback(
    () => execute(ContactListsDocument).then((d) => setLists(d.contactLists?.nodes ?? []), (e: Error) => setMessage(e.message)),
    [],
  );

  useEffect(() => {
    load();
  }, [load]);

  async function openList(list: List, after: string | null = null) {
    const d = await execute(ContactListMembersDocument, { listId: list.id, after });
    setOpen(list);
    setMembers((m) => [...(after ? m : []), ...(d.contactListMembers?.nodes ?? [])]);
    setCursor(d.contactListMembers?.pageInfo.hasNextPage ? (d.contactListMembers.pageInfo.endCursor ?? null) : null);
  }

  async function rename(list: List) {
    const name = window.prompt("List name", list.name);
    if (!name) return;
    const { renameContactList } = await execute(RenameListDocument, { input: { id: list.id, name } });
    setMessage(messageOf(renameContactList.errors));
    await load();
  }

  async function remove(list: List) {
    if (!window.confirm(`Delete list "${list.name}"? Contacts stay.`)) return;
    const { deleteContactList } = await execute(DeleteListDocument, { input: { id: list.id } });
    setMessage(messageOf(deleteContactList.errors));
    if (open?.id === list.id) setOpen(null);
    await load();
  }

  async function removeMember(contactId: string) {
    if (!open) return;
    await execute(RemoveFromListDocument, { input: { listId: open.id, contactIds: [contactId] } });
    setMembers((m) => m.filter((x) => x.contact?.id !== contactId));
    await load();
  }

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-semibold">Lists</h1>
      {message && <p role="status">{message}</p>}
      <table className="w-full text-sm">
        <thead>
          <tr className="text-left">
            <th className="px-2 py-1">Name</th>
            <th className="px-2 py-1">Contacts</th>
            <th className="px-2 py-1">Created</th>
            <th className="px-2 py-1" />
          </tr>
        </thead>
        <tbody>
          {lists.map((l) => (
            <tr key={l.id} className="border-t border-border">
              <td className="px-2 py-1">
                <button type="button" onClick={() => openList(l)} className="underline">
                  {l.name}
                </button>
              </td>
              <td className="px-2 py-1">{l.memberCount}</td>
              <td className="px-2 py-1">
                {new Date(l.createdAtUtc).toLocaleDateString()} · {l.createdBy}
              </td>
              <td className="space-x-2 px-2 py-1">
                <button type="button" onClick={() => rename(l)} className="underline">
                  Rename
                </button>
                <button type="button" onClick={() => remove(l)} className="underline">
                  Delete
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      {open && (
        <section aria-label={`Members of ${open.name}`} className="space-y-2">
          <h2 className="text-lg font-semibold">{open.name}</h2>
          <p className="text-sm text-muted">
            Add contacts from the <Link href="/app/contacts" className="underline">contacts search</Link>.
          </p>
          <table className="w-full text-sm">
            <tbody>
              {members.map((m) => (
                <tr key={m.contact?.id} className="border-t border-border">
                  <td className="px-2 py-1">
                    <Link href={`/app/contacts/${m.contact?.id}`} className="underline">
                      {m.contact?.name ?? m.contact?.email}
                    </Link>
                  </td>
                  <td className="px-2 py-1">{m.contact?.email}</td>
                  <td className="px-2 py-1">{m.contact?.phone}</td>
                  <td className="px-2 py-1">{m.contact?.city}</td>
                  <td className="px-2 py-1">
                    <button type="button" onClick={() => removeMember(m.contact!.id)} className="underline">
                      Remove
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {cursor && (
            <button type="button" onClick={() => openList(open, cursor)} className="underline">
              Load more
            </button>
          )}
        </section>
      )}
    </div>
  );
}
