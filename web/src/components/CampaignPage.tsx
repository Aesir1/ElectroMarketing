"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { graphql } from "@/gql";
import type { CampaignDtoInput, CampaignEditorQuery, CampaignMessagesQuery } from "@/gql/graphql";
import { execute } from "@/lib/graphql";
import { messageOf } from "./ImportWizard";
import { CampaignInsightsView } from "./CampaignInsightsView";

const CampaignEditorDocument = graphql(`
  query CampaignEditor($id: UUID!) {
    campaign(id: $id) {
      id
      rowVersion
      name
      status
      contactListId
      sendEmail
      sendSms
      sendPost
      emailSubject
      emailBody
      smsText
      letterBody
      landingTitle
      landingBody
      startedAtUtc
      finishedAtUtc
      lettersReady
    }
    contactLists(first: 100) {
      nodes {
        id
        name
        memberCount
      }
    }
  }
`);

const CampaignMessagesDocument = graphql(`
  query CampaignMessages($id: UUID!, $after: String) {
    campaignMessages(campaignId: $id, first: 100, after: $after) {
      totalCount
      pageInfo {
        hasNextPage
        endCursor
      }
      nodes {
        id
        channel
        status
        token
        sentAtUtc
        error
        contact {
          id
          name
          email
          phone
        }
      }
    }
  }
`);

const CampaignCountsDocument = graphql(`
  query CampaignCounts($id: UUID!) {
    queued: campaignMessages(campaignId: $id, where: { status: { eq: QUEUED } }) {
      totalCount
    }
    sent: campaignMessages(campaignId: $id, where: { status: { eq: SENT } }) {
      totalCount
    }
    failed: campaignMessages(campaignId: $id, where: { status: { eq: FAILED } }) {
      totalCount
    }
    skipped: campaignMessages(campaignId: $id, where: { status: { eq: SKIPPED } }) {
      totalCount
    }
  }
`);

const SaveCampaignDocument = graphql(`
  mutation SaveCampaign($input: UpdateCampaignInput!) {
    updateCampaign(input: $input) {
      campaign {
        id
        rowVersion
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

const StartCampaignDocument = graphql(`
  mutation StartCampaign($input: StartCampaignInput!) {
    startCampaign(input: $input) {
      campaign {
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

type Campaign = NonNullable<CampaignEditorQuery["campaign"]>;
type Message = NonNullable<NonNullable<CampaignMessagesQuery["campaignMessages"]>["nodes"]>[number];

const sample = { Name: "Erika Mustermann", Link: "https://short.link/Ab3dE5g", UnsubscribeLink: "https://short.link/u/Ab3dE5g" };

/** Client-side preview with the same {{Key}} placeholders the Api renders. */
function preview(template: string | null | undefined) {
  return (template ?? "").replace(/\{\{(\w+)\}\}/g, (m, key: string) => sample[key as keyof typeof sample] ?? m);
}

const editable = [
  "name",
  "contactListId",
  "sendEmail",
  "sendSms",
  "sendPost",
  "emailSubject",
  "emailBody",
  "smsText",
  "letterBody",
  "landingTitle",
  "landingBody",
] as const;

export function CampaignPage({ id }: { id: string }) {
  const [campaign, setCampaign] = useState<Campaign | null>(null);
  const [lists, setLists] = useState<{ id: string; name: string; memberCount: number }[]>([]);
  const [messages, setMessages] = useState<Message[]>([]);
  const [counts, setCounts] = useState<Record<string, number>>({});
  const [next, setNext] = useState<string | null>(null);
  const [loadingMore, setLoadingMore] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const req = useRef(0); // id of the newest fetch; older responses are dropped

  const load = useCallback(async () => {
    const mine = ++req.current;
    try {
      const d = await execute(CampaignEditorDocument, { id });
      if (mine !== req.current) return;
      setCampaign(d.campaign ?? null);
      setLists(d.contactLists?.nodes ?? []);
      if (d.campaign && d.campaign.status !== "DRAFT") {
        const [m, c] = await Promise.all([
          execute(CampaignMessagesDocument, { id, after: null }),
          execute(CampaignCountsDocument, { id }),
        ]);
        if (mine !== req.current) return;
        setMessages(m.campaignMessages?.nodes ?? []);
        setNext(m.campaignMessages?.pageInfo.hasNextPage ? (m.campaignMessages.pageInfo.endCursor ?? null) : null);
        setCounts({
          queued: c.queued?.totalCount ?? 0,
          sent: c.sent?.totalCount ?? 0,
          failed: c.failed?.totalCount ?? 0,
          skipped: c.skipped?.totalCount ?? 0,
        });
      }
      setError(null);
    } catch (e) {
      setError((e as Error).message);
    }
  }, [id]);

  useEffect(() => {
    // load() only sets state after awaiting the Api; the rule cannot see through the callback.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    load();
  }, [load]);

  // Refresh while sending.
  useEffect(() => {
    if (campaign?.status !== "SENDING") return;
    const timer = setInterval(load, 5000);
    return () => clearInterval(timer);
  }, [campaign?.status, load]);

  if (!campaign) return error ? <p role="alert">{error}</p> : <p>Loading…</p>;

  const set = <K extends keyof Campaign>(key: K, value: Campaign[K]) => setCampaign({ ...campaign, [key]: value });
  const input = () => ({ id, campaign: Object.fromEntries(editable.map((k) => [k, campaign[k] ?? null])) as CampaignDtoInput, rowVersion: campaign.rowVersion });

  async function save(): Promise<boolean> {
    setSaved(false);
    const { updateCampaign } = await execute(SaveCampaignDocument, { input: input() });
    setError(messageOf(updateCampaign.errors));
    const ok = !updateCampaign.errors?.length;
    // The next save must carry the new version.
    const next = updateCampaign.campaign;
    if (next) setCampaign((c) => c && { ...c, rowVersion: next.rowVersion });
    setSaved(ok);
    return ok;
  }

  async function loadMore() {
    const mine = ++req.current;
    setLoadingMore(true);
    try {
      const m = await execute(CampaignMessagesDocument, { id, after: next });
      if (mine !== req.current) return;
      setMessages((prev) => [...prev, ...(m.campaignMessages?.nodes ?? [])]);
      setNext(m.campaignMessages?.pageInfo.hasNextPage ? (m.campaignMessages.pageInfo.endCursor ?? null) : null);
    } catch (e) {
      if (mine === req.current) setError((e as Error).message);
    } finally {
      setLoadingMore(false);
    }
  }

  async function start() {
    setError(null);
    try {
      if (!(await save())) return;
      if (!window.confirm("Send this campaign now? This cannot be undone.")) return;
      const { startCampaign } = await execute(StartCampaignDocument, { input: { id } });
      if (!startCampaign.campaign) return setError(messageOf(startCampaign.errors));
      await load();
    } catch (e) {
      setError((e as Error).message);
    }
  }

  if (campaign.status !== "DRAFT") {
    return (
      <div className="space-y-4">
        <h1 className="text-2xl font-semibold">{campaign.name}</h1>
        <p>
          Status: {campaign.status.toLowerCase()}
          {campaign.startedAtUtc && ` · started ${new Date(campaign.startedAtUtc).toLocaleString()}`}
        </p>
        <ul className="flex flex-wrap gap-3 text-sm">
          {Object.entries(counts).map(([k, n]) => (
            <li key={k} className="rounded-md border border-border px-2 py-1">
              {k}: {n}
            </li>
          ))}
        </ul>
        {error && (
          <p role="alert" className="text-sm text-error">
            {error}
          </p>
        )}
        {campaign.lettersReady && (
          <a href={`/api/campaigns/${id}/letters`} className="underline">
            Download letters (PDF)
          </a>
        )}
        <CampaignInsightsView campaignId={id} />
        <table className="w-full text-sm">
          <thead>
            <tr className="text-left">
              <th className="px-2 py-1">Contact</th>
              <th className="px-2 py-1">Channel</th>
              <th className="px-2 py-1">Status</th>
              <th className="px-2 py-1">Sent</th>
              <th className="px-2 py-1">Note</th>
            </tr>
          </thead>
          <tbody>
            {messages.map((m) => (
              <tr key={m.id} className="border-t border-border">
                <td className="px-2 py-1">{m.contact ? (m.contact.name ?? m.contact.email ?? m.contact.phone) : "—"}</td>
                <td className="px-2 py-1">{m.channel.toLowerCase()}</td>
                <td className="px-2 py-1">{m.status.toLowerCase()}</td>
                <td className="px-2 py-1">{m.sentAtUtc && new Date(m.sentAtUtc).toLocaleString()}</td>
                <td className="px-2 py-1">{m.error}</td>
              </tr>
            ))}
          </tbody>
        </table>
        {next && (
          <button type="button" disabled={loadingMore} onClick={loadMore} className="rounded-md border border-border px-4 py-1.5 disabled:opacity-50">
            Load more
          </button>
        )}
      </div>
    );
  }

  const field = "mt-1 block w-full rounded-md border border-border bg-background px-2 py-1";
  return (
    <div className="space-y-4">
      <label className="block text-sm">
        Name
        <input value={campaign.name} onChange={(e) => set("name", e.target.value)} className={field} />
      </label>
      <label className="block text-sm">
        Contact list
        <select value={campaign.contactListId ?? ""} onChange={(e) => set("contactListId", e.target.value || null)} className={field}>
          <option value="">— pick a list —</option>
          {lists.map((l) => (
            <option key={l.id} value={l.id}>
              {l.name} ({l.memberCount})
            </option>
          ))}
        </select>
      </label>
      <fieldset className="flex gap-4 text-sm">
        <legend>Channels</legend>
        <label>
          <input type="checkbox" checked={campaign.sendEmail} onChange={(e) => set("sendEmail", e.target.checked)} /> E-Mail
        </label>
        <label>
          <input type="checkbox" checked={campaign.sendSms} onChange={(e) => set("sendSms", e.target.checked)} /> SMS
        </label>
        <label>
          <input type="checkbox" checked={campaign.sendPost} onChange={(e) => set("sendPost", e.target.checked)} /> Post
        </label>
      </fieldset>
      <p className="text-sm text-muted">Placeholders: {"{{Name}}"}, {"{{Link}}"}, {"{{UnsubscribeLink}}"}. A link is added when missing.</p>
      {campaign.sendEmail && (
        <section className="space-y-2">
          <label className="block text-sm">
            E-Mail subject
            <input value={campaign.emailSubject ?? ""} onChange={(e) => set("emailSubject", e.target.value)} className={field} />
          </label>
          <label className="block text-sm">
            E-Mail body (HTML)
            <textarea rows={8} value={campaign.emailBody ?? ""} onChange={(e) => set("emailBody", e.target.value)} className={field} />
          </label>
          <iframe title="E-Mail preview" sandbox="" srcDoc={preview(campaign.emailBody)} className="h-48 w-full rounded-md border border-border bg-white" />
        </section>
      )}
      {campaign.sendSms && (
        <section className="space-y-2">
          <label className="block text-sm">
            SMS text
            <textarea rows={3} value={campaign.smsText ?? ""} onChange={(e) => set("smsText", e.target.value)} className={field} />
          </label>
          <p className="rounded-md bg-surface p-2 text-sm">{preview(campaign.smsText)}</p>
        </section>
      )}
      {campaign.sendPost && (
        <label className="block text-sm">
          Letter text (a QR code with the link is printed below it)
          <textarea rows={8} value={campaign.letterBody ?? ""} onChange={(e) => set("letterBody", e.target.value)} className={field} />
        </label>
      )}
      <section className="space-y-2">
        <h2 className="font-semibold">Landing page (opened from links and QR codes)</h2>
        <input aria-label="Landing title" value={campaign.landingTitle ?? ""} onChange={(e) => set("landingTitle", e.target.value)} className={field} />
        <textarea aria-label="Landing text" rows={5} value={campaign.landingBody ?? ""} onChange={(e) => set("landingBody", e.target.value)} className={field} />
      </section>
      {error && (
        <p role="alert" className="text-sm text-error">
          {error}
        </p>
      )}
      {saved && !error && (
        <p role="status" className="text-sm">
          Draft saved.
        </p>
      )}
      <div className="flex gap-2">
        <button type="button" onClick={() => save().catch((e: Error) => setError(e.message))} className="rounded-md border border-border px-4 py-1.5">
          Save draft
        </button>
        <button type="button" onClick={start} className="rounded-md bg-primary px-4 py-1.5 text-on-primary">
          Save and start
        </button>
      </div>
    </div>
  );
}
