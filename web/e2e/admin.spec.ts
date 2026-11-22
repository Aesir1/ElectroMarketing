import { expect, test } from "@playwright/test";
import { login, users } from "./helpers";

test("platform admin opens the queue admin page", async ({ page }) => {
  await login(page, users.admin);
  await page.getByRole("link", { name: "Admin · Queues" }).click();
  await expect(page.getByRole("heading", { name: "Queues" })).toBeVisible();
  await expect(page.getByRole("columnheader", { name: "Queue" })).toBeVisible();
  await expect(page.getByRole("status")).toHaveCount(0);
});

test("a plain user cannot use the queue admin page", async ({ page }) => {
  await login(page, users.user);
  await page.goto("/app/admin/queues");
  await expect(page.getByRole("status")).toContainText(/not authorized/i);
});
