import { execFileSync } from 'node:child_process';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('../', import.meta.url));
const apiVersion = process.env['SUR_RELEASE_API_VERSION'];
const apiRevision = process.env['SUR_RELEASE_API_REVISION'];
const schemaPath = process.env['SUR_RELEASE_OPENAPI'];
if (!apiVersion || !apiRevision || !schemaPath) {
  throw new Error('Set SUR_RELEASE_API_VERSION, SUR_RELEASE_API_REVISION and SUR_RELEASE_OPENAPI to the reviewed API version and saved OpenAPI JSON');
}
const schema = readFileSync(resolve(schemaPath));
const parsed = JSON.parse(schema);
if (!parsed.openapi || !parsed.paths || !parsed.info) { throw new Error('Invalid OpenAPI document'); }
const manifest = {
  spaVersion: JSON.parse(readFileSync(resolve(root, 'package.json'), 'utf8')).version,
  revision: execFileSync('git', ['rev-parse', 'HEAD'], { cwd: root, encoding: 'utf8' }).trim(),
  dirty: !!execFileSync('git', ['status', '--porcelain'], { cwd: root, encoding: 'utf8' }).trim(),
  apiVersion,
  apiRevision,
  contractVersion: 1,
  openApiSha256: createHash('sha256').update(schema).digest('hex'),
};
mkdirSync(resolve(root, 'public'), { recursive: true });
writeFileSync(resolve(root, 'public/release.json'), JSON.stringify(manifest, null, 2) + '\n');
