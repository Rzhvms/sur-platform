import assert from 'node:assert/strict';
import { EventEmitter } from 'node:events';
import { test } from 'node:test';
import { defineProxy } from './proxy-options.cjs';

const origin = 'https://alpha.sur.test:7443';
const first = options => Object.values(options)[0];

test('selects the configured HTTPS tenant and strips client forwarding headers', () => {
  const proxy = first(defineProxy(origin, undefined));
  assert.equal(proxy.target, origin);
  assert.equal(proxy.changeOrigin, true);
  assert.equal(proxy.secure, true);
  const events = new EventEmitter();
  const headers = {
    host: 'alpha.sur.test:7443', forwarded: 'host=beta.sur.test',
    'x-forwarded-host': 'beta.sur.test', cookie: 'session=fixture',
  };
  proxy.configure(events);
  events.emit('proxyReq', { getHeaders: () => headers, removeHeader: name => delete headers[name] });
  assert.deepEqual(headers, { host: 'alpha.sur.test:7443', cookie: 'session=fixture' });
});

test('separates API paths and rejects internal routes including path-normalization aliases', () => {
  const config = defineProxy(origin, undefined);
  const matcher = new RegExp(Object.keys(config)[0]);
  assert.ok(matcher.test('/api/v1'));
  assert.ok(!matcher.test('/api/v10'));
  const proxy = first(config);
  for (const url of ['/api/v1/public/risks?search=%25', '/api/v1/provider/business-history']) {
    assert.equal(proxy.bypass({ url }), undefined);
  }
  for (const url of ['/api/v1', '/api/v1/internal/sessions', '/api/v1/public/../internal/sessions',
    '/api/v1/public/%2e%2e/internal/sessions', '/api/v1/public\\..\\internal/sessions']) {
    assert.equal(proxy.bypass({ url }), false);
  }
});

test('requires a canonical DNS HTTPS origin and validates TLS with the configured CA', () => {
  for (const value of [undefined, 'http://alpha.sur.test', 'https://127.0.0.1', 'https://singlelabel',
    'https://user@alpha.sur.test', 'https://alpha.sur.test/other', 'https://alpha.sur.test/?tenant=beta']) {
    assert.throws(() => defineProxy(value, undefined));
  }
  const cert = Buffer.from('fixture certificate');
  const proxy = first(defineProxy(origin, 'ca.pem', path => {
    assert.equal(path, 'ca.pem');
    return cert;
  }));
  assert.equal(proxy.agent.options.ca, cert);
  assert.equal(proxy.agent.options.rejectUnauthorized, true);
});


test('does not proxy service health endpoints', () => {
  const config = defineProxy('https://localhost:7224', undefined);
  const matcher = new RegExp(Object.keys(config)[0]);
  const proxy = first(config);
  for (const url of ['/health/release', '/health/release?probe=1', '/health/live', '/health/ready']) {
    assert.equal(matcher.test(url), false);
    assert.equal(proxy.bypass({ url }), false);
  }
});
