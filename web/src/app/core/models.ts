export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
  totalPages: number;
  hasNext: boolean;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  fullName: string;
  email: string;
  password: string;
  phoneNumber?: string | null;
  asOrganizer?: boolean;
}

export interface AuthUser {
  id: string;
  fullName: string;
  email: string;
  phoneNumber?: string | null;
  avatarUrl?: string | null;
  roles: string[];
  isOrganizer: boolean;
  createdAt: string;
}

export interface AuthSession {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  user: AuthUser;
}

export interface EventCategory {
  id: string;
  name: string;
  slug: string;
  icon: string;
  color: string;
  imageUrl?: string | null;
}

export interface VenueSection {
  id: string;
  code: string;
  name: string;
  kind: string;
  color: string;
  priceMultiplier: number;
  capacity: number;
}

export interface Venue {
  id: string;
  name: string;
  slug: string;
  address: string;
  city: string;
  country: string;
  capacity: number;
  layoutKind: string;
  imageUrl?: string | null;
  latitude: number;
  longitude: number;
  sections: VenueSection[];
}

export interface EventSummary {
  id: string;
  slug: string;
  title: string;
  summary?: string | null;
  startsAt: string;
  endsAt: string;
  doorsOpenAt: string;
  venueName: string;
  venueSlug: string;
  city: string;
  address: string;
  category: string;
  categorySlug: string;
  bannerUrl?: string | null;
  basePrice: number;
  minPrice: number;
  currency: string;
  capacity: number;
  ticketsSold: number;
  availableTickets: number;
  isSeated: boolean;
  status: string;
  isFeatured: boolean;
  ageRating: string;
  organizerName: string;
  tags: string[];
}

export interface EventSectionInfo {
  id: string;
  code: string;
  name: string;
  kind: string;
  color: string;
  price: number;
  priceMultiplier: number;
  capacity: number;
  available: number;
}

export interface SeatStats {
  total: number;
  available: number;
  held: number;
  sold: number;
}

export interface EventDetail extends EventSummary {
  description?: string | null;
  venueAddress: string;
  latitude: number;
  longitude: number;
  layoutKind: string;
  mapWidth: number;
  mapHeight: number;
  stageX: number;
  stageY: number;
  stageWidth: number;
  stageHeight: number;
  stageLabel: string;
  organizerId: string;
  sections: EventSectionInfo[];
  stats: SeatStats;
}

export interface MapPoint {
  x: number;
  y: number;
}

export interface StageInfo {
  x: number;
  y: number;
  width: number;
  height: number;
  label: string;
}

export type SeatStatus = 'available' | 'held' | 'sold' | 'blocked';

export interface Seat {
  id: string;
  code: string;
  sectionCode: string;
  row: string;
  number: number;
  x: number;
  y: number;
  rotation: number;
  price: number;
  status: SeatStatus;
}

export interface SeatSection {
  id: string;
  code: string;
  name: string;
  kind: string;
  color: string;
  price: number;
  geometry: MapPoint[];
  seats: Seat[];
}

export interface SeatMap {
  eventId: string;
  title: string;
  slug: string;
  venueName: string;
  city: string;
  layoutKind: string;
  mapWidth: number;
  mapHeight: number;
  stage: StageInfo;
  currency: string;
  maxSeatsPerOrder: number;
  holdExpiresAt?: string | null;
  stats: SeatStats;
  sections: SeatSection[];
}

export interface HeldSeatsResponse {
  bookingId: string;
  reference: string;
  expiresAt: string;
  subtotal: number;
  serviceFee: number;
  total: number;
  currency: string;
  tickets: Ticket[];
}

export interface Ticket {
  id: string;
  code: string;
  sectionName?: string | null;
  row?: string | null;
  seatNumber?: number | null;
  ticketType: string;
  price: number;
  isCheckedIn: boolean;
  checkedInAt?: string | null;
}

export interface Booking {
  id: string;
  reference: string;
  eventId: string;
  eventTitle: string;
  eventSlug: string;
  eventStartsAt: string;
  venueName: string;
  venueImageUrl?: string | null;
  city: string;
  status: string;
  paymentStatus: string;
  subtotal: number;
  serviceFee: number;
  total: number;
  currency: string;
  itemCount: number;
  createdAt: string;
  expiresAt?: string | null;
  confirmedAt?: string | null;
  cancelledAt?: string | null;
  canCancel: boolean;
  tickets: Ticket[];
}

export interface BookingSummary {
  id: string;
  reference: string;
  eventTitle: string;
  eventSlug: string;
  eventStartsAt: string;
  venueName: string;
  bannerUrl?: string | null;
  status: string;
  total: number;
  currency: string;
  itemCount: number;
  createdAt: string;
}

export interface AdminBooking {
  id: string;
  reference: string;
  customerName: string;
  customerEmail: string;
  eventTitle: string;
  eventStartsAt: string;
  itemCount: number;
  total: number;
  status: string;
  paymentStatus: string;
  createdAt: string;
}

export interface AppNotification {
  id: string;
  title: string;
  body: string;
  type: string;
  link?: string | null;
  isRead: boolean;
  createdAt: string;
}

export interface ChartPoint {
  label: string;
  value: number;
  count: number;
}

export interface EventPerformance {
  id: string;
  title: string;
  startsAt: string;
  status: string;
  capacity: number;
  ticketsSold: number;
  sellThrough: number;
  revenue: number;
  checkedIn: number;
}

export interface OrganizerDashboard {
  totalEvents: number;
  liveEvents: number;
  ticketsSold: number;
  grossRevenue: number;
  totalAttendees: number;
  averageSellThrough: number;
  events: EventPerformance[];
  salesByDay: ChartPoint[];
  recentBookings: BookingSummary[];
}

export interface AdminUser {
  id: string;
  fullName: string;
  email: string;
  isOrganizer: boolean;
  roles: string[];
  createdAt: string;
  bookingCount: number;
}

export interface AdminDashboard {
  totalUsers: number;
  organizers: number;
  totalEvents: number;
  publishedEvents: number;
  totalBookings: number;
  grossRevenue: number;
  refunded: number;
  averageOrderValue: number;
  revenueByDay: ChartPoint[];
  bookingsByCategory: ChartPoint[];
  topEvents: EventPerformance[];
  recentUsers: AdminUser[];
}

export interface Attendee {
  ticketCode: string;
  attendeeName: string;
  email: string;
  section?: string | null;
  row?: string | null;
  seat?: number | null;
  bookingReference: string;
  isCheckedIn: boolean;
  checkedInAt?: string | null;
  bookedAt: string;
}

export interface CheckInResult {
  success: boolean;
  message: string;
  attendeeName?: string | null;
  ticketCode?: string | null;
  eventTitle?: string | null;
  seat?: string | null;
  checkedInAt?: string | null;
}

export interface EventFilters {
  q?: string;
  category?: string;
  city?: string;
  sort?: string;
  page?: number;
  pageSize?: number;
  minPrice?: number;
  maxPrice?: number;
  includePast?: boolean;
}

export interface SeatChange {
  seatId: string;
  code: string;
  status: SeatStatus;
}

export interface CreateEventPayload {
  title: string;
  summary?: string;
  description?: string;
  categoryId: string;
  venueId: string;
  startsAt: string;
  endsAt?: string;
  doorsOpenAt?: string;
  basePrice: number;
  isSeated: boolean;
  bannerUrl?: string;
  tags?: string;
  ageRating?: string;
  publish: boolean;
  isFeatured?: boolean;
}
