import { HttpErrorResponse } from '@angular/common/http';
import { toApiError } from './api-error';

describe('toApiError', () => {
  it('reads a validation problem with field errors', () => {
    const error = new HttpErrorResponse({
      status: 400,
      error: { title: 'One or more validation errors occurred.', errors: { 'payComponents[0].amount': ['Too small.'] } },
    });

    expect(toApiError(error)).toEqual({
      status: 400,
      title: 'One or more validation errors occurred.',
      detail: undefined,
      fieldErrors: { 'payComponents[0].amount': ['Too small.'] },
    });
  });

  it('reads a conflict with a detail', () => {
    const error = new HttpErrorResponse({
      status: 409,
      error: { title: 'Conflict.', detail: 'The record was modified by another user.' },
    });

    expect(toApiError(error)).toMatchObject({ status: 409, detail: 'The record was modified by another user.', fieldErrors: {} });
  });

  it('describes a network failure', () => {
    expect(toApiError(new HttpErrorResponse({ status: 0 }))).toMatchObject({ status: 0, title: 'The server cannot be reached.' });
  });

  it('treats a gateway error without a problem body as an unreachable server', () => {
    expect(toApiError(new HttpErrorResponse({ status: 502, statusText: 'Bad Gateway', error: '' }))).toMatchObject({
      status: 502,
      title: 'The server cannot be reached.',
    });
  });

  it('falls back to the status text when the body is not a problem', () => {
    expect(toApiError(new HttpErrorResponse({ status: 500, statusText: 'Internal Server Error', error: '<html>' }))).toMatchObject({
      status: 500,
      title: 'Internal Server Error',
    });
  });

  it('passes an ApiError through and wraps anything else', () => {
    const apiError = { status: 422, title: 'Business rule violated.', fieldErrors: {} };

    expect(toApiError(apiError)).toBe(apiError);
    expect(toApiError(new Error('boom'))).toEqual({ status: 0, title: 'An unexpected error occurred.', fieldErrors: {} });
  });
});
