import { cleanup } from "@testing-library/react";
import { afterAll, afterEach, beforeAll, vi } from "vitest";
import { server } from "./src/test/server";

beforeAll(() => {
  vi.stubGlobal("confirm", () => true);
  server.listen({ onUnhandledRequest: "error" });
});
afterEach(() => {
  server.resetHandlers();
  cleanup();
});
afterAll(() => server.close());
