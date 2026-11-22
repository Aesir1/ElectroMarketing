"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { graphql } from "@/gql";
import type { CampaignsQuery } from "@/gql/graphql";
import { execute } from "@/lib/graphql";
import { messageOf } from "./ImportWizard";

const CampaignsDocument = graphql(`
  query Campaigns {
    campaigns(first: 100) {
      nodes {
        id
        name
        status
        sendEmail
        sendSms
        sendPost
        createdAtUtc
        startedAtUtc
      }
    }
  }
`);

const CreateCampaignDocument = graphql(`
  mutation CreateCampaign($input: CreateCampaignInput!) {
    createCampaign(input: $input) {
      campaign {
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

type Row = NonNullable<NonNullable<CampaignsQuery["campaigns"]>["nodes"]>[number];

export function CampaignsPage() {
  const router = useRouter();
  const [rows, setRows] = useState<Row[]>([]);
  const [name, setName] = useState("");
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    execute(CampaignsDocument).then((d) => setRows(d.campaigns?.nodes ?? []), (e: Error) => setError(e.message));
  }, []);

  async function create() {
    const { createCampaign } = await execute(CreateCampaignDocument, {
      input: { campaign: { name, sendEmail: true, sendSms: false, sendPost: false } },
    });
    if (createCampaign.campaign) router.push(`/app/campaigns/${createCampaign.campaign.id}`);
    else setError(messageOf(createCampaign.errors));
  }

  const channels = (c: Row) => [c.sendEmail && "E-Mail", c.sendSms && "SMS", c.sendPost && "Post"].filter(Boolean).join(", ");

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold">Campaigns</h1>
      <div className="flex gap-2">
        <input
          aria-label="New campaign name"
          placeholder="New campaign name"
          value={name}
          onChange={(e) => setName(e.target.value)}
          className="rounded-md border border-border bg-background px-2 py-1"
        />
        <button type="button" disabled={!name.trim()} onClick={create} className="rounded-md bg-primary px-4 py-1.5 text-on-primary disabled:opacity-50">
          Create
        </button>
      </div>
      {error && <p role="alert">{error}</p>}
      <table className="w-full text-sm">
        <thead>
          <tr className="text-left">
            <th className="px-2 py-1">Name</th>
            <th className="px-2 py-1">Channels</th>
            <th className="px-2 py-1">Status</th>
            <th className="px-2 py-1">Started</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((c) => (
            <tr key={c.id} className="border-t border-border">
              <td className="px-2 py-1">
                <Link href={`/app/campaigns/${c.id}`} className="underline">
                  {c.name}
                </Link>
              </td>
              <td className="px-2 py-1">{channels(c)}</td>
              <td className="px-2 py-1">{c.status.toLowerCase()}</td>
              <td className="px-2 py-1">{c.startedAtUtc && new Date(c.startedAtUtc).toLocaleString()}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
