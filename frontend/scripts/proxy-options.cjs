const { Agent } = require('node:https');
const { isIP } = require('node:net');

function defineProxy(origin, caPath, readFile) {
  const target = new URL(origin ?? '');
  if (target.protocol !== 'https:' || target.href !== target.origin + '/' ||
      target.username || target.password || isIP(target.hostname) || (target.hostname !== 'localhost' && !target.hostname.includes('.'))) {
    throw new Error('SUR_API_ORIGIN must be an HTTPS origin with a registered tenant DNS hostname');
  }
  const agent = caPath ? new Agent({ ca: readFile(caPath), rejectUnauthorized: true }) : undefined;
  return {
    '^/(?:api/v1(?:/|[?]|$)|health/release(?:[?]|$))': {
      target: target.origin,
      changeOrigin: true,
      secure: true,
      agent,
      configure(proxy) {
        proxy.on('proxyReq', request => {
          for (const header of Object.keys(request.getHeaders())) {
            if (header === 'forwarded' || header.startsWith('x-forwarded-')) {
              request.removeHeader(header);
            }
          }
        });
      },
      bypass(request) {
        // Administrative browser endpoints use /provider; internal endpoints are never proxied.
        const pathname = request.url?.split('?')[0] ?? '';
        const allowed = pathname === '/health/release' || /^\/api\/v1\/(public|provider)(\/|$)/.test(pathname);
        if (!allowed ||
            /(?:%|\\|\/\.{1,2}(?:\/|$))/.test(pathname)) {
          return false;
        }
      },
    },
  };
}

module.exports = { defineProxy };
