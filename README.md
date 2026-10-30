# AI Writing Coach

A web app for AI-assisted English writing practice and proofreading. Write an email or an exam-style task
(A2, B1, B2, C1: essay, email/letter, report, review, article, proposal...), send it to an LLM for checking
against selectable criteria, and review the corrections in a locally computed diff where every change is linked
to the AI's explanation and can be accepted or rejected individually.

> **Status:** first iteration – the core write → check → review → apply loop, saved texts and history.
> Progress charts, the vocabulary list and multi-user mode are planned (see [Roadmap](#roadmap)).

## Features (current)

- **Write** screen with profiles/presets (B2 Essay, B2 Email, C1 Report, Business email, …) that bundle level,
  task type, criteria, model and word limits, plus a *Criteria* flyout for adding custom criteria
  (formality, tone, conciseness, clarity, length).
- **AI check** via [OpenRouter](https://openrouter.ai): corrected text, structured error list (original, correction,
  category, subcategory, explanation, severity), vocabulary upgrades with CEFR level, estimated level,
  per-criterion scores (0–5) and overall feedback. Structured JSON output is validated, and invalid answers are
  retried with a repair prompt.
- **Diff view** computed locally with DiffPlex (word level) in two modes: side-by-side (WinMerge style, line-aligned)
  and inline. Clicking a change highlights its explanation and vice versa; accept/reject per change or all at once,
  then apply the result back into the editor.
- **History**: every text and every check is saved, so you can come back later.
- **Usage log**: tokens, estimated cost and latency of every LLM request (for future pricing decisions).
- **UI in English and Bulgarian** (`.resx`), light/dark theme.

## Tech stack

.NET 10 · Blazor Web App (Interactive Server) · MudBlazor · EF Core 10 (SQL Server by default; PostgreSQL / SQLite
switchable) · Microsoft.Extensions.AI + OpenAI .NET SDK pointed at OpenRouter · DiffPlex · xUnit.

## Solution structure

| Project | Responsibility |
| --- | --- |
| `src/AIWritingCoach.Core` | Domain model, AI contract (`ReviewRequest`/`ReviewResult` + validator), diff model, interfaces (`ICurrentUser`, `IAiReviewService`, repositories), `WritingCoachService` orchestration. No EF/UI/HTTP dependencies. |
| `src/AIWritingCoach.Infrastructure` | EF Core `AppDbContext` (Identity-ready, per-user query filters), repositories, JSON seed data, OpenRouter chat pipeline (fallback/backoff, usage logging, pricing), review service, DiffPlex diff service. |
| `src/AIWritingCoach.Migrations.SqlServer` | SQL Server migrations (one migrations assembly per provider). |
| `src/AIWritingCoach.Web` | Blazor UI, `Program.cs` with app-mode and provider switching, localization. |
| `tests/AIWritingCoach.Tests` | Unit tests (diff, parsing/validation, fallback, review service) and data tests on in-memory SQLite. |

Rubrics, profiles and prompt templates are **data**: JSON files under
`src/AIWritingCoach.Infrastructure/Seed/` are upserted into the database at startup (a system row is replaced only
when its seed `version` increases).

## Getting started

Prerequisites: [.NET SDK 10](https://dotnet.microsoft.com/download), SQL Server (a local default instance works;
change the connection string otherwise) and an [OpenRouter API key](https://openrouter.ai/keys).

```bash
dotnet user-secrets set "OpenRouter:ApiKey" "sk-or-..." --project src/AIWritingCoach.Web
```

```bash
dotnet run --project src/AIWritingCoach.Web
```

Then open <http://localhost:5155>. The database is created and migrated on first start.

On Windows you can instead double-click `start.cmd`: it runs the app with `dotnet watch` (hot reload) and opens the
browser automatically.

### Configuration

All settings live in `src/AIWritingCoach.Web/appsettings.json`; secrets never go there.

| Setting | Meaning |
| --- | --- |
| `App:Mode` | `Personal` (no login, one fixed user, localhost only – the app refuses to start on any other address) or `MultiUser` (planned). |
| `Database:Provider` | `SqlServer` (default), `PostgreSql` or `Sqlite`; the matching `ConnectionStrings:<Provider>` is used. |
| `Database:MigrateOnStartup` | Apply pending migrations at startup. |
| `OpenRouter:DefaultModel`, `OpenRouter:FallbackModels` | Model chain tried in order. |
| `OpenRouter:MaxRetriesPerModel`, `BaseRetryDelay`, `MaxRetryDelay` | Backoff on 429/5xx. |

In Docker or other hosts use environment variables, e.g. `OpenRouter__ApiKey`, `ConnectionStrings__SqlServer`.

### Migrations

```bash
dotnet tool restore
```

```bash
dotnet ef migrations add <Name> -p src/AIWritingCoach.Migrations.SqlServer -s src/AIWritingCoach.Migrations.SqlServer
```

Set `AIWRITINGCOACH_SQLSERVER` to point the design-time factory at another server. PostgreSQL and SQLite get their
own `AIWritingCoach.Migrations.<Provider>` projects following the same pattern.

### Tests

```bash
dotnet test
```

## Roadmap

- Progress: score trends, most frequent error categories, misspelled words.
- Vocabulary: words to start using, mark as learned.
- Settings: model selection, feedback language, theme persistence.
- PostgreSQL and SQLite migration projects.
- MultiUser mode: ASP.NET Core Identity, then OIDC, usage limits and payments.
- Self-contained single-file build that opens the browser; Docker Compose for self-hosting.

## Disclaimer

"Cambridge-style" rubrics are the author's own descriptions modelled on the publicly known structure of the exams.
This project is not affiliated with or endorsed by Cambridge University Press & Assessment.
