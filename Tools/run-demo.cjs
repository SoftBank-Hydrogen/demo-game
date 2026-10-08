'use strict';
const { createTugServer } = require('../game/server/server');
const { available } = require('../game/server/db');
const { createClientServer } = require('./serve-client.cjs');

async function startDemo({ gamePort = 8080, webPort = 3000, scores } = {}) {
  if (scores === undefined && !available()) throw new Error('SQLite requires Node.js 22.13 or newer.');
  const game = createTugServer(scores === undefined ? {} : { scores });
  const web = createClientServer();
  const listen = (server, port) => new Promise((resolve, reject) => {
    server.once('error', reject);
    server.listen(port, '127.0.0.1', resolve);
  });
  let stopped;
  const stop = () => stopped ||= (async () => {
    await new Promise(resolve => web.close(resolve));
    await game.closeAll();
  })();
  try { await listen(game, gamePort); await listen(web, webPort); }
  catch (e) { await stop(); throw e; }
  return { game, web, stop };
}

if (require.main === module) {
  startDemo().then(demo => {
    console.log('Open http://localhost:3000 in two browser tabs.');
    console.log('Game server: http://localhost:8080 | Stop: Ctrl+C');
    for (const signal of ['SIGINT', 'SIGTERM']) process.once(signal, () => demo.stop().then(() => process.exit(0)));
  }).catch(e => {
    console.error(e.code === 'EADDRINUSE' ? 'Port 8080 or 3000 is already in use. Close the other local server and retry.' : e.message);
    process.exitCode = 1;
  });
}
module.exports = { startDemo };
