import { spawn } from 'node:child_process';
import { accessSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

const files = ['SUR_TLS_CERT', 'SUR_TLS_KEY'].map(name => {
  const path = process.env[name];
  if (!path) {
    throw new Error(`Set ${name} for local HTTPS`);
  }
  accessSync(path);
  return path;
});

const cli = fileURLToPath(new URL('../node_modules/@angular/cli/bin/ng.js', import.meta.url));
const child = spawn(process.execPath, [
  cli, 'serve', '--host', '127.0.0.1', '--ssl', '--ssl-cert', files[0], '--ssl-key', files[1],
  '--proxy-config', 'scripts/proxy.cjs',
], { stdio: 'inherit' });
child.once('exit', code => { process.exitCode = code ?? 1; });
for (const signal of ['SIGINT', 'SIGTERM']) {
  process.once(signal, () => { child.kill(signal); });
}
