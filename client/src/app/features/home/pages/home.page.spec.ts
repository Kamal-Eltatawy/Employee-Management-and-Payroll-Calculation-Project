import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Role } from '../../../core/auth/auth.models';
import { AuthService } from '../../../core/auth/auth.service';
import { problemDetailsInterceptor } from '../../../core/http/problem-details.interceptor';
import { HomePage } from './home.page';

describe('HomePage', () => {
  let backend: HttpTestingController;
  let fixture: ComponentFixture<HomePage>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(withInterceptors([problemDetailsInterceptor])), provideHttpClientTesting()],
    });
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  function open(roles: Role[], employeeId: number | null): HTMLElement {
    TestBed.inject(AuthService).startSession({
      accessToken: 't', expiresAtUtc: '2999-01-01T00:00:00Z', roles, employeeId, email: 'user@demo.local',
    });
    fixture = TestBed.createComponent(HomePage);
    fixture.detectChanges();
    TestBed.tick();
    return fixture.nativeElement as HTMLElement;
  }

  it('gives an Hr + Employee user the HR dashboard (Hr wins)', () => {
    open(['Employee', 'Hr'], 3);

    backend.expectOne('/api/dashboard');
    backend.expectNone((r) => r.url.includes('/payslip'));
  });

  it('does not repeat a 5xx inline: the toast already reports it', async () => {
    const element = open(['Hr'], null);
    backend.expectOne('/api/dashboard').flush({ title: 'Server error.' }, { status: 500, statusText: 'Error' });
    await fixture.whenStable();

    expect(element.querySelector('.alert')).toBeNull();
    expect(element.textContent).not.toContain('Loading');
  });

  it('explains a missing employee record instead of loading forever', async () => {
    const element = open(['Employee'], null);
    await fixture.whenStable();

    backend.expectNone(() => true);
    expect(element.textContent).toContain('not linked to an employee record');
    expect(element.textContent).not.toContain('Loading');
  });
});
