import { BreakpointObserver } from '@angular/cdk/layout';
import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatMenuModule } from '@angular/material/menu';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ToastrService } from 'ngx-toastr';
import { map } from 'rxjs';

import { AuthService } from '../../core/services/auth.service';
import { PreferencesService } from '../../core/services/preferences.service';

interface NavigationItem {
  icon: string;
  label: string;
  route: string;
}

@Component({
  selector: 'app-dashboard-layout',
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatButtonModule,
    MatIconModule,
    MatListModule,
    MatMenuModule,
    MatSidenavModule,
    MatToolbarModule,
    MatTooltipModule
  ],
  templateUrl: './dashboard-layout.component.html',
  styleUrl: './dashboard-layout.component.scss'
})
export class DashboardLayoutComponent {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly toastr = inject(ToastrService);
  private readonly breakpointObserver = inject(BreakpointObserver);
  protected readonly isCompactSidebar = inject(PreferencesService).isCompactSidebar;
  protected readonly isMobile = toSignal(
    this.breakpointObserver.observe('(max-width: 720px)').pipe(map((result) => result.matches)),
    { initialValue: false }
  );
  protected readonly mobileSidebarOpened = signal(false);

  protected readonly currentUser = this.authService.currentUser;
  protected readonly navigationItems = computed(() => this.allNavigationItems.filter((item) => {
    return item.route !== '/admin' || this.authService.isAdmin();
  }));

  private readonly allNavigationItems: NavigationItem[] = [
    { icon: 'dashboard', label: 'Dashboard', route: '/dashboard' },
    { icon: 'description', label: 'Documents', route: '/documents' },
    { icon: 'chat', label: 'Chat', route: '/chat' },
    { icon: 'settings', label: 'Settings', route: '/settings' },
    { icon: 'admin_panel_settings', label: 'Admin', route: '/admin' }
  ];

  protected toggleMobileSidebar(): void {
    this.mobileSidebarOpened.update((opened) => !opened);
  }

  protected closeMobileSidebar(): void {
    if (this.isMobile()) {
      this.mobileSidebarOpened.set(false);
    }
  }

  protected logout(): void {
    this.authService.logout().subscribe({
      next: () => this.finishLogout(),
      error: () => this.finishLogout()
    });
  }

  private finishLogout(): void {
    this.toastr.success('You have been signed out.', 'Signed out');
    void this.router.navigate(['/login']);
  }
}
