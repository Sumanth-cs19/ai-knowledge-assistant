import { Component, computed, input } from '@angular/core';

@Component({
  selector: 'app-skeleton',
  templateUrl: './skeleton.component.html',
  styleUrl: './skeleton.component.scss'
})
export class SkeletonComponent {
  readonly rows = input(3);
  protected readonly skeletonRows = computed(() => Array.from({ length: this.rows() }));
}
