import { CampaignPage } from "@/components/CampaignPage";

export const metadata = { title: "Campaign" };

export default async function Page({ params }: { params: Promise<{ id: string }> }) {
  return <CampaignPage id={(await params).id} />;
}
