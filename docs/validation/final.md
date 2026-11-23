# Local acceptance status — 2026-11-23

Startup follow-up: the existing server returned 200 with actual Swagger UI HTML at
`https://localhost:7041/swagger/index.html`. Both launch profiles had launchBrowser
disabled. Enabled browser launch and set launchUrl to swagger/index.html for both
profiles; parsed JSON and verified those fields. No application build is needed
for this launch-setting-only change. Visual Studio must start a new debug session.

Owner-requested standalone Swagger: Development now serves Swagger UI/assets and
OpenAPI anonymously so `/swagger/index.html` opens directly. Other environments
retain authenticated documentation. API endpoint authorization is unchanged;
the development integration test checks anonymous feed/Admin calls remain 401.

Follow-up development feed fix: StrictMode's setup/cleanup replay could dispatch a
feed request and immediately abort it, surfacing TaskCanceledException in the SQL
query under the debugger. FeedPage now starts loading in a guarded microtask,
skips discarded effects and ignores stale successful responses after cancellation.
StrictMode and immediate-unmount regressions passed; current client build/lint and
all 9 frontend tests passed. Request cancellation on actual navigation is retained.

Follow-up startup/debug fix: missing, empty or oversized refresh cookies now produce
401 `invalid_session` ProblemDetails directly rather than throwing from ReadCookie.
This preserves the session contract while avoiding a debugger break during the
anonymous startup refresh. The new controller regression test passed for refresh
and logout; all 5 SQL Server AuthTests passed, including replay and concurrent
refresh. No change to token validation, role authorization or database schema.

Follow-up fix: Vite development requests previously returned 404 because no API
proxy was configured. Added forwarding for API/OpenAPI/Swagger/health, aligned the
backend SPA proxy with localhost:5173, and preserved TLS validation and rejection
of foreign Origins. Client build/lint/7 tests and solution build pass after this
change. [Proxy regression results](dev-proxy.json) use a temporary HTTP API stub;
no actual account was created and no database was accessed during that check.

Local implementation and verification have advanced to 54/55 tracked tasks (98%).
T054 remains open because container/native Linux/MinIO smoke was not run. This
percentage is task completion, not a quantitative guarantee of production readiness.

| Check | Actual outcome |
|---|---|
| Solution build, isolated artifacts, BuildClient=false | Passed, 0 warnings/errors |
| SQL Server backend tests | 50 passed, 0 failed, 0 skipped |
| Frontend build / lint | Passed |
| Frontend tests | 9 passed across 3 files |
| Backend and test C# whitespace verification | Passed |
| Chrome desktop/mobile + private video | Passed |
| Report hide/restore demonstration | Recorded actual GIF |
| Demo seed twice, same new database | Passed; includes generated reel |
| Feed performance, 1,000 users / 100,000 posts | p95 13.14 ms, target <300 ms |
| Compose quiet configuration validation | Passed; Docker CLI warned that its user config was inaccessible |
| Docker/Linux/MinIO runtime | Not run; normal development uses local SQL Server/storage |
| Hosted GitHub CI | Not run; no push or hosted execution authorized |

Commands executed from the workspace unless otherwise stated:

```powershell
dotnet build Project-Web-ConnectingBook.sln --no-restore -p:UseArtifactsOutput=true -p:ArtifactsPath=D:/CV/Project-Web-ConnectingBook/.local/validation-artifacts -p:BuildClient=false
# ConnectionStrings__Test supplied through the process environment, Windows Authentication.
dotnet test tests/ConnectingBook.Server.Tests/ConnectingBook.Server.Tests.csproj --no-restore -p:UseArtifactsOutput=true -p:ArtifactsPath=D:/CV/Project-Web-ConnectingBook/.local/validation-artifacts -p:BuildClient=false --verbosity minimal
```

`dotnet format whitespace <project.csproj> --verify-no-changes --no-restore` passed
for the backend and tests. Only implementation files already in scope were formatted.
From the client: `npm run build`, `npm run lint`, `npm run test -- --run` passed.
`node scripts/Validate-Browser.mjs` and `node scripts/Validate-Performance.mjs`
recorded actual local results. Node used the existing trusted localhost public
certificate through NODE_EXTRA_CA_CERTS; certificate validation was not disabled.
Compose used dummy process-only interpolation values and `docker compose config --quiet`.
No container was started and no Docker secret/config file was opened by the agent.

Acceptance evidence:

- Accounts/session, five-failure lockout, replay, concurrent refresh and role restrictions:
  AuthTests, OperationsTests and PostsMediaTests.
- Profile/follow ownership; image type/size/corruption/dimensions/avatar/EXIF and media ACL:
  PostsMediaTests and ImageEvidenceTests.
- Feed/comment cursor ties, inserts, deleted anchor, hide/unfollow, limits and ownership:
  CursorTests, FeedTests and InteractionTests.
- Moderation privacy/snapshots/restore, competing decisions/edits/deletes/reports and
  failed transaction rollback: ModerationTests and ModerationConcurrencyTests.
- Actual FFmpeg MP4, authenticated Range, concurrent claim, duration/size rejection,
  retries, stale state policy, deletion during processing and output cleanup:
  ReelsTests, ReelStateTests and ReelDeletionRaceTests.
- Operational evidence: [browser](browser.json), [moderation](moderation.md),
  [performance](performance.md), [security review](security.md).

Review of changes in this continuation:

- Fixed service-worker Authorization transport for native video requests and extended
  the regression test using a no-cors input request. Chrome confirmed playback.
- Extended SQL Server tests for media, feed/comments, authentication and transaction races.
- Added a generated sample reel to demo-only seed, retaining the existing guard that
  refuses the application database. Browser QA repeated the seed successfully.
- Added local browser/performance evidence and synchronized README/quickstart/tasks.
- No dependency, schema migration or application database data changed in this continuation.

Git metadata is absent (`git rev-parse --git-dir` reports not a repository). Review
therefore used the intended edits and current source/artifacts; a repository-wide
baseline diff cannot be independently reconstructed. No commit, push or deployment.
No paid APIs, cloud resources or billing actions were used. Actual application data
was not seeded/reset; browser/performance databases contain synthetic data and are
retained for inspection. Test fixtures removed only their own generated databases.
