import { Suspense } from "react";
import { ContactsPage } from "@/components/ContactsPage";

export const metadata = { title: "Contacts" };

export default function Page() {
  return (
    <Suspense>
      <ContactsPage />
    </Suspense>
  );
}
