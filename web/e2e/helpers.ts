import { expect, type Page } from "@playwright/test";

export const users = {
  user: { email: "user@local.test", password: process.env.E2E_USER_PASSWORD ?? "34b11a79a75c2d2324e7e3e72c6fb36a" },
  admin: { email: "admin@local.test", password: process.env.E2E_ADMIN_PASSWORD ?? "d605432b54bd51c214494ac5fc1fd9f8" },
};

/** Start screen → Keycloak → back in /app. */
export async function login(page: Page, { email, password }: { email: string; password: string }) {
  await page.goto("/");
  await page.getByRole("button", { name: "Sign in or register" }).click();
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Password", { exact: true }).fill(password);
  await page.getByRole("button", { name: "Sign In" }).click();
  await expect(page).toHaveURL(/\/app\/documents$/);
}

export const unique = (prefix: string) =>
  `${prefix}${Date.now().toString(36).toUpperCase()}${Math.floor(Math.random() * 1000)}`;
