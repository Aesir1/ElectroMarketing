import { expect, test } from "@playwright/test";
import { login, unique, users } from "./helpers";

test("start screen is branded", async ({ page }) => {
  await page.goto("/");
  await expect(page).toHaveTitle("ElectroMarketing");
  await expect(page.getByRole("heading", { name: "ElectroMarketing" })).toBeVisible();
  await expect(page.getByText("Your marketing, in one place.")).toBeVisible();
  await expect(page.getByTestId("start-screen")).toHaveCSS("background-image", /login-bg\.svg/);
});

test("unauthenticated /app redirects to the start screen", async ({ page }) => {
  await page.goto("/app/documents");
  await expect(page).toHaveURL(/\/($|\?)/);
  await expect(page.getByRole("button", { name: "Sign in" })).toBeVisible();
});

test("user signs in, sees no admin link, and signs out of Keycloak too", async ({ page }) => {
  await login(page, users.user);
  await expect(page.getByRole("link", { name: "Documents" })).toBeVisible();
  await expect(page.getByRole("link", { name: "Admin · Queues" })).toHaveCount(0);

  await page.getByRole("button", { name: "Sign out" }).click();
  await expect(page.getByRole("button", { name: "Sign in" })).toBeVisible();
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page.getByLabel("Email")).toBeVisible(); // SSO session ended: Keycloak asks again
});
