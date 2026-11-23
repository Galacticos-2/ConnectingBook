# Feed performance — local SQL Server

Measured on 2026-11-23 (Asia/Bangkok), using synthetic data in a dedicated
`ConnectingBook_Test_Performance_...` database. The application database was not seeded.

| Measurement | Result |
|---|---:|
| Users / posts | 1,000 / 100,000 |
| Warmup requests | 15 |
| Measured requests / concurrency | 100 / 5 |
| p50 / p95 / maximum | 6.98 / 13.14 / 23.82 ms |
| Target p95 | <300 ms — passed |

Machine: Intel Core i5-12450H, 12 logical CPUs, 32 GiB RAM; Windows 10.0.26300,
Node 22.14.0, local SQL Server Express. HTTP timing includes HTTPS response consumption.
This is a short local benchmark, not evidence of sustained production capacity.

Reproduce with `node scripts/Validate-Performance.mjs` after building the validation
server and trusting the local HTTPS certificate. The script generates credentials in
memory, creates its own database, verifies row counts, warms up and measures the feed.
It uses Node's existing HTTP tooling instead of adding k6. No paid service is called.

Artifacts: [measurements](feed-benchmark.json), [dataset](performance-dataset.json),
[actual SQL execution plan](feed-query.sqlplan), [IO/TIME](feed-query-statistics.txt).
The plan captures the equivalent first-page selection query; the HTTP benchmark
also includes DTO, media, author and interaction queries. No query optimization was
needed to meet the measured target.

The initial plan export failed because sqlcmd disallows combining `-h -1` with
`-y 0`. This was corrected and the actual plan exported from the same benchmark
database without reseeding or rerunning the successful HTTP measurement.
