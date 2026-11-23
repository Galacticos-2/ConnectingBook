// Isolated local QA only. Uses the installed Chrome/Edge, Node built-ins and generated test data.
import { spawn } from 'node:child_process'
import { randomBytes, randomUUID } from 'node:crypto'
import { createServer } from 'node:net'
import { mkdir, writeFile, readFile, copyFile, readdir } from 'node:fs/promises'
import { existsSync } from 'node:fs'
import { resolve, join } from 'node:path'

const root = resolve('.')
const serverRoot = join(root, 'Project-Web-ConnectingBook.Server')
const dll = process.env.VALIDATION_SERVER_DLL ?? join(root, '.local/validation-artifacts/bin/Project-Web-ConnectingBook.Server/debug/Project-Web-ConnectingBook.Server.dll')
const owned = join(root, '.local', `browser-qa-${randomUUID()}`)
const database = `ConnectingBook_Test_Browser_${randomUUID().replaceAll('-', '')}`
const password = randomBytes(24).toString('base64')
const adminPassword = randomBytes(24).toString('base64')
const email = `member-${randomUUID()}@example.invalid`
const adminEmail = `admin-${randomUUID()}@example.invalid`
const env = {
  ...process.env,
  ConnectionStrings__Default: String.raw`Server=.\SQLEXPRESS;Database=${database};Integrated Security=True;Encrypt=True;TrustServerCertificate=True`,
  Jwt__SigningKey: randomBytes(48).toString('base64'),
  DEMO_EMAIL: email, DEMO_PASSWORD: password, ADMIN_EMAIL: adminEmail, ADMIN_PASSWORD: adminPassword,
  ASPNETCORE_ENVIRONMENT: 'Development',
  Storage__Provider: 'Local', Storage__LocalPath: join(owned, 'media'),
}
const chromePath = ['C:/Program Files/Google/Chrome/Application/chrome.exe', 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'].find(existsSync)
if (!chromePath) throw new Error('An installed local Chrome/Edge is required; no browser is downloaded.')
const delay = ms => new Promise(resolve => setTimeout(resolve, ms))
async function freePort() {
  const server = createServer()
  await new Promise((resolve, reject) => { server.once('error', reject); server.listen(0, '127.0.0.1', resolve) })
  const port = server.address().port
  await new Promise(resolve => server.close(resolve))
  return port
}
async function run(file, args) {
  await new Promise((resolve, reject) => {
    const child = spawn(file, args, { cwd: root, env, windowsHide: true, stdio: 'ignore' })
    child.once('error', reject)
    child.once('exit', code => code === 0 ? resolve() : reject(new Error(`${file} failed (exit ${code}). No credentials or process output are printed.`)))
  })
}
async function copyTree(source, target) {
  await mkdir(target, { recursive: true })
  for (const entry of await readdir(source, { withFileTypes: true })) {
    if (entry.isDirectory()) await copyTree(join(source, entry.name), join(target, entry.name))
    else await copyFile(join(source, entry.name), join(target, entry.name))
  }
}
class CDP {
  constructor(url) {
    this.socket = new WebSocket(url)
    this.counter = 0; this.pending = new Map()
    this.ready = new Promise((resolve, reject) => { this.socket.addEventListener('open', resolve, { once: true }); this.socket.addEventListener('error', reject, { once: true }) })
    this.socket.addEventListener('message', event => {
      const message = JSON.parse(event.data)
      const request = this.pending.get(message.id)
      if (request) { this.pending.delete(message.id); clearTimeout(request.timeout); message.error ? request.reject(new Error(message.error.message)) : request.resolve(message.result) }
    })
  }
  async send(method, params = {}) {
    await this.ready
    const id = ++this.counter
    return new Promise((resolve, reject) => {
      const timeout = setTimeout(() => { this.pending.delete(id); reject(new Error(`Browser command timed out: ${method}`)) }, 15000)
      this.pending.set(id, { resolve, reject, timeout })
      this.socket.send(JSON.stringify({ id, method, params }))
    })
  }
  async evaluate(expression) {
    const result = await this.send('Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true })
    if (result.exceptionDetails) throw new Error('The local page could not complete the QA action.')
    return result.result.value
  }
  async until(expression, label, attempts = 60) {
    for (let index = 0; index < attempts; index++) { if (await this.evaluate(expression)) return; await delay(250) }
    if (label.includes('video')) console.log(JSON.stringify(await this.evaluate("({videos:[...document.querySelectorAll('video')].map(v=>({readyState:v.readyState,networkState:v.networkState,errorCode:v.error?.code})),playerErrors:[...document.querySelectorAll('.video-player .error')].map(e=>e.textContent),secure:isSecureContext,controlled:!!navigator.serviceWorker?.controller})")))
    throw new Error(`Browser state not reached: ${label}`)
  }
  async capture(path) {
    const screenshot = await this.send('Page.captureScreenshot', { format: 'png', captureBeyondViewport: false })
    await writeFile(path, Buffer.from(screenshot.data, 'base64'))
  }
}
await mkdir(owned, { recursive: true })
await mkdir(join(root, 'docs', 'demo'), { recursive: true })
await mkdir(join(root, 'docs', 'validation'), { recursive: true })
await copyTree(join(root, 'project-web-connectingbook.client/dist'), join(serverRoot, 'wwwroot'))
console.log('Preparing an isolated browser QA database and generated demo accounts; no credentials are printed.')
await run('dotnet', [dll, '--seed-demo', '--contentRoot', serverRoot])
await run('dotnet', [dll, '--seed-demo', '--contentRoot', serverRoot]) // Idempotent re-run, same owned database.
const port = await freePort()
const debugPort = await freePort()
const base = `https://localhost:${port}`
env.ASPNETCORE_URLS = base; env.Client__Origin = base
const app = spawn('dotnet', [dll, '--contentRoot', serverRoot], { cwd: root, env, windowsHide: true, stdio: 'ignore' })
let browserProcess
let cdp
const report = { database, origin: base, isolated: true, checks: {}, images: [] }
try {
  let ready = false
  for (let index = 0; index < 60; index++) {
    try { if ((await fetch(`${base}/health/ready`)).ok) { ready = true; break } } catch { /* startup */ }
    await delay(500)
  }
  if (!ready) throw new Error('Local HTTPS readiness failed; no certificate validation bypass is used.')
  async function request(path, token, options = {}) {
    const headers = new Headers(options.headers)
    if (token) headers.set('Authorization', `Bearer ${token}`)
    if (options.body && !(options.body instanceof FormData)) headers.set('Content-Type', 'application/json')
    const response = await fetch(`${base}${path}`, { ...options, headers })
    if (!response.ok) throw new Error(`Isolated QA request failed: ${path.split('?')[0]} HTTP ${response.status}`)
    return response.status === 204 ? null : response.json()
  }
  const member = await request('/api/auth/login', null, { method: 'POST', body: JSON.stringify({ email, password }) })
  const admin = await request('/api/auth/login', null, { method: 'POST', body: JSON.stringify({ email: adminEmail, password: adminPassword }) })
  const png = join(owned, 'sample.png')
  const mp4 = join(owned, 'sample.mp4')
  await run('ffmpeg', ['-nostdin', '-v', 'error', '-y', '-f', 'lavfi', '-i', 'color=c=0x248b65:s=640x360', '-frames:v', '1', '-threads', '1', png])
  const picture = new FormData(); picture.append('file', new Blob([await readFile(png)], { type: 'image/png' }), 'generated-demo.png')
  const image = await request('/api/media/images', member.accessToken, { method: 'POST', body: picture })
  await request('/api/posts', member.accessToken, { method: 'POST', body: JSON.stringify({ text: 'Một khoảnh khắc mới để chia sẻ. Ảnh minh họa được tạo cục bộ cho bản demo.', imageIds: [image.imageId] }) })
  await run('ffmpeg', ['-nostdin', '-v', 'error', '-y', '-f', 'lavfi', '-i', 'testsrc=size=320x180:rate=24:duration=2', '-c:v', 'libx264', '-pix_fmt', 'yuv420p', '-threads', '1', mp4])
  const movie = new FormData(); movie.append('file', new Blob([await readFile(mp4)], { type: 'video/mp4' }), 'generated-demo.mp4'); movie.append('caption', 'Video thử nghiệm được tạo cục bộ.')
  const reel = await request('/api/reels', member.accessToken, { method: 'POST', body: movie })
  let published = false
  for (let index = 0; index < 60; index++) { const own = await request('/api/me/posts', member.accessToken); const item = own.items.find(item => item.id === reel.id); if (item?.status === 'Visible') { published = true; break }; if (item?.status === 'Failed') throw new Error('The demo reel failed processing.'); await delay(500) }
  if (!published) throw new Error('The demo reel did not publish in time.')
  report.checks.reelPublished = true
  browserProcess = spawn(chromePath, ['--headless=new', '--disable-gpu', '--no-first-run', '--no-default-browser-check', '--noerrdialogs', `--remote-debugging-port=${debugPort}`, '--remote-debugging-address=127.0.0.1', `--user-data-dir=${join(owned, 'browser-profile')}`, 'about:blank'], { windowsHide: true, stdio: 'ignore' })
  let target
  for (let index = 0; index < 60; index++) {
    try { const targets = await (await fetch(`http://127.0.0.1:${debugPort}/json/list`)).json(); target = targets.find(item => item.type === 'page'); if (target) break } catch { /* browser startup */ }
    await delay(250)
  }
  if (!target) throw new Error('Installed browser did not expose the isolated QA session.')
  cdp = new CDP(target.webSocketDebuggerUrl)
  await cdp.send('Page.enable'); await cdp.send('Runtime.enable')
  await cdp.send('Emulation.setDeviceMetricsOverride', { width: 1360, height: 1000, deviceScaleFactor: 1, mobile: false })
  await cdp.send('Page.navigate', { url: base })
  await cdp.until("document.querySelector('input[name=email]') !== null", 'login form')
  async function loginBrowser(loginEmail, loginPassword) {
    await cdp.evaluate(`(() => { const email = document.querySelector('input[name=email]'); const password = document.querySelector('input[name=password]'); email.value=${JSON.stringify(loginEmail)}; password.value=${JSON.stringify(loginPassword)}; email.form.requestSubmit(); return true })()`)
    await cdp.until("document.querySelector('.topbar-user') !== null", 'authenticated workspace')
  }
  await loginBrowser(email, password)
  await cdp.until("document.querySelectorAll('.post-card').length > 0", 'loaded feed')
  await cdp.until("document.querySelector('video')?.readyState >= 1", 'authenticated video metadata')
  await cdp.evaluate("(async () => { const video=document.querySelector('video'); video.muted=true; await video.play(); return true })()")
  await cdp.until("document.querySelector('video')?.readyState >= 2", 'authenticated video first frame')
  report.checks.browserVideoReady = true
  report.checks.noLocalStorageTokens = await cdp.evaluate('localStorage.length === 0')
  report.checks.desktopNoOverflow = await cdp.evaluate('document.documentElement.scrollWidth <= innerWidth')
  await cdp.capture(join(root, 'docs/demo/feed-desktop.png')); report.images.push('docs/demo/feed-desktop.png')
  await cdp.send('Emulation.setDeviceMetricsOverride', { width: 390, height: 844, deviceScaleFactor: 1, mobile: true })
  report.checks.mobileNoOverflow = await cdp.evaluate('document.documentElement.scrollWidth <= innerWidth')
  await cdp.capture(join(root, 'docs/demo/feed-mobile.png')); report.images.push('docs/demo/feed-mobile.png')
  await cdp.send('Emulation.setDeviceMetricsOverride', { width: 1360, height: 1000, deviceScaleFactor: 1, mobile: false })
  // Logout through the API and reload the browser. The previous in-memory access token is discarded.
  await cdp.evaluate("(() => { document.querySelector('.sidebar-bottom button').click(); return true })()")
  await cdp.until("document.querySelector('input[name=email]') !== null", 'logged-out login form')
  await loginBrowser(adminEmail, adminPassword)
  await cdp.evaluate("location.hash='moderation'; true")
  await cdp.until("document.querySelector('.queue-item') !== null", 'pending moderation queue')
  await cdp.evaluate("document.querySelector('.queue-item').click(); true")
  await cdp.until("document.querySelector('.moderation-detail') !== null", 'report detail')
  await cdp.capture(join(root, 'docs/demo/moderation-before.png'))
  const queue = await request('/api/admin/report-groups', admin.accessToken)
  const postId = queue.items[0].postId
  const detail = await request(`/api/admin/report-groups/${postId}`, admin.accessToken)
  await request(`/api/admin/report-groups/${postId}/decision`, admin.accessToken, { method: 'POST', body: JSON.stringify({ decision: 'hide', reason: 'Quyết định minh họa trên dữ liệu demo cục bộ.', groupVersion: detail.groupVersion }) })
  // Refresh the selected detail using its visible lookup form; no destructive browser action is used.
  await cdp.evaluate(`(() => { const input=document.querySelector('#moderation-id'); const native=Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set; native.call(input,${JSON.stringify(postId)}); input.dispatchEvent(new Event('input',{bubbles:true})); return true })()`)
  await cdp.evaluate("document.querySelector('#moderation-id').form.requestSubmit(); true")
  await cdp.until("document.querySelector('.moderation-detail .badge')?.textContent === 'Đã ẩn'", 'hidden moderation result')
  await cdp.capture(join(root, 'docs/demo/moderation-hidden.png'))
  const hidden = await request(`/api/admin/report-groups/${postId}`, admin.accessToken)
  await request(`/api/admin/posts/${postId}/restore`, admin.accessToken, { method: 'POST', headers: { 'If-Match': hidden.currentPost.version }, body: JSON.stringify({ reason: 'Khôi phục minh họa sau khi kiểm tra lại.' }) })
  await cdp.evaluate("document.querySelector('#moderation-id').form.requestSubmit(); true")
  await cdp.until("document.querySelector('.moderation-detail .badge')?.textContent === 'Đang hiển thị'", 'restored moderation result')
  await cdp.capture(join(root, 'docs/demo/moderation-restored.png'))
  report.checks.moderationHideRestore = true
  await run('ffmpeg', ['-nostdin', '-v', 'error', '-y', '-loop', '1', '-t', '2', '-i', join(root, 'docs/demo/moderation-before.png'), '-loop', '1', '-t', '2', '-i', join(root, 'docs/demo/moderation-hidden.png'), '-loop', '1', '-t', '2', '-i', join(root, 'docs/demo/moderation-restored.png'), '-filter_complex', '[0:v][1:v][2:v]concat=n=3:v=1:a=0,fps=1,scale=1020:-1', '-threads', '1', join(root, 'docs/demo/moderation.gif')])
  report.images.push('docs/demo/moderation.gif')
  await writeFile(join(root, 'docs/validation/browser.json'), JSON.stringify(report, null, 2) + '\n')
  console.log(JSON.stringify({ checks: report.checks, images: report.images, isolatedDatabase: database }, null, 2))
} finally {
  if (cdp) { try { await cdp.send('Browser.close') } catch { /* already exited */ }; cdp.socket.close() }
  browserProcess?.kill()
  app.kill()
  // Keep the isolated QA database/profile for inspection. Never drop/reset ConnectingBookSystemDb.
}
