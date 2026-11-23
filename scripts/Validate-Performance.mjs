// Dedicated local SQL Server benchmark; never seeds the application's current database.
import { spawn, execFile } from 'node:child_process'
import { promisify } from 'node:util'
import { randomBytes, randomUUID } from 'node:crypto'
import { createServer } from 'node:net'
import { mkdir, writeFile } from 'node:fs/promises'
import { resolve, join } from 'node:path'

const execute = promisify(execFile)
const root = resolve('.')
const serverRoot = join(root, 'Project-Web-ConnectingBook.Server')
const dll = process.env.VALIDATION_SERVER_DLL ?? join(root, '.local/validation-artifacts/bin/Project-Web-ConnectingBook.Server/debug/Project-Web-ConnectingBook.Server.dll')
const database = `ConnectingBook_Test_Performance_${randomUUID().replaceAll('-', '')}`
const email = `benchmark-${randomUUID()}@example.invalid`
const password = randomBytes(24).toString('base64')
const env = {
  ...process.env,
  ConnectionStrings__Default: String.raw`Server=.\SQLEXPRESS;Database=${database};Integrated Security=True;Encrypt=True;TrustServerCertificate=True`,
  Jwt__SigningKey: randomBytes(48).toString('base64'),
  DEMO_EMAIL: email, DEMO_PASSWORD: password, ADMIN_EMAIL: '', ADMIN_PASSWORD: '',
  ASPNETCORE_ENVIRONMENT: 'Development', Storage__Provider: 'Local',
  Storage__LocalPath: join(root, '.local', `performance-storage-${randomUUID()}`),
}
const delay = ms => new Promise(resolve => setTimeout(resolve, ms))
async function freePort() {
  const listener = createServer()
  await new Promise(resolve => listener.listen(0, '127.0.0.1', resolve))
  const port = listener.address().port
  await new Promise(resolve => listener.close(resolve))
  return port
}
async function run(file, args, output = false) {
  await new Promise((resolve, reject) => {
    const child = spawn(file, args, { cwd: root, env, windowsHide: true, stdio: output ? ['ignore', 'inherit', 'inherit'] : 'ignore' })
    child.once('error', reject)
    child.once('exit', code => code === 0 ? resolve() : reject(new Error(`Benchmark command failed (exit ${code}). No credentials are printed.`)))
  })
}
console.log('Seeding an isolated performance database with 1,000 users and 100,000 posts. This may take a few minutes.')
await run('dotnet', [dll, '--seed-performance', '--contentRoot', serverRoot])
const countResult = await execute('sqlcmd', ['-S', String.raw`.\SQLEXPRESS`, '-E', '-d', database, '-h', '-1', '-W', '-s', ',', '-Q', 'SET NOCOUNT ON; SELECT (SELECT COUNT(*) FROM dbo.Users), (SELECT COUNT(*) FROM dbo.Posts);'], { cwd: root, env, windowsHide: true, maxBuffer: 1024 * 1024 })
const counts = countResult.stdout.match(/\b(\d+),(\d+)\b/)
if (!counts || Number(counts[1]) !== 1000 || Number(counts[2]) !== 100000) throw new Error('Dedicated benchmark dataset has unexpected counts.')
env.PERF_USERS = counts[1]; env.PERF_POSTS = counts[2]
const port = await freePort()
env.ASPNETCORE_URLS = `https://localhost:${port}`
env.Client__Origin = env.ASPNETCORE_URLS
env.PERF_ORIGIN = env.ASPNETCORE_URLS
const app = spawn('dotnet', [dll, '--contentRoot', serverRoot], { cwd: root, env, windowsHide: true, stdio: 'ignore' })
try {
  let ready = false
  for (let index = 0; index < 60; index++) {
    try { if ((await fetch(`${env.PERF_ORIGIN}/health/ready`)).ok) { ready = true; break } } catch { /* startup */ }
    await delay(500)
  }
  if (!ready) throw new Error('Benchmark HTTPS server was not ready. Certificate checks are not bypassed.')
  await run(process.execPath, [join(root, 'scripts/performance/feed.mjs')], true)
  const query = `SET NOCOUNT ON; DECLARE @me uniqueidentifier = (SELECT Id FROM dbo.Users WHERE Email = N'${email}'); SET STATISTICS IO ON; SET STATISTICS TIME ON; SET STATISTICS XML ON; SELECT TOP(21) p.* FROM dbo.Posts p WHERE p.Status = N'Visible' AND (p.AuthorId = @me OR EXISTS(SELECT 1 FROM dbo.Follows f WHERE f.FollowerId = @me AND f.FollowedId = p.AuthorId)) ORDER BY p.PublishedAt DESC,p.Id DESC; SET STATISTICS XML OFF;`
  const plan = await execute('sqlcmd', ['-S', String.raw`.\SQLEXPRESS`, '-E', '-d', database, '-y', '0', '-w', '65535', '-Q', query], { cwd: root, env, windowsHide: true, maxBuffer: 16 * 1024 * 1024 })
  const xml = plan.stdout.match(/<ShowPlanXML[\s\S]*?<\/ShowPlanXML>/)?.[0]
  if (!xml) throw new Error('SQL Server did not produce an actual execution plan.')
  await mkdir(join(root, 'docs/validation'), { recursive: true })
  await writeFile(join(root, 'docs/validation/feed-query.sqlplan'), xml + '\n')
  const statistics = (plan.stdout + '\n' + plan.stderr).split(/\r?\n/).filter(line => /logical reads|physical reads|CPU time|elapsed time|SQL Server Execution Times|SQL Server parse and compile time/i.test(line))
  await writeFile(join(root, 'docs/validation/feed-query-statistics.txt'), statistics.join('\n') + '\n')
  await writeFile(join(root, 'docs/validation/performance-dataset.json'), JSON.stringify({ database, users: 1000, posts: 100000, sqlServer: 'local SQLEXPRESS', syntheticDataOnly: true }, null, 2) + '\n')
  console.log('Actual SQL Server plan and IO/TIME statistics saved. Dedicated benchmark database retained for reproduction.')
} finally { app.kill() }
