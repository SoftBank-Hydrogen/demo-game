// Browser check for SkyBoard (web/). Start the API and the web server first, then:
//   node Tools/board-browser-check.cjs [webUrl]      (default http://localhost:5173)
// Two people (desktop + phone) enter, share a post with an image, plan tasks and search.
// Uses real clicks and typing. Needs Google Chrome and `npm ci --prefix Tools` (Playwright).
const { chromium } = require('playwright');
const assert = require('node:assert/strict');
const zlib = require('node:zlib');
const path = require('node:path');

const web = (process.argv[2] || 'http://localhost:5173').replace(/\/$/, '');
const shots = process.env.SHOTS_DIR || path.resolve(__dirname, '../Artifacts');
const stamp = Date.now().toString(36);
const alice = { name: `Alice ${stamp}`, password: '1' };
const bob = { name: `Bob ${stamp}`, password: '2' };
const title = `Demo day plan ${stamp}`, poster = `QR poster ${stamp}`;

(async () => {
  const browser = await chromium.launch({ channel: 'chrome', headless: true });
  const checks = [];
  const problems = [];
  try {
    const desktop = await page(browser, { viewport: { width: 1280, height: 860 } }, problems);
    const phone = await page(browser, { viewport: { width: 390, height: 844 }, hasTouch: true, isMobile: true }, problems);

    await enter(desktop, alice);
    await enter(phone, bob);
    checks.push('Two people enter with a new name (account created on the spot) and land on the board');

    // Alice writes a post with two images.
    await desktop.getByRole('link', { name: 'New post' }).click();
    await desktop.getByLabel('Title').fill(title);
    await desktop.getByLabel('Text').fill(`Booth opens at 10.\nBring the ${poster}.`);
    await desktop.locator('input[type=file]').setInputFiles([png('poster.png', [19, 138, 108]), png('booth.png', [255, 181, 71])]);
    await desktop.locator('.gallery.small img').nth(1).waitFor();
    await desktop.locator('input[type=file]').setInputFiles({ name: 'notes.txt', mimeType: 'image/png', buffer: Buffer.from('plain text') });
    await desktop.getByText('notes.txt: Only PNG, JPEG, GIF and WebP images are accepted').waitFor();
    await desktop.getByRole('button', { name: 'Publish' }).click();
    await desktop.getByRole('heading', { name: title }).waitFor();
    assert.equal(await loadedImages(desktop, '.gallery img'), 2);
    checks.push('Post with 2 images published; a text file posing as an image is refused with a message');

    // Bob sees it on the phone, with a thumbnail, and cannot edit it.
    await phone.reload();
    await phone.getByRole('link', { name: title }).click();
    await phone.getByRole('heading', { name: title }).waitFor();
    assert.equal(await loadedImages(phone, '.gallery img'), 2);
    assert.equal(await phone.getByRole('link', { name: 'Edit' }).count(), 0);
    checks.push('The other person sees the post and its images; no Edit/Delete for a post that is not theirs');
    await phone.screenshot({ path: path.join(shots, 'board-phone-post.png'), fullPage: true });

    // Bob plans the work.
    await phone.getByRole('link', { name: 'Projects' }).click();
    await phone.getByPlaceholder('New project name').fill(`Demo day ${stamp}`);
    await phone.getByRole('button', { name: 'Create' }).click();
    await phone.getByRole('heading', { name: `Demo day ${stamp}`, level: 1 }).waitFor();
    for (const [task, who] of [[`Print ${poster}`, alice.name], ['Rehearse pitch', '']]) {
      await phone.getByLabel('New task title').fill(task);
      if (who) await phone.getByLabel('Assignee', { exact: true }).selectOption({ label: who });
      await phone.getByRole('button', { name: 'Add task' }).click();
      await phone.locator('.task h3', { hasText: task }).waitFor();
    }
    await phone.locator('.task', { hasText: `Print ${poster}` }).getByRole('button', { name: 'In progress →' }).click();
    await phone.locator('.column.doing .task', { hasText: `Print ${poster}` }).waitFor();
    assert.equal(await phone.locator('.column.todo .task').count(), 1);
    checks.push('Project created; 2 tasks added (one assigned); a task moves To do → In progress');
    await phone.screenshot({ path: path.join(shots, 'board-phone-project.png'), fullPage: true });

    // Alice finds her task through search and finishes it.
    await desktop.getByLabel('Search').fill(poster);
    await desktop.getByLabel('Search').press('Enter');
    const section = name => desktop.locator('.results', { has: desktop.getByRole('heading', { name, exact: true }) });
    await section('Tasks').waitFor();
    assert.equal(await section('Posts').locator('.result').count(), 1);
    await section('Tasks').getByText(`Print ${poster}`).click();
    await desktop.locator('.task', { hasText: `Print ${poster}` }).getByRole('button', { name: 'Done →' }).click();
    await desktop.locator('.column.done .task', { hasText: `Print ${poster}` }).waitFor();
    checks.push('Search finds the post and the task; the assignee marks the task Done from the project page');
    await desktop.screenshot({ path: path.join(shots, 'board-desktop-project.png'), fullPage: true });

    // Alice removes one image from her post.
    await desktop.getByRole('link', { name: 'Board' }).click();
    await desktop.getByRole('link', { name: title }).click();
    await desktop.getByRole('link', { name: 'Edit' }).click();
    await desktop.getByRole('button', { name: 'Remove image' }).first().click();
    await desktop.getByRole('button', { name: 'Save changes' }).click();
    await desktop.getByRole('heading', { name: title }).waitFor();
    assert.equal(await loadedImages(desktop, '.gallery img'), 1);
    checks.push('Editing the post removes one image');

    // Log out and back in.
    await desktop.getByRole('button', { name: 'Log out' }).click();
    await desktop.getByRole('button', { name: 'Enter' }).waitFor();
    await desktop.getByLabel('Name').fill(alice.name);
    await desktop.getByLabel('Password').fill('wrong');
    await desktop.getByRole('button', { name: 'Enter' }).click();
    await desktop.getByText('Wrong password for this name').waitFor();
    await desktop.getByLabel('Password').fill(alice.password);
    await desktop.getByRole('button', { name: 'Enter' }).click();
    await desktop.getByRole('heading', { name: 'Board', exact: true }).waitFor();
    await desktop.getByRole('link', { name: title }).waitFor();   // same account: her post is still hers
    checks.push('Log out; the same name with a wrong password is refused, the right one gets back in');

    assert.deepEqual(problems, [], 'no page errors');
    console.log(JSON.stringify({ success: true, web, checks }, null, 2));
  } catch (error) {
    console.log(JSON.stringify({ success: false, web, error: String(error), checks, problems }, null, 2));
    process.exitCode = 1;
  } finally {
    await browser.close();
  }
})();

async function page(browser, options, problems) {
  const p = await (await browser.newContext(options)).newPage();
  p.on('pageerror', e => problems.push(String(e)));
  // 401 on purpose (wrong password) and the refused text file (415) are expected.
  p.on('console', m => {
    if (m.type() === 'error' && !/401|415/.test(m.text())) problems.push(`${m.text()} ${m.location().url || ''}`.trim());
  });
  await p.goto(web);
  return p;
}

async function enter(p, user) {
  await p.getByLabel('Name').fill(user.name);
  await p.getByLabel('Password').fill(user.password);
  await p.getByRole('button', { name: 'Enter' }).click();
  await p.getByRole('heading', { name: 'Board', exact: true }).waitFor();
}

// Number of images that actually loaded (not broken).
async function loadedImages(p, selector) {
  await p.waitForFunction(s => [...document.querySelectorAll(s)].every(i => i.complete), selector);
  return p.$$eval(selector, images => images.filter(i => i.naturalWidth > 0).length);
}

// A solid-colour 160x120 PNG made in memory.
function png(name, [r, g, b]) {
  const width = 160, height = 120;
  const row = Buffer.concat([Buffer.from([0]), Buffer.from(Array.from({ length: width }, () => [r, g, b]).flat())]);
  const chunk = (type, data) => {
    const body = Buffer.concat([Buffer.from(type), data]);
    const out = Buffer.alloc(body.length + 8);
    out.writeUInt32BE(data.length, 0); body.copy(out, 4); out.writeUInt32BE(zlib.crc32(body), body.length + 4);
    return out;
  };
  const header = Buffer.alloc(13);
  header.writeUInt32BE(width, 0); header.writeUInt32BE(height, 4); header.set([8, 2, 0, 0, 0], 8);
  const buffer = Buffer.concat([Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]), chunk('IHDR', header),
    chunk('IDAT', zlib.deflateSync(Buffer.concat(Array(height).fill(row)))), chunk('IEND', Buffer.alloc(0))]);
  return { name, mimeType: 'image/png', buffer };
}
