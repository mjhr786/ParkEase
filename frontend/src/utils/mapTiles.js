/**
 * Map tile provider configuration.
 * CARTO basemaps require an API key (VITE_CARTO_API_KEY).
 * If no key is provided, falls back to standard OpenStreetMap tiles (100% free, no key required).
 */
export const MAP_TILE_ATTRIBUTION =
  '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors';

export function getMapTileUrl(theme) {
  const cartoKey = import.meta.env.VITE_CARTO_API_KEY;
  if (cartoKey) {
    const variant = theme === 'light' ? 'light_all' : 'dark_all';
    return `https://{s}.basemaps.cartocdn.com/${variant}/{z}/{x}/{y}{r}.png?key=${cartoKey}`;
  }

  // OpenStreetMap standard tiles — 100% free, no API key required
  return 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png';
}

