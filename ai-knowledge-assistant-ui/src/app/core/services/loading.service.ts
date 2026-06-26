import { Injectable, computed, signal } from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class LoadingService {
  private readonly pendingRequests = signal(0);
  readonly isLoading = computed(() => this.pendingRequests() > 0);

  show(): void {
    this.pendingRequests.update((value) => value + 1);
  }

  hide(): void {
    this.pendingRequests.update((value) => Math.max(0, value - 1));
  }
}
