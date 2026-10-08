// Joins N bot players to the local game server so you can see a crowded round in the browser.
// Usage: node Tools/tug-crowd.cjs 20      (stop with Ctrl+C)
// Bots tap at a relaxed, random pace during rounds. This is a demo helper, not a load test.
const path = require('node:path');
const WebSocket = require(path.join(__dirname, '../game/server/node_modules/ws'));

const count = Math.max(1, Math.min(200, Number(process.argv[2]) || 10));
const url = process.argv[3] || 'ws://127.0.0.1:8080/ws';
let open = 0;

for (let i = 0; i < count; i++) {
  const ws = new WebSocket(url);
  let playing = false;
  ws.on('open', () => { open++; ws.send(JSON.stringify({ type: 'join' })); });
  ws.on('message', data => { const m = JSON.parse(data); if (m.type === 'state') playing = m.phase === 'playing'; });
  ws.on('close', () => { open--; });
  ws.on('error', e => console.error('bot', i, e.message));
  setInterval(() => {
    if (playing && ws.readyState === WebSocket.OPEN && Math.random() < .4)
      ws.send(JSON.stringify({ type: 'tap', n: 1 + Math.floor(Math.random() * 3) }));
  }, 200);
}
setInterval(() => process.stdout.write(`\r${open}/${count} bots connected `), 1000);
process.on('SIGINT', () => { console.log('\nstopping'); process.exit(0); });
