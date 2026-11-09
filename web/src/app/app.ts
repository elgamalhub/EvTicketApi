import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { SiteHeader } from './shared/site-header';
import { SiteFooter } from './shared/site-footer';
import { ToastHost } from './shared/toast-host';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, SiteHeader, SiteFooter, ToastHost],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {}
