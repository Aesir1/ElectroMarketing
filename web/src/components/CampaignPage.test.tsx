import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { graphql, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { server } from "@/test/server";
import { CampaignPage } from "./CampaignPage";

const draft = {
  rowVersion: 1,
  id: "k1",
  name: "Spring",
  status: "DRAFT",
  contactListId: "l1",
  sendEmail: false,
  sendSms: true,
  sendPost: false,
  emailSubject: null,
  emailBody: null,
  smsText: "Hi {{Name}}, see {{Link}}",
  letterBody: null,
  landingTitle: null,
  landingBody: null,
  startedAtUtc: null,
  finishedAtUtc: null,
  lettersReady: false,
};

describe("CampaignPage", () => {
  it("previews the SMS with sample values and shows start validation errors", async () => {
    server.use(
      graphql.query("CampaignEditor", () =>
        HttpResponse.json({ data: { campaign: draft, contactLists: { nodes: [{ id: "l1", name: "Fair", memberCount: 3 }] } } }),
      ),
      graphql.mutation("SaveCampaign", () => HttpResponse.json({ data: { updateCampaign: { campaign: { id: "k1", rowVersion: 2 }, errors: null } } })),
      graphql.mutation("StartCampaign", () =>
        HttpResponse.json({
          data: {
            startCampaign: {
              campaign: null,
              errors: [{ __typename: "ConflictError", message: "No contact in the list can be reached on the selected channels." }],
            },
          },
        }),
      ),
    );
    render(<CampaignPage id="k1" />);

    expect(await screen.findByText("Hi Erika Mustermann, see https://short.link/Ab3dE5g")).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Save and start" }));
    await waitFor(() => expect(screen.getByRole("alert").textContent).toContain("No contact in the list can be reached"));
  });

  it("shows server-side counts and a dash for an unreadable contact", async () => {
    server.use(
      graphql.query("CampaignEditor", () =>
        HttpResponse.json({ data: { campaign: { ...draft, status: "SENT" }, contactLists: { nodes: [] } } }),
      ),
      graphql.query("CampaignMessages", () =>
        HttpResponse.json({
          data: {
            campaignMessages: {
              totalCount: 250,
              pageInfo: { hasNextPage: false, endCursor: null },
              nodes: [{ id: "m1", channel: "SMS", status: "SENT", token: "Ab3dE5g", sentAtUtc: null, error: null, contact: null }],
            },
          },
        }),
      ),
      graphql.query("CampaignCounts", () =>
        HttpResponse.json({ data: { queued: { totalCount: 0 }, sent: { totalCount: 250 }, failed: { totalCount: 0 }, skipped: { totalCount: 0 } } }),
      ),
    );
    render(<CampaignPage id="k1" />);

    expect(await screen.findByText("sent: 250")).toBeTruthy();
    expect(screen.getByText("—")).toBeTruthy();
  });
});
