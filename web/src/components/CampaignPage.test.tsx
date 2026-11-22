import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { graphql, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { server } from "@/test/server";
import { CampaignPage } from "./CampaignPage";

const draft = {
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
      graphql.mutation("SaveCampaign", () => HttpResponse.json({ data: { updateCampaign: { campaign: { id: "k1" }, errors: null } } })),
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
});
