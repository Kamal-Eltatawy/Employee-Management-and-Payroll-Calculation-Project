import { Component, computed, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { IconComponent } from '../../shared/ui/icon.component';
import { AuthService } from '../auth/auth.service';
import { LanguageService } from '../i18n/language.service';
import { ToastComponent } from '../notifications/toast.component';

/** Sidebar layout for signed-in users (D57); a top bar with a menu button on small screens. */
@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, ToastComponent, IconComponent],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss',
})
export class ShellComponent {
  protected readonly auth = inject(AuthService);
  protected readonly language = inject(LanguageService);
  protected readonly menuOpen = signal(false);

  protected readonly initials = computed(() => (this.auth.currentUser()?.email ?? '').slice(0, 2).toUpperCase());

  constructor() {
    // Close the mobile menu after every navigation.
    inject(Router)
      .events.pipe(filter((event) => event instanceof NavigationEnd))
      .subscribe(() => this.menuOpen.set(false));
  }
}
