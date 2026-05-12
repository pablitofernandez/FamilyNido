/** Wire shape returned by `GET /api/family`. */
export interface Family {
  id: string;
  name: string;
  timeZone: string;
  locale: string;
  latitude: number | null;
  longitude: number | null;
  locationLabel: string | null;
}

/** Payload accepted by `PUT /api/family/location`. Pass all-null to clear. */
export interface UpdateFamilyLocationRequest {
  latitude: number | null;
  longitude: number | null;
  locationLabel: string | null;
}
