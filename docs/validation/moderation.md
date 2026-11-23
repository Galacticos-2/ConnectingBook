# Moderation evidence

Validated on 2026-11-23 with generated accounts and an isolated SQL Server database.

- Browser login, pending queue and report details rendered successfully.
- Hide closed the pending report and showed the hidden state.
- Restore returned the original post to Visible, with history retained.
- [Actual demonstration GIF](../demo/moderation.gif) contains before/hidden/restored
  screenshots. Mutations were performed through authenticated API calls against
  synthetic data; the browser displayed the resulting states.
- Integration tests verify duplicate/self-report rejection, member/admin separation,
  retained snapshot access after author deletion, one winner for concurrent decisions,
  and full rollback when report closure fails after the action/post save.
- Report dialog tests cover required Other description, success and duplicate errors.

Browser evidence: [results](browser.json), [desktop feed](../demo/feed-desktop.png),
[mobile feed](../demo/feed-mobile.png). Video playback was confirmed in Chrome after
correcting the service worker's native `no-cors` request handling. Authorization is
forwarded only to the already validated same-origin private media route.
