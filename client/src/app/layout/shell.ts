import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { ElementRef, ViewChild } from '@angular/core';
import { AuthService } from '../core/auth/auth.service';
import { MessageCenterComponent } from '../features/message-center/message-center.component';
import { ChatComponent } from '../features/chat/chat.component';
import { OnInit, signal } from '@angular/core';
import { FeatureSwitchesService } from '../core/feature-switches.service';
import { RealtimeService } from '../core/realtime.service';

@Component({ selector: 'app-shell', standalone: true, imports: [ChatComponent, MessageCenterComponent, RouterLink, RouterLinkActive, RouterOutlet], templateUrl: './shell.html', styleUrl: './shell.scss' })
export class ShellComponent implements OnInit {
  readonly auth = inject(AuthService);
  private readonly featureSwitches = inject(FeatureSwitchesService);
  readonly realtime = inject(RealtimeService);
  readonly messageCenterEnabled = signal(false);
  readonly chatEnabled = signal(false);
  readonly realtimeEnabled = signal(false);
  private readonly router = inject(Router);
  @ViewChild('directChatTrigger') private directChatTrigger?: ElementRef<HTMLButtonElement>;
  directChatOpen = false;
  toggleDirectChat(): void {
    if (this.directChatOpen) {
      this.closeDirectChat();
      return;
    }
    this.directChatOpen = true;
  }
  closeDirectChat(): void {
    this.directChatOpen = false;
    this.directChatTrigger?.nativeElement.focus();
  }
  ngOnInit(): void {
    this.featureSwitches.availability().subscribe({
      next: values => {
        this.messageCenterEnabled.set(values.some(value => value.code === 'MESSAGE_CENTER' && value.enabled));
        this.chatEnabled.set(values.some(value => value.code === 'CHAT' && value.enabled));
        const realtimeEnabled = values.some(value => value.code === 'REALTIME' && value.enabled);
        this.realtimeEnabled.set(realtimeEnabled);
        if (realtimeEnabled) void this.realtime.start();
        else void this.realtime.stop();
      },
      error: () => {
        this.messageCenterEnabled.set(false);
        this.chatEnabled.set(false);
        this.realtimeEnabled.set(false);
        void this.realtime.stop();
      }
    });
  }
  logout(): void { this.auth.logout(); void this.router.navigateByUrl('/login'); }
}

