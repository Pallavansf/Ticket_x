import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { StatusPanelComponent } from '../../shared/status-panel.component';

@Component({ standalone: true, imports: [RouterLink, StatusPanelComponent], template: `<app-status-panel title="Access denied" message="Your identity is valid, but it does not have the permission required for this area." tone="error"><a routerLink="/app">Return to the workspace</a></app-status-panel>` })
export class UnauthorizedPage {}

