import { Component } from '@angular/core';
import { StatusPanelComponent } from '../shared/status-panel.component';

@Component({ standalone: true, imports: [StatusPanelComponent], template: `<app-status-panel title="Workspace foundation" message="The application shell is ready. Feature screens will be added in their planned phases." tone="empty" />` })
export class HomePage {}
