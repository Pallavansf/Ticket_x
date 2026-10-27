import { Component, ElementRef, EventEmitter, Input, OnInit, Output, ViewChild, inject, signal } from '@angular/core';
import { FeatureSwitchesService } from '../../core/feature-switches.service';

@Component({
  selector: 'app-chat',
  standalone: true,
  templateUrl: './chat.component.html',
  styleUrl: './chat.component.scss'
})
export class ChatComponent implements OnInit {
  private readonly switches = inject(FeatureSwitchesService);
  readonly available = signal(false);
  @ViewChild('closeButton')
  set closeButton(element: ElementRef<HTMLButtonElement> | undefined) {
    if (!this.isWorkItemChat) element?.nativeElement.focus();
  }
  @Input() workItemId: number | null = null;
  @Output() closed = new EventEmitter<void>();

  ngOnInit(): void {
    this.switches.availability().subscribe({
      next: values => this.available.set(values.some(value => value.code === 'CHAT' && value.enabled)),
      error: () => this.available.set(false)
    });
  }

  closeFromKeyboard(): void {
    if (!this.isWorkItemChat) this.closed.emit();
  }

  get isWorkItemChat(): boolean {
    return this.workItemId !== null;
  }
}
