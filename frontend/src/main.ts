import { bootstrapApplication } from '@angular/platform-browser';
import { AppComponent } from './app/app.component';
import { appConfig } from './app/app.config';
import { text } from './app/shared/text';

void bootstrapApplication(AppComponent, appConfig).catch(() => console.error(text.startupFailure));
