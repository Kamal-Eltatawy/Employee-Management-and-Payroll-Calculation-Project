import { convertToParamMap } from '@angular/router';
import { queryFromParams } from './employee-list.page';

describe('queryFromParams', () => {
  const read = (params: Record<string, string>) => queryFromParams(convertToParamMap(params));

  it('uses defaults for an empty URL', () => {
    expect(read({})).toEqual({
      search: undefined,
      departmentId: undefined,
      status: undefined,
      sortBy: 'name',
      sortDir: 'asc',
      page: 1,
      pageSize: 10,
    });
  });

  it('reads valid values', () => {
    expect(
      read({ search: 'al', departmentId: '2', status: 'Inactive', sortBy: 'salary', sortDir: 'desc', page: '3', pageSize: '25' }),
    ).toEqual({ search: 'al', departmentId: 2, status: 'Inactive', sortBy: 'salary', sortDir: 'desc', page: 3, pageSize: 25 });
  });

  it('falls back to page 1 for an invalid page', () => {
    expect(read({ page: 'abc' }).page).toBe(1);
    expect(read({ page: '0' }).page).toBe(1);
    expect(read({ page: '-2' }).page).toBe(1);
    expect(read({ page: '1.5' }).page).toBe(1);
  });

  it('caps the page size at 100', () => {
    expect(read({ pageSize: '500' }).pageSize).toBe(100);
    expect(read({ pageSize: 'x' }).pageSize).toBe(10);
  });

  it('ignores an unknown sort column, direction, status or department', () => {
    const query = read({ sortBy: 'password', sortDir: 'sideways', status: 'Deleted', departmentId: 'abc' });

    expect(query.sortBy).toBe('name');
    expect(query.sortDir).toBe('asc');
    expect(query.status).toBeUndefined();
    expect(query.departmentId).toBeUndefined();
  });
});
