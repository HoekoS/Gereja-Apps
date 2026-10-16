import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import BiblePage from "./BiblePage";

type Handler = () => { status?: number; body?: unknown };

const translations = [
  { id: "tb", abbrev: "TB", name: "Terjemahan Baru", language: "id" },
  { id: "tl", abbrev: "TL", name: "Terjemahan Lama", language: "id" },
];

let routes: Record<string, Handler>;

function serve(extra: Record<string, Handler> = {}) {
  routes = {
    "GET /api/translations": () => ({ body: translations }),
    ...extra,
  };
  vi.stubGlobal(
    "fetch",
    vi.fn(async (url: string) => {
      const [path, search] = url.split("?");
      const key = `GET ${path}`;
      const handler = routes[key];
      if (!handler) throw new Error(`Unhandled request: ${key}${search ? `?${search}` : ""}`);
      const { status = 200, body } = handler();
      return new Response(body === undefined ? null : JSON.stringify(body), {
        status,
        headers: { "Content-Type": "application/json" },
      });
    }),
  );
}

beforeEach(() => vi.useFakeTimers({ shouldAdvanceTime: true }));
afterEach(() => {
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

it("looks up a passage from a free-form reference and shows its verses", async () => {
  serve({
    "GET /api/bible/reference": () => ({
      body: { bookId: 43, chapter: 3, verseStart: 16, verseEnd: 16 },
    }),
    "GET /api/bible/passage": () => ({
      body: {
        translationId: "tb",
        bookId: 43,
        bookName: "Yohanes",
        chapter: 3,
        verses: [{ verse: 16, text: "Karena begitu besar kasih Allah akan dunia ini." }],
      },
    }),
  });

  render(<BiblePage />);
  await waitFor(() =>
    expect(screen.getByRole("option", { name: /Terjemahan Baru/ })).toBeInTheDocument(),
  );

  await userEvent.type(screen.getByLabelText("Referensi"), "Yohanes 3:16");
  await vi.advanceTimersByTimeAsync(300);

  await waitFor(() =>
    expect(screen.getByText(/Karena begitu besar kasih Allah/)).toBeInTheDocument(),
  );
  expect(screen.getByRole("heading", { name: "Yohanes 3" })).toBeInTheDocument();
});

it("treats an unparseable reference as an ordinary outcome of typing, not an error", async () => {
  serve({
    "GET /api/bible/reference": () => ({
      status: 404,
      body: {
        error: {
          code: "UNPARSEABLE_REFERENCE",
          message: "'asdf' is not a book, chapter and verse.",
        },
      },
    }),
  });

  render(<BiblePage />);
  await waitFor(() =>
    expect(screen.getByRole("option", { name: /Terjemahan Baru/ })).toBeInTheDocument(),
  );

  await userEvent.type(screen.getByLabelText("Referensi"), "asdf");
  await vi.advanceTimersByTimeAsync(300);

  await waitFor(() => expect(screen.queryByRole("status")).not.toBeInTheDocument());
  expect(screen.queryByText(/not a book, chapter and verse/)).not.toBeInTheDocument();
});
