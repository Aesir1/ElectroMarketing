import { render, screen } from "@testing-library/react";
import { graphql, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { server } from "@/test/server";
import { CampaignInsightsView } from "./CampaignInsightsView";

const insights = {
  medianResponseHours: 0.5,
  channels: [
    {
      channel: "EMAIL",
      sent: 4,
      failed: 0,
      skipped: 0,
      opened: 1,
      clicked: 2,
      landingViews: 0,
      unsubscribed: 0,
      openRate: 0.25,
      clickRate: 0.5,
    },
  ],
  responseLatency: [{ label: "< 1 h", count: 2 }],
  timeline: [{ start: "2026-11-22T21:00:00+01:00", opens: 1, clicks: 2 }],
  byWeekdayHour: [{ weekday: 0, hour: 21, count: 2 }],
};

describe("CampaignInsightsView", () => {
  it("renders counts and rates, with times in the campaign zone", async () => {
    server.use(
      graphql.query("CampaignInsights", () => HttpResponse.json({ data: { campaignInsights: insights } })),
    );
    render(<CampaignInsightsView campaignId="k1" />);
    expect(await screen.findByText("1 (25%)")).toBeTruthy();
    expect(screen.getByText("2 (50%)")).toBeTruthy();
    expect(screen.getAllByText(/times in UTC\+01:00/)).toHaveLength(2);
  });

  it("shows an alert when the query fails", async () => {
    server.use(graphql.query("CampaignInsights", () => HttpResponse.json({ errors: [{ message: "boom" }] })));
    render(<CampaignInsightsView campaignId="k1" />);
    expect((await screen.findByRole("alert")).textContent).toContain("boom");
  });
});
