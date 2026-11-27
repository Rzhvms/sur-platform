export interface ApiRelease { version: string; revision: string; contractVersion: number }
export const expectedContract = 1;

/** Проверяем контракт и идентификаторы релиза, не доверяя HTML fallback или неполному ответу. */
export function compatibleRelease(value: unknown): value is ApiRelease {
  if (!value || typeof value !== 'object') { return false; }
  const data = value as Record<string, unknown>;
  return data['contractVersion'] === expectedContract && typeof data['version'] === 'string' && !!data['version']
    && typeof data['revision'] === 'string' && !!data['revision'];
}

export const releaseText = {
  heading: 'Не удалось открыть приложение',
  incompatible: 'Версии приложения и API несовместимы. Обновите страницу или обратитесь к администратору.',
  unavailable: 'Не удалось проверить версию API. Проверьте подключение к серверу и повторите попытку.',
  retry: 'Проверить подключение', refresh: 'Обновить приложение',
};

export interface SpaRelease {
  spaVersion: string;
  apiVersion: string;
  apiRevision: string;
  contractVersion: number;
  openApiSha256: string;
}

/** Сравнивает установленный API с парой, зафиксированной при проверке SPA. */
export function sameRelease(api: unknown, spa: SpaRelease): boolean {
  return compatibleRelease(api) && spa.contractVersion === api.contractVersion
    && spa.apiVersion === api.version && spa.apiRevision === api.revision
    && /^[a-f0-9]{64}$/.test(spa.openApiSha256);
}
