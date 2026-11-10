/// <reference types="@angular/localize" />

import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { App } from './app/app';

// Each locale is a separate build (D56). Arabic is right-to-left; the CSS uses logical properties to follow `dir`.
const locale = $localize.locale ?? 'en';
document.documentElement.lang = locale;
document.documentElement.dir = locale.startsWith('ar') ? 'rtl' : 'ltr';

bootstrapApplication(App, appConfig).catch((err) => console.error(err));
