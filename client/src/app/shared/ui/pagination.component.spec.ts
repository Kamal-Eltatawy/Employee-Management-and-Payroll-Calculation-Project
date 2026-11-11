import { TestBed } from '@angular/core/testing';
import { PaginationComponent } from './pagination.component';

describe('PaginationComponent', () => {
  async function render(page: number, totalCount: number, pageSize = 10) {
    const fixture = TestBed.createComponent(PaginationComponent);
    fixture.componentRef.setInput('page', page);
    fixture.componentRef.setInput('pageSize', pageSize);
    fixture.componentRef.setInput('totalCount', totalCount);
    const emitted: number[] = [];
    fixture.componentInstance.pageChange.subscribe((value) => emitted.push(value));
    await fixture.whenStable();

    const element = fixture.nativeElement as HTMLElement;
    const [previous, next] = Array.from(element.querySelectorAll('button'));
    return { element, previous, next, emitted };
  }

  it('shows the range and the page count', async () => {
    const { element } = await render(2, 25);

    expect(element.textContent).toContain('11–20 of 25');
    expect(element.textContent).toContain('Page 2 of 3');
  });

  it('moves to the next and previous pages', async () => {
    const { previous, next, emitted } = await render(2, 25);

    next.click();
    previous.click();

    expect(emitted).toEqual([3, 1]);
  });

  it('disables the buttons at the edges', async () => {
    const { previous, next } = await render(1, 5);

    expect(previous.disabled).toBe(true);
    expect(next.disabled).toBe(true);
  });

  it('says when there are no results', async () => {
    const { element } = await render(1, 0);

    expect(element.textContent).toContain('No results');
    expect(element.textContent).toContain('Page 1 of 1');
  });
});
