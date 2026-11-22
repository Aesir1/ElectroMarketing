// web/src/app/app/contacts/[id]/page.tsx
import { ContactDetailPage } from "@/components/ContactDetailPage";

export const metadata = { title: "Contact" };

export default async function Page({ params }: { params: Promise<{ id: string }> }) {
  return <ContactDetailPage id={(await params).id} />;
}
