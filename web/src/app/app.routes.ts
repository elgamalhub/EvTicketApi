import { Routes } from '@angular/router';
import { authGuard, guestGuard, adminGuard, organizerGuard } from './core/guards';

export const routes: Routes = [
  {
    path: '',
    title: 'EvTicket — Live events, real seats',
    loadComponent: () => import('./features/home/home-page').then((m) => m.HomePage)
  },
  {
    path: 'events',
    title: 'Discover events — EvTicket',
    loadComponent: () => import('./features/events/events-page').then((m) => m.EventsPage)
  },
  {
    path: 'events/:slug',
    title: 'Event — EvTicket',
    loadComponent: () => import('./features/event-detail/event-detail-page').then((m) => m.EventDetailPage)
  },
  {
    path: 'events/:slug/seats',
    title: 'Choose seats — EvTicket',
    canActivate: [authGuard],
    loadComponent: () => import('./features/seat-map/seat-map-page').then((m) => m.SeatMapPage)
  },
  {
    path: 'checkout/:bookingId',
    title: 'Checkout — EvTicket',
    canActivate: [authGuard],
    loadComponent: () => import('./features/checkout/checkout-page').then((m) => m.CheckoutPage)
  },
  {
    path: 'bookings/:id',
    title: 'Your tickets — EvTicket',
    canActivate: [authGuard],
    loadComponent: () => import('./features/booking/booking-detail-page').then((m) => m.BookingDetailPage)
  },
  {
    path: 'account',
    title: 'Your account — EvTicket',
    canActivate: [authGuard],
    loadComponent: () => import('./features/account/account-page').then((m) => m.AccountPage)
  },
  {
    path: 'login',
    title: 'Sign in — EvTicket',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/login-page').then((m) => m.LoginPage)
  },
  {
    path: 'register',
    title: 'Create an account — EvTicket',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/register-page').then((m) => m.RegisterPage)
  },
  {
    path: 'forgot-password',
    title: 'Reset your password — EvTicket',
    loadComponent: () => import('./features/auth/forgot-password-page').then((m) => m.ForgotPasswordPage)
  },
  {
    path: 'reset-password',
    title: 'Choose a new password — EvTicket',
    loadComponent: () => import('./features/auth/reset-password-page').then((m) => m.ResetPasswordPage)
  },
  {
    path: 'organizer',
    title: 'Organizer studio — EvTicket',
    canActivate: [organizerGuard],
    loadComponent: () => import('./features/organizer/organizer-dashboard-page').then((m) => m.OrganizerDashboardPage)
  },
  {
    path: 'organizer/events/new',
    title: 'Create event — EvTicket',
    canActivate: [organizerGuard],
    loadComponent: () => import('./features/organizer/event-form-page').then((m) => m.EventFormPage)
  },
  {
    path: 'organizer/events/:id/edit',
    title: 'Edit event — EvTicket',
    canActivate: [organizerGuard],
    loadComponent: () => import('./features/organizer/event-form-page').then((m) => m.EventFormPage)
  },
  {
    path: 'organizer/check-in',
    title: 'Check-in desk — EvTicket',
    canActivate: [organizerGuard],
    loadComponent: () => import('./features/organizer/check-in-page').then((m) => m.CheckInPage)
  },
  {
    path: 'admin',
    title: 'Admin console — EvTicket',
    canActivate: [adminGuard],
    loadComponent: () => import('./features/admin/admin-dashboard-page').then((m) => m.AdminDashboardPage)
  },
  {
    path: '**',
    title: 'Not found — EvTicket',
    loadComponent: () => import('./features/not-found/not-found-page').then((m) => m.NotFoundPage)
  }
];
