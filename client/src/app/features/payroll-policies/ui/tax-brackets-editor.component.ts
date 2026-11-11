import { ChangeDetectorRef, Component, LOCALE_ID, inject, input } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { FormArray, NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { switchMap } from 'rxjs';
import { FieldErrorsComponent } from '../../../shared/ui/field-errors.component';
import { IconComponent } from '../../../shared/ui/icon.component';
import { TaxMethod } from '../data/payroll-policy.models';
import { BracketBoundProblem, TaxBracketForm, createBracketRow, describeBands } from './tax-brackets';

/** Edits a policy's tax brackets (one FormArray). Exempt schemes have none, a flat scheme exactly one open row. */
@Component({
  selector: 'app-tax-brackets-editor',
  imports: [ReactiveFormsModule, FieldErrorsComponent, IconComponent],
  template: `
    <div class="editor">
      <div class="editor-head">
        <span class="form-label mb-0" i18n="@@brackets.title">Tax brackets (monthly taxable income)</span>
        @if (method() === 'Progressive' && !readOnly()) {
          <button type="button" class="btn btn-sm btn-outline-primary d-inline-flex align-items-center gap-1" (click)="add()">
            <app-icon name="plus" [size]="16" />
            <span i18n="@@brackets.add">Add bracket</span>
          </button>
        }
      </div>

      @switch (method()) {
        @case ('Exempt') {
          <p class="hint" i18n="@@brackets.exemptHint">This scheme is tax exempt: no income tax and no brackets. Social insurance still applies.</p>
        }
        @case ('Flat') {
          <p class="hint" i18n="@@brackets.flatHint">A flat tax applies one rate to all taxable income.</p>
        }
        @case ('Progressive') {
          <p class="hint" i18n="@@brackets.progressiveHint">Each rate applies to the part of income inside its band. Upper bounds go up row by row; leave the last one empty.</p>
        }
      }

      @if (method() !== 'Exempt') {
        @for (row of array().controls; track row; let i = $index; let last = $last) {
          <div class="item" [formGroup]="row">
            @if (method() !== 'Flat') {
              <div>
                <label class="form-label small" [for]="'upperBound' + i" i18n="@@brackets.upperBound">Up to (empty = no limit)</label>
                <input class="form-control" type="number" min="0.01" step="0.01" [id]="'upperBound' + i" formControlName="upperBound"
                       [placeholder]="last ? noLimit : ''"
                       [class.is-invalid]="boundProblem(i) || (row.controls.upperBound.invalid && row.controls.upperBound.touched)" />
                @switch (boundProblem(i)) {
                  @case ('lastOpen') {
                    <div class="invalid-feedback d-block" i18n="@@brackets.lastOpen">Leave the upper bound of the last bracket empty.</div>
                  }
                  @case ('onlyLastOpen') {
                    <div class="invalid-feedback d-block" i18n="@@brackets.onlyLastOpen">Only the last bracket can have no upper bound.</div>
                  }
                  @case ('ascending') {
                    <div class="invalid-feedback d-block" i18n="@@brackets.ascending">Must be greater than the previous upper bound.</div>
                  }
                }
                <app-field-errors [control]="row.controls.upperBound" label="Upper bound" i18n-label="@@brackets.upperBoundLabel" />
              </div>
            }
            <div>
              <label class="form-label small" [for]="'ratePercent' + i" i18n="@@brackets.rate">Rate %</label>
              <input class="form-control" type="number" min="0" max="100" step="0.01" [id]="'ratePercent' + i" formControlName="ratePercent"
                     [class.is-invalid]="row.controls.ratePercent.invalid && row.controls.ratePercent.touched" />
              <app-field-errors [control]="row.controls.ratePercent" label="Rate" i18n-label="@@brackets.rateLabel" />
            </div>
            @if (method() === 'Progressive' && !readOnly()) {
              <button type="button" class="btn btn-light icon-button text-danger remove" (click)="remove(i)"
                      title="Remove bracket" i18n-title="@@brackets.remove" aria-label="Remove bracket" i18n-aria-label="@@brackets.remove">
                <app-icon name="trash" />
              </button>
            }
          </div>
        } @empty {
          <p class="hint" i18n="@@brackets.empty">No brackets yet.</p>
        }

        @if (shapeError()) {
          <div class="invalid-feedback d-block" role="alert">
            @switch (shapeError()) {
              @case ('Progressive') {
                <ng-container i18n="@@brackets.shapeProgressive">A progressive tax needs at least one bracket.</ng-container>
              }
              @case ('Flat') {
                <ng-container i18n="@@brackets.shapeFlat">A flat tax needs exactly one bracket without an upper bound.</ng-container>
              }
            }
          </div>
        }
      }

      @if (serverError(); as message) {
        <div class="invalid-feedback d-block" role="alert">{{ message }}</div>
      }

      @if (method() !== 'Exempt' && bands().length > 0) {
        <div class="preview">
          <span class="preview-title" i18n="@@brackets.preview">Preview</span>
          <ul>
            @for (band of bands(); track $index) {
              <li><bdi>{{ band }}</bdi></li>
            }
          </ul>
        </div>
      }
    </div>
  `,
  styles: `
    .editor { border: 1px dashed var(--app-border); border-radius: .75rem; padding: 1rem; background: #fcfcfd; }
    .editor-head { display: flex; align-items: center; justify-content: space-between; gap: .5rem; margin-block-end: .5rem; }
    .hint { color: var(--app-muted); font-size: .9rem; margin-block: 0 .75rem; }
    .item { display: grid; grid-template-columns: repeat(auto-fit, minmax(10rem, 1fr)) auto; gap: .5rem; align-items: start; margin-block-end: .5rem; }
    .item .remove { margin-block-start: 1.75rem; }
    .preview { margin-block-start: .75rem; padding-block-start: .75rem; border-block-start: 1px solid var(--app-border); font-size: .9rem; }
    .preview-title { display: block; font-weight: 600; margin-block-end: .25rem; }
    .preview ul { margin: 0; padding-inline-start: 1.25rem; font-variant-numeric: tabular-nums; }
    @media (max-width: 575.98px) { .item { grid-template-columns: 1fr; } .item .remove { margin-block-start: 0; justify-self: end; } }
  `,
})
export class TaxBracketsEditorComponent {
  readonly array = input.required<FormArray<TaxBracketForm>>();
  readonly method = input<TaxMethod>();
  readonly readOnly = input(false);

  private readonly fb = inject(NonNullableFormBuilder);
  private readonly locale = inject(LOCALE_ID);
  protected readonly noLimit = $localize`:@@brackets.noLimit:No limit`;

  constructor() {
    // OnPush (the Angular 22 default) and form state is not a signal: the page's submit and server errors change
    // the array without an event in this component, so refresh on every form event (as the pay components editor).
    const changeDetector = inject(ChangeDetectorRef);
    toObservable(this.array)
      .pipe(
        switchMap((array) => array.events),
        takeUntilDestroyed(),
      )
      .subscribe(() => changeDetector.markForCheck());
  }

  protected add(): void {
    this.array().push(createBracketRow(this.fb));
  }

  protected remove(index: number): void {
    this.array().removeAt(index);
    this.array().markAsDirty();
  }

  /** Row problems show once the user worked on the brackets or tried to save. */
  protected boundProblem(index: number): BracketBoundProblem | null {
    const array = this.array();
    if (!(array.touched || array.dirty)) {
      return null;
    }

    const bounds = array.errors?.['bracketBounds'] as Record<number, BracketBoundProblem> | undefined;
    return bounds?.[index] ?? null;
  }

  protected shapeError(): TaxMethod | null {
    const array = this.array();
    return array.touched || array.dirty ? ((array.errors?.['bracketShape'] as TaxMethod | undefined) ?? null) : null;
  }

  /** A server message on the whole list (e.g. the bracket shape for the scheme's method). */
  protected serverError(): string | null {
    const message: unknown = this.array().errors?.['server'];
    return typeof message === 'string' ? message : null;
  }

  protected bands(): string[] {
    return describeBands(this.array().getRawValue(), this.locale);
  }
}
