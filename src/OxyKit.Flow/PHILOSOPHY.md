# Philosophy

## Problem

Automate **delivery** of generated documents to one or more destinations, without owning document generation itself.

Callers (including legacy systems) need a durable request: "with this content, produce this report, then send it here" - safely under retries.

Render engines vary -- modern .NET (OxyKit.Render), legacy 32-bit Framework sidecars (e.g. Crystal Reports), etc. They must stay process-isolated.

## Decision

OxyKit.Flow is an **orchestrator with a job store**:

1. **Accept** -- HTTP job (render spec + outputs); optional `Idempotency-Key`
2. **Persist** -- SQLite job row (Queued) so work survives restarts and duplicate posts
3. **Render** -- call a named HTTP render engine (`IDocumentRenderer` from Abstractionsctions; default `oxykit-render` via Client; third-party adapters via `Kind=assembly` config)
4. **Deliver** -- `IOutputProvider` implementations (`file`, `mail` via OxyKit.Mail, in-process `ftp`/`sftp`; cloud later)
5. **Retry** -- claim/processing status like OxyKit.Mail; dead-letter after max attempts

## Non-goals

- Not a document generator (no in-process CSV/HTML/PDF engines)
- Not a BI / dashboard designer
- Not multi-tenant SaaS / billing
