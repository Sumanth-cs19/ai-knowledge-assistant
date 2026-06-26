import { Component, computed, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatMenuModule } from '@angular/material/menu';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { ToastrService } from 'ngx-toastr';

import { AuthService } from '../../core/services/auth.service';

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
    MatToolbarModule
  ],
  templateUrl: './dashboard-layout.component.html',
  styleUrl: './dashboard-layout.component.scss'
})
export class DashboardLayoutComponent {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly toastr = inject(ToastrService);

  protected readonly currentUser = this.authService.currentUser;
  protected readonly navigationItems = computed(() => this.allNavigationItems.filter((item) => {
    return item.route !== '/admin' || this.authService.isAdmin();
  }));

  private readonly allNavigationItems: NavigationItem[] = [
    { icon: 'dashboard', label: 'Dashboard', route: '/dashboard' },
    { icon: 'description', label: 'Documents', route: '/documents' },
    { icon: 'chat', label: 'Chat', route: '/chat' },
    { icon: 'forum', label: 'Conversations', route: '/conversations' },
    { icon: 'person', label: 'Profile', route: '/profile' },
    { icon: 'settings', label: 'Settings', route: '/settings' },
    { icon: 'admin_panel_settings', label: 'Admin', route: '/admin' }
  ];

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
