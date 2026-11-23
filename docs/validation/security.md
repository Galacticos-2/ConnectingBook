# Security and API review — 2026-11-23

Reviewed controller DTOs, authentication configuration, exception handling, media
authorization and frontend rendering/token transport without opening secret files.

- Public registration/profile DTOs do not expose role setters. Role injection into
  profile JSON cannot grant administrative access.
- Access tokens remain in application/worker memory; no localStorage/sessionStorage
  use or token-bearing media URLs found. Worker tokens are scoped to browser clients.
- Passwords use ASP.NET Core PasswordHasher; refresh token hashes/families are stored
  with rotation and replay revocation. Concurrent refresh has a single winner and
  replay revokes the winning family. Cookies are HttpOnly/Secure/SameSite Strict.
- Media authorization occurs before ETag/Range responses. Foreign unpublished
  media returns 404 even with a matching ETag. Deleted report evidence requires the
  separate Admin route. Image decode/re-encode strips EXIF and applies orientation.
- Errors expose stable ProblemDetails codes and correlation IDs. Unexpected errors
  log only exception type/correlation; worker failures log only exception type.
- OpenAPI JSON and Swagger assets require authentication outside Development.
  In Development the owner requested direct standalone Swagger access; application
  API authentication and Admin authorization remain enforced. Error documentation covers
  input/auth/ownership/conflicts/rate limits and upload type/size errors.
- Posts/comments use React text rendering. The API documentation iframe builds its
  HTML from trusted local Swagger assets and an escaped JSON document; user content
  is not inserted into it. Swagger authorization persistence is disabled.
- Security headers include nosniff, frame denial, referrer policy and correlation ID.
- Development can generate a signing key in memory when absent; Production rejects
  missing keys and every environment rejects an explicitly undersized key.

Evidence is in the real SQL Server integration suites and
[browser results](browser.json). This is an implementation review and targeted test
evidence, not a third-party penetration test. No external scanner or paid API ran.
