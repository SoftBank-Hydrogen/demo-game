// Browser check for the Tug Unity client against a running game server (ws://localhost:8080/ws)
// and the built client served at http://localhost:3000. Uses real mouse/keyboard input.
const { chromium } = require('playwright');
const assert = require('node:assert/strict');
const path = require('node:path');

const url = process.argv[2] || 'http://localhost:3000/';
const state = p => p.evaluate(() => window.__tugState);
const until = (p, fn, arg, timeout = 60000) => p.waitForFunction(fn, arg, { timeout });

(async () => {
  require('node:fs').mkdirSync(path.resolve(__dirname, '../Artifacts'), { recursive: true });
  const browser = await chromium.launch({ channel: 'chrome', headless: true, args: ['--enable-webgl', '--use-angle=swiftshader', '--enable-unsafe-swiftshader'] });
  const checks = [];
  let desktop, phone;
  try {
    desktop = await (await browser.newContext({ viewport: { width: 1440, height: 900 } })).newPage();
    phone = await (await browser.newContext({ viewport: { width: 390, height: 844 }, hasTouch: true, isMobile: true })).newPage();
    const sent = { desktop: [], phone: [] };
    for (const [name, page] of [['desktop', desktop], ['phone', phone]])
      page.on('websocket', ws => ws.on('framesent', f => { if (String(f.payload).includes('"tap"')) sent[name].push(String(f.payload)); }));
    global.__sent = sent;
    await Promise.all([desktop.goto(url), phone.goto(url)]);
    await until(desktop, () => window.__tugState?.connected, null, 120000);
    await until(phone, () => window.__tugState?.connected, null, 120000);
    const [d, p] = [await state(desktop), await state(phone)];
    assert.notEqual(d.team, p.team);
    checks.push(`Two clients connect to ${d.server} and get opposite teams (${d.team}/${p.team})`);

    // One Mochi per player on each side, and each screen marks its own character.
    for (const page of [desktop, phone])
      await until(page, () => { const s = window.__tugState; return s && s.shownA === 1 && s.shownB === 1 && s.mineMarked; }, null, 10000);
    checks.push('Both screens show one character per team and mark their own character with YOU');

    // Accessories come from the player id, so both screens must agree on who wears what.
    const [dl, pl] = [(await state(desktop)).looks, (await state(phone)).looks];
    assert.ok(dl && !dl.includes(':-1'), `every character has an accessory (${dl})`);
    assert.equal(dl, pl, 'both screens show the same accessory per player');
    checks.push(`Both screens show the same accessory per player (${dl})`);

    // Start from a fresh round so earlier taps are not mixed in.
    await until(desktop, () => window.__tugState?.phase === 'countdown', null, 45000);
    await until(desktop, () => window.__tugState?.phase === 'playing', null, 15000);
    // Human-speed input (about 6 taps/s). Unity reads input once per frame, so several
    // presses inside one frame count as one; headless rendering is slow, hence the gaps.
    for (let i = 0; i < 4; i++) { await desktop.mouse.click(1160, 570); await desktop.waitForTimeout(150); }   // TAP (landscape)
    await desktop.keyboard.press('Space'); await desktop.waitForTimeout(150);
    for (let i = 0; i < 3; i++) { await phone.touchscreen.tap(195, 596); await phone.waitForTimeout(150); }   // TAP (portrait 900x1600 → 390x844)
    await desktop.waitForTimeout(600);
    const sum = list => list.reduce((n, f) => n + JSON.parse(f).n, 0);
    assert.equal(sum(sent.desktop), 5, 'desktop taps sent');
    assert.equal(sum(sent.phone), 3, 'phone taps sent');
    await until(phone, ([team, other]) => {
      const s = window.__tugState; return s && s['taps' + team] === 5 && s['taps' + other] === 3;
    }, [d.team, p.team], 10000);
    await until(desktop, ([team, other]) => {
      const s = window.__tugState; return s && s['taps' + team] === 5 && s['taps' + other] === 3;
    }, [d.team, p.team], 10000);
    checks.push('Desktop mouse clicks + Space (5) and phone touch taps (3) are sent once each and appear on both screens');
    await desktop.screenshot({ path: path.resolve(__dirname, '../Artifacts/tug-desktop.png') });
    await phone.screenshot({ path: path.resolve(__dirname, '../Artifacts/tug-phone.png') });

    await until(desktop, () => window.__tugState?.phase === 'result', null, 40000);
    checks.push('Round ends and both clients enter the result phase');

    // Scoreboard (SQLite on the server). -1 means the server has no scoreboard (Node < 22).
    const before = await state(desktop);
    if (before.savedRounds === -1) checks.push('Scoreboard unavailable on this server (skipped)');
    else {
      await until(phone, n => window.__tugState?.savedRounds >= n, before.savedRounds, 10000);
      const p2 = await state(phone);
      checks.push(`Round saved: both clients see ${p2.savedRounds} saved round(s), wins A ${p2.winsA} : B ${p2.winsB}`);
      await desktop.screenshot({ path: path.resolve(__dirname, '../Artifacts/tug-result.png') });
    }
    console.log(JSON.stringify({ success: true, checks, desktop: await state(desktop), phone: await state(phone) }, null, 2));
  } catch (e) {
    const snap = async page => page ? page.evaluate(() => window.__tugState).catch(() => null) : null;
    if (desktop) await desktop.screenshot({ path: path.resolve(__dirname, '../Artifacts/tug-failure-desktop.png') }).catch(() => {});
    if (phone) await phone.screenshot({ path: path.resolve(__dirname, '../Artifacts/tug-failure-phone.png') }).catch(() => {});
    console.log(JSON.stringify({ success: false, error: String(e), checks, sent: global.__sent, desktop: await snap(desktop), phone: await snap(phone) }, null, 2));
    process.exitCode = 1;
  } finally { await browser.close(); }
})();
