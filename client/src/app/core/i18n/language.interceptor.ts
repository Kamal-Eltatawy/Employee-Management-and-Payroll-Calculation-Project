import { HttpInterceptorFn } from '@angular/common/http';
import { LOCALE_ID, inject } from '@angular/core';

/** Tells the API which language to answer in, so server messages match the UI (D56). */
export const languageInterceptor: HttpInterceptorFn = (request, next) => {
  if (!request.url.startsWith('/api/')) {
    return next(request);
  }

  const language = inject(LOCALE_ID).startsWith('ar') ? 'ar' : 'en';
  return next(request.clone({ setHeaders: { 'Accept-Language': language } }));
};
