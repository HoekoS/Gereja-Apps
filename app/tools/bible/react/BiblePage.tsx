import { useEffect, useRef, useState } from "react";
import { api, ApiError } from "../../../dls/react/backend";
import {
  Card,
  Field,
  FormField,
  Select,
  Callout,
  StatusMessage,
} from "../../../dls/react/components/index";

type Translation = { id: string; abbrev: string; name: string; language: string };
type VerseLine = { verse: number; text: string };
type Passage = {
  translationId: string;
  bookId: number;
  bookName: string;
  chapter: number;
  verses: VerseLine[];
};
type ParsedReference = {
  bookId: number;
  chapter: number;
  verseStart: number;
  verseEnd: number | null;
};

// The operator's fastest path to a passage mid-service (FR-LIB-08): type a
// free-form reference, see the verses, switch translation without losing
// the place (FR-LIB-06). Debounced so most keystrokes — which are not yet a
// complete reference — don't each fire a request.
const DEBOUNCE_MS = 250;

export default function BiblePage() {
  const [translations, setTranslations] = useState<Translation[] | null>(null);
  const [translationId, setTranslationId] = useState("");
  const [query, setQuery] = useState("");
  const [passage, setPassage] = useState<Passage | null>(null);
  // "quiet" covers an in-progress reference and one that failed to parse —
  // both are an ordinary state of typing (SYS-BIB-07), not an error, so
  // nothing alarms the operator over either one.
  const [status, setStatus] = useState<"idle" | "searching" | "quiet" | "error">("idle");
  const [errorMessage, setErrorMessage] = useState("");
  const latest = useRef(0);

  useEffect(() => {
    api<Translation[]>("/translations").then(
      (list) => {
        setTranslations(list);
        setTranslationId((current) => current || list[0]?.id || "");
      },
      () => {
        setStatus("error");
        setErrorMessage("Tidak bisa memuat daftar terjemahan. Periksa sambungan ke server.");
      },
    );
  }, []);

  useEffect(() => {
    const requestId = ++latest.current;
    if (!query.trim() || !translationId) {
      setPassage(null);
      setStatus("idle");
      return;
    }
    setStatus("searching");
    const timer = setTimeout(() => {
      void lookup(requestId);
    }, DEBOUNCE_MS);
    return () => clearTimeout(timer);

    async function lookup(id: number) {
      try {
        const reference = await api<ParsedReference>(
          `/bible/reference?q=${encodeURIComponent(query)}`,
        );
        if (id !== latest.current) return;

        const params = new URLSearchParams({
          translationId,
          bookId: String(reference.bookId),
          chapter: String(reference.chapter),
          verseStart: String(reference.verseStart),
        });
        if (reference.verseEnd != null) params.set("verseEnd", String(reference.verseEnd));

        const result = await api<Passage>(`/bible/passage?${params}`);
        if (id !== latest.current) return;
        setPassage(result);
        setStatus("idle");
      } catch (error) {
        if (id !== latest.current) return;
        setPassage(null);
        if (error instanceof ApiError && error.code !== "UNPARSEABLE_REFERENCE") {
          setStatus("error");
          setErrorMessage(error.message);
        } else {
          setStatus("quiet");
        }
      }
    }
  }, [query, translationId]);

  return (
    <>
      <header className="dls-page-header">
        <div>
          <h1>Alkitab</h1>
          <p>Ketik referensi bebas, misalnya "Yohanes 3:16" atau "Mazmur 23".</p>
        </div>
      </header>
      <main className="dls-page bible-page">
        <Card className="bible-controls">
          <FormField id="bible-translation" label="Terjemahan">
            <Select
              id="bible-translation"
              value={translationId}
              onChange={(event) => setTranslationId(event.target.value)}
              disabled={!translations?.length}
            >
              {translations?.map((translation) => (
                <option key={translation.id} value={translation.id}>
                  {translation.name} ({translation.abbrev})
                </option>
              ))}
            </Select>
          </FormField>
          <Field
            id="bible-reference"
            label="Referensi"
            placeholder="Yohanes 3:16"
            autoComplete="off"
            value={query}
            onChange={(event) => setQuery(event.target.value)}
          />
        </Card>

        {status === "error" && <Callout tone="warning">{errorMessage}</Callout>}

        {passage && (
          <Card className="bible-passage">
            <h2>
              {passage.bookName} {passage.chapter}
            </h2>
            <div className="bible-verses">
              {passage.verses.map((verse) => (
                <p key={verse.verse}>
                  <span className="bible-verse-number">{verse.verse}</span> {verse.text}
                </p>
              ))}
            </div>
          </Card>
        )}

        {status === "searching" && <StatusMessage>Mencari…</StatusMessage>}
      </main>
    </>
  );
}
