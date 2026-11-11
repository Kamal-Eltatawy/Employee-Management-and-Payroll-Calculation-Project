import { HttpErrorResponse } from '@angular/common/http';

/** The client-side shape of an RFC 7807 problem (D42). */
export interface ApiError {
  status: number;
  title: string;
  detail?: string;
  fieldErrors: Record<string, string[]>;
}

export function isApiError(value: unknown): value is ApiError {
  return (
    typeof value === 'object' &&
    value !== null &&
    typeof (value as ApiError).status === 'number' &&
    typeof (value as ApiError).title === 'string' &&
    typeof (value as ApiError).fieldErrors === 'object'
  );
}

/** Converts anything thrown by HttpClient into an ApiError. Never throws. */
export function toApiError(error: unknown): ApiError {
  if (isApiError(error)) {
    return error;
  }

  if (error instanceof HttpErrorResponse) {
    const body: unknown = error.error;
    if (typeof body === 'object' && body !== null && typeof (body as { title?: unknown }).title === 'string') {
      const problem = body as { title: string; detail?: unknown; errors?: unknown };
      return {
        status: error.status,
        title: problem.title,
        detail: typeof problem.detail === 'string' ? problem.detail : undefined,
        fieldErrors: isFieldErrors(problem.errors) ? problem.errors : {},
      };
    }

    // 0 = no response; 502–504 without a problem body = the dev proxy or a gateway could not reach the API.
    const unreachable = error.status === 0 || (error.status >= 502 && error.status <= 504);
    return {
      status: error.status,
      title: unreachable
        ? $localize`:@@error.unreachable:The server cannot be reached.`
        : error.statusText || $localize`:@@error.requestFailed:The request failed.`,
      fieldErrors: {},
    };
  }

  return { status: 0, title: $localize`:@@error.unexpected:An unexpected error occurred.`, fieldErrors: {} };
}

/** Network failures and server errors: problemDetailsInterceptor already shows them as a toast. */
export function isReportedGlobally(error: ApiError): boolean {
  return error.status === 0 || error.status >= 500;
}

/** The message a page shows inline, or null when the toast already told the user (no double message). */
export function inlineMessage(error: ApiError | null | undefined): string | null {
  return error && !isReportedGlobally(error) ? (error.detail ?? error.title) : null;
}

/** A resource wraps a non-Error failure (our ApiError) in an Error whose cause is the original. */
export function resourceApiError(error: Error | undefined): ApiError | null {
  return error ? toApiError(error.cause ?? error) : null;
}

function isFieldErrors(value: unknown): value is Record<string, string[]> {
  return (
    typeof value === 'object' &&
    value !== null &&
    Object.values(value).every((messages) => Array.isArray(messages) && messages.every((m) => typeof m === 'string'))
  );
}
