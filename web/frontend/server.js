// Production server for the built site (dist/). `npm start` builds first (the "prestart" script),
// so a fresh checkout or container needs no separate build step.
//   PORT          port to listen on (default 3000)
//   HOST          address to listen on (default 0.0.0.0, i.e. reachable inside a container)
//   API_BASE_URL  backend address given to the browser through /config.json
//                 (if unset, dist/config.json from public/config.json is used)
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.join(path.dirname(fileURLToPath(import.meta.url)), 'dist');
const port = Number(process.env.PORT || 3000);
const host = process.env.HOST || '0.0.0.0';
const types = {
  '.html': 'text/html; charset=utf-8', '.js': 'text/javascript; charset=utf-8', '.css': 'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8', '.svg': 'image/svg+xml', '.png': 'image/png', '.ico': 'image/x-icon',
};

if (!fs.existsSync(path.join(root, 'index.html'))) {
  console.error('dist/index.html is missing. Run `npm run build` first.');
  process.exit(1);
}

http.createServer((req, res) => {
  let pathname;
  try { pathname = decodeURIComponent(new URL(req.url, 'http://localhost').pathname); }
  catch { return send(res, 400, 'text/plain', 'Bad request'); }
  if (pathname === '/health') return send(res, 200, types['.json'], '{"status":"ok"}');
  if (pathname === '/config.json' && process.env.API_BASE_URL)
    return send(res, 200, types['.json'], JSON.stringify({ apiBaseUrl: process.env.API_BASE_URL }), 'no-store');

  const file = path.resolve(root, '.' + pathname);
  if (file.startsWith(root + path.sep) && fs.existsSync(file) && fs.statSync(file).isFile()) {
    // Vite puts a content hash in asset names, so those can be cached for good.
    const cache = pathname.startsWith('/assets/') ? 'public, max-age=31536000, immutable' : 'no-store';
    return send(res, 200, types[path.extname(file)] || 'application/octet-stream', fs.readFileSync(file), cache);
  }
  // Any other path is a page of the app (e.g. /posts/3): the browser-side router shows it.
  send(res, 200, types['.html'], fs.readFileSync(path.join(root, 'index.html')), 'no-store');
}).listen(port, host, () => console.log(`TeamBoard web on http://${host}:${port}`));

function send(res, status, type, body, cache = 'no-store') {
  res.writeHead(status, { 'Content-Type': type, 'Cache-Control': cache, 'X-Content-Type-Options': 'nosniff' });
  res.end(body);
}
