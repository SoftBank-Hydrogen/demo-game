'use strict';
const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '../game/client');
const types = { '.html': 'text/html; charset=utf-8', '.json': 'application/json', '.js': 'application/javascript', '.wasm': 'application/wasm', '.png': 'image/png' };
function createClientServer() { return http.createServer((req, res) => {
  try {
    const relative = decodeURIComponent(new URL(req.url, 'http://localhost').pathname).replace(/^\/+/, '') || 'index.html';
    const file = path.resolve(root, relative);
    if (!file.startsWith(root + path.sep) || !fs.existsSync(file) || !fs.statSync(file).isFile()) { res.writeHead(404); return res.end(); }
    res.writeHead(200, { 'Content-Type': types[path.extname(file)] || 'application/octet-stream', 'Cache-Control': 'no-store' });
    const stream = fs.createReadStream(file);
    stream.on('error', () => res.destroy()); stream.pipe(res);
  } catch { res.writeHead(400); res.end(); }
}); }
if (require.main === module) createClientServer().listen(Number(process.env.CLIENT_PORT || 3000), '127.0.0.1', () => console.log('TUG client: http://localhost:' + (process.env.CLIENT_PORT || 3000)));
module.exports = { createClientServer };
