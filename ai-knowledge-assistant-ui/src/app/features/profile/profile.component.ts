import { DatePipe } from '@angular/common';
import { Component, computed, inject } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';

import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-profile',
  imports: [DatePipe, MatCardModule, MatIconModule],
  templateUrl: './profile.component.html',
  styleUrl: './profile.component.scss'
})
export class ProfileComponent {
  private readonly authService = inject(AuthService);
  protected readonly currentUser = this.authService.currentUser;
  protected readonly isAuthenticated = computed(() => this.authService.isAuthenticated());
  protected readonly accountCreatedAt = computed(() => null as string | null);
}
