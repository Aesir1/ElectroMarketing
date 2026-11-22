"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { graphql } from "@/gql";
import type { RecentInsightsQuery } from "@/gql/graphql";
import { execute } from "@/lib/graphql";

const RecentInsightsDocument = graphql(`
  query RecentInsights {
    recentCampaignInsights(take: 10) {
      campaignId
      campaignName
      startedAtUtc
      medianResponseHours
      channels {
        channel
        sent
        openRate
        clickRate
        unsubscribed
      }
    }
  }
`);

const percent = new Intl.NumberFormat(undefined, { style: "percent", maximumFractionDigits: 1 });

export function InsightsPage() {
  const [rows, setRows] = useState<RecentInsightsQuery["recentCampaignInsights"]>([]);

  useEffect(() => {
    execute(RecentInsightsDocument).then((d) => setRows(d.recentCampaignInsights), () => setRows([]));
  }, []);

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold">Insights</h1>
      <p className="text-sm text-muted">Last 10 campaigns. Open a campaign for timing details.</p>
      <table className="w-full text-sm">
        <thead>
          <tr className="text-left">
            <th className="px-2 py-1">Campaign</th>
            <th className="px-2 py-1">Started</th>
            <th className="px-2 py-1">Channel</th>
            <th className="px-2 py-1">Sent</th>
            <th className="px-2 py-1">Opens*</th>
            <th className="px-2 py-1">Clicks / scans</th>
            <th className="px-2 py-1">Unsubscribed</th>
            <th className="px-2 py-1">Median response</th>
          </tr>
        </thead>
        <tbody>
          {rows.flatMap((r) =>
            r.channels.map((c, i) => (
              <tr key={`${r.campaignId}-${c.channel}`} className={i === 0 ? "border-t border-border" : ""}>
                <td className="px-2 py-1">
                  {i === 0 && (
                    <Link href={`/app/campaigns/${r.campaignId}`} className="underline">
                      {r.campaignName}
                    </Link>
                  )}
                </td>
                <td className="px-2 py-1">{i === 0 && r.startedAtUtc && new Date(r.startedAtUtc).toLocaleDateString()}</td>
                <td className="px-2 py-1">{c.channel.toLowerCase()}</td>
                <td className="px-2 py-1">{c.sent}</td>
                <td className="px-2 py-1">{c.channel === "EMAIL" ? percent.format(c.openRate) : "—"}</td>
                <td className="px-2 py-1">{percent.format(c.clickRate)}</td>
                <td className="px-2 py-1">{c.unsubscribed}</td>
                <td className="px-2 py-1">{i === 0 && r.medianResponseHours != null && `${r.medianResponseHours} h`}</td>
              </tr>
            )),
          )}
        </tbody>
      </table>
      <p className="text-xs text-muted">* E-mail opens are approximate.</p>
    </div>
  );
}
