"use client";

import { useEffect, useState } from "react";
import { Bar, BarChart, CartesianGrid, Legend, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { graphql } from "@/gql";
import type { CampaignInsightsQuery } from "@/gql/graphql";
import { execute } from "@/lib/graphql";

const CampaignInsightsDocument = graphql(`
  query CampaignInsights($id: UUID!) {
    campaignInsights(campaignId: $id) {
      medianResponseHours
      channels {
        channel
        sent
        failed
        skipped
        opened
        clicked
        landingViews
        unsubscribed
        openRate
        clickRate
      }
      responseLatency {
        label
        count
      }
      timeline {
        start
        opens
        clicks
      }
      byWeekdayHour {
        weekday
        hour
        count
      }
    }
  }
`);

const percent = new Intl.NumberFormat(undefined, { style: "percent", maximumFractionDigits: 1 });
const weekdays = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

export function CampaignInsightsView({ campaignId }: { campaignId: string }) {
  const [data, setData] = useState<CampaignInsightsQuery["campaignInsights"] | null>(null);

  useEffect(() => {
    execute(CampaignInsightsDocument, { id: campaignId }).then((d) => setData(d.campaignInsights ?? null), () => setData(null));
  }, [campaignId]);

  if (!data) return null;
  const max = Math.max(1, ...data.byWeekdayHour.map((c) => c.count));
  const timeline = data.timeline.map((p) => ({ ...p, label: new Date(p.start).toLocaleString(undefined, { weekday: "short", hour: "2-digit", day: "2-digit" }) }));

  const totals = { opens: timeline.reduce((a, p) => a + p.opens, 0), clicks: timeline.reduce((a, p) => a + p.clicks, 0) };
  const peak = data.byWeekdayHour.reduce((a, c) => (c.count > a.count ? c : a), { weekday: 0, hour: 0, count: 0 });

  return (
    <section aria-label="Insights" className="space-y-6">
      <h2 className="text-lg font-semibold">Insights</h2>
      <table className="w-full text-sm">
        <thead>
          <tr className="text-left">
            <th className="px-2 py-1">Channel</th>
            <th className="px-2 py-1">Sent</th>
            <th className="px-2 py-1">Failed / skipped</th>
            <th className="px-2 py-1">Opened*</th>
            <th className="px-2 py-1">Clicked / scanned</th>
            <th className="px-2 py-1">Unsubscribed</th>
          </tr>
        </thead>
        <tbody>
          {data.channels.map((c) => (
            <tr key={c.channel} className="border-t border-border">
              <td className="px-2 py-1">{c.channel.toLowerCase()}</td>
              <td className="px-2 py-1">{c.sent}</td>
              <td className="px-2 py-1">
                {c.failed} / {c.skipped}
              </td>
              <td className="px-2 py-1">{c.channel === "EMAIL" ? `${c.opened} (${percent.format(c.openRate)})` : "—"}</td>
              <td className="px-2 py-1">
                {c.clicked} ({percent.format(c.clickRate)})
              </td>
              <td className="px-2 py-1">{c.unsubscribed}</td>
            </tr>
          ))}
        </tbody>
      </table>
      <p className="text-xs text-muted">* E-mail opens are approximate: some mail apps load the tracking image automatically. Clicks are reliable.</p>

      <div>
        <h3 className="font-semibold">
          Time to first response{data.medianResponseHours != null && ` · median ${data.medianResponseHours} h`}
        </h3>
        <div role="img" aria-label={`Responses by time to first response: ${data.responseLatency.map((b) => `${b.label} ${b.count}`).join(", ")}`}>
        <ResponsiveContainer width="100%" height={220}>
          <BarChart data={data.responseLatency}>
            <CartesianGrid vertical={false} stroke="var(--color-border)" />
            <XAxis dataKey="label" tick={{ fill: "var(--color-muted)", fontSize: 12 }} stroke="var(--color-border)" />
            <YAxis allowDecimals={false} tick={{ fill: "var(--color-muted)", fontSize: 12 }} stroke="var(--color-border)" />
            <Tooltip contentStyle={{ background: "var(--color-background)", border: "1px solid var(--color-border)", color: "var(--color-foreground)" }} />
            <Bar dataKey="count" name="Responses" fill="var(--color-primary)" radius={[4, 4, 0, 0]} />
          </BarChart>
        </ResponsiveContainer>
        </div>
      </div>

      <div>
        <h3 className="font-semibold">Activity over time</h3>
        <div role="img" aria-label={`Activity over time: ${totals.opens} opens and ${totals.clicks} clicks in ${timeline.length} periods`}>
        <ResponsiveContainer width="100%" height={240}>
          <LineChart data={timeline}>
            <CartesianGrid vertical={false} stroke="var(--color-border)" />
            <XAxis dataKey="label" minTickGap={24} tick={{ fill: "var(--color-muted)", fontSize: 12 }} stroke="var(--color-border)" />
            <YAxis allowDecimals={false} tick={{ fill: "var(--color-muted)", fontSize: 12 }} stroke="var(--color-border)" />
            <Tooltip contentStyle={{ background: "var(--color-background)", border: "1px solid var(--color-border)", color: "var(--color-foreground)" }} />
            <Legend formatter={(v) => <span style={{ color: "var(--color-foreground)" }}>{v}</span>} />
            <Line type="monotone" dataKey="opens" name="Opens" stroke="var(--color-muted)" strokeWidth={2} strokeDasharray="4 3" dot={{ r: 3 }} />
            <Line type="monotone" dataKey="clicks" name="Clicks" stroke="var(--color-primary)" strokeWidth={2} dot={{ r: 3 }} />
          </LineChart>
        </ResponsiveContainer>
        </div>
      </div>

      <div>
        <h3 className="font-semibold">Clicks by weekday and hour</h3>
        <div role="img" aria-label={peak.count ? `Clicks by weekday and hour; busiest ${weekdays[peak.weekday]} ${peak.hour}:00 with ${peak.count}` : "Clicks by weekday and hour; no clicks yet"} className="grid grid-cols-[3rem_repeat(24,minmax(0,1fr))] gap-px text-[10px]">
          <span />
          {Array.from({ length: 24 }, (_, h) => (
            <span key={h} className="text-center text-muted">
              {h}
            </span>
          ))}
          {weekdays.map((day, w) => (
            <div key={day} className="contents">
              <span>{day}</span>
              {Array.from({ length: 24 }, (_, h) => {
                const count = data.byWeekdayHour.find((c) => c.weekday === w && c.hour === h)?.count ?? 0;
                return (
                  <span
                    key={h}
                    title={`${day} ${h}:00 · ${count} clicks`}
                    className="aspect-square rounded-sm"
                    style={{ background: "var(--color-primary)", opacity: count === 0 ? 0.06 : 0.2 + (0.8 * count) / max }}
                  />
                );
              })}
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
