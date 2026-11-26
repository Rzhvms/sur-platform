import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { bootstrapApplication } from '@angular/platform-browser';
import { provideRouter, withInMemoryScrolling } from '@angular/router';
import { App, routes } from './app/app';
import { text } from './app/shared/text';
import { accessToken } from './app/core/auth/access';

void bootstrapApplication(App, {
  providers: [
    provideRouter(routes, withInMemoryScrolling({ scrollPositionRestoration: 'enabled', anchorScrolling: 'enabled' })),
    provideHttpClient(withInterceptors([accessToken])),
  ],
}).catch(() => console.error(text.startupFailure));
