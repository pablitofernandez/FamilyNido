/** Wire shape returned by `GET /api/weather/today`. */
export interface WeatherToday {
  locationLabel: string;
  currentTemperature: number;
  apparentTemperature: number | null;
  maxTemperature: number;
  minTemperature: number;
  weatherCode: number;
  weatherLabel: string;
  weatherIcon: string;
  precipitationProbability: number | null;
  sunrise: string | null;
  sunset: string | null;
}
