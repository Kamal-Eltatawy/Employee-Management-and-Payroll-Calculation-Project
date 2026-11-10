import { Injectable, LOCALE_ID, inject } from '@angular/core';
import { Router } from '@angular/router';

export type Language = 'en' | 'ar';

/**
 * Each language is its own build (Angular i18n, D56): /en/… and /ar/… in production/Docker,
 * and two dev servers locally (npm start on 4200 for English, npm run start:ar on 4201 for Arabic).
 */
@Injectable({ providedIn: 'root' })
export class LanguageService {
  private readonly router = inject(Router);

  readonly current: Language = inject(LOCALE_ID).startsWith('ar') ? 'ar' : 'en';
  readonly other: Language = this.current === 'ar' ? 'en' : 'ar';
  readonly otherLabel = this.other === 'ar' ? 'العربية' : 'English';

  /** The same page in the other language. */
  otherLanguageUrl(): string {
    const { origin, pathname, port } = window.location;
    const prefix = `/${this.current}/`;
    if (pathname.startsWith(prefix)) {
      return `${origin}/${this.other}${this.router.url}`;
    }

    // Development: one dev server per language.
    const otherPort = port === '4201' ? '4200' : '4201';
    return `${window.location.protocol}//${window.location.hostname}:${otherPort}${this.router.url}`;
  }
}
