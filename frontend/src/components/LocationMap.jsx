import { MapContainer, TileLayer, Marker, Popup } from 'react-leaflet';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';

// Fix Leaflet default marker icons
delete L.Icon.Default.prototype._getIconUrl;
L.Icon.Default.mergeOptions({
    iconRetinaUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png',
    iconUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png',
    shadowUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png',
});

const PARKING_TYPES = ['Open', 'Covered', 'Garage', 'Street', 'Underground'];

export default function LocationMap({ parkingSpaces = [], singleLocation = null, height = '400px' }) {
    // Single location mode (ParkingDetails page)
    if (singleLocation) {
        const { latitude, longitude, title } = singleLocation;
        const lat = parseFloat(latitude);
        const lng = parseFloat(longitude);

        if (isNaN(lat) || isNaN(lng) || (lat === 0 && lng === 0)) {
            return (
                <div className="map-unavailable">
                    <span>📍</span>
                    <p>Location not available on map</p>
                </div>
            );
        }

        return (
            <div className="map-container" style={{ height, borderRadius: 'var(--radius-md)', overflow: 'hidden' }}>
                <MapContainer
                    center={[lat, lng]}
                    zoom={16}
                    style={{ height: '100%', width: '100%' }}
                    scrollWheelZoom={true}
                >
                    <TileLayer
                        attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
                        url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
                    />
                    <Marker position={[lat, lng]}>
                        <Popup>
                            <div className="map-popup">
                                <strong>{title || 'Parking Location'}</strong>
                                <a
                                    href={`https://www.google.com/maps/dir/?api=1&destination=${lat},${lng}`}
                                    target="_blank"
                                    rel="noopener noreferrer"
                                    className="map-popup-nav"
                                >
                                    🧭 Get Directions
                                </a>
                            </div>
                        </Popup>
                    </Marker>
                </MapContainer>
            </div>
        );
    }

    // Multiple markers mode (Search page)
    const validSpaces = parkingSpaces.filter(p => {
        const lat = parseFloat(p.latitude);
        const lng = parseFloat(p.longitude);
        return !isNaN(lat) && !isNaN(lng) && !(lat === 0 && lng === 0);
    });

    if (validSpaces.length === 0) {
        return (
            <div className="map-unavailable">
                <span>🗺️</span>
                <p>No locations to display on map</p>
            </div>
        );
    }

    // Calculate bounds to fit all markers
    const bounds = L.latLngBounds(
        validSpaces.map(p => [parseFloat(p.latitude), parseFloat(p.longitude)])
    );

    return (
        <div className="map-container" style={{ height, borderRadius: 'var(--radius-md)', overflow: 'hidden' }}>
            <MapContainer
                bounds={bounds}
                boundsOptions={{ padding: [40, 40] }}
                style={{ height: '100%', width: '100%' }}
                scrollWheelZoom={true}
            >
                <TileLayer
                    attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
                    url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
                />
                {validSpaces.map(parking => (
                    <Marker
                        key={parking.id}
                        position={[parseFloat(parking.latitude), parseFloat(parking.longitude)]}
                    >
                        <Popup>
                            <div className="map-popup">
                                <strong>{parking.title}</strong>
                                <p className="map-popup-address">📍 {parking.address}, {parking.city}</p>
                                <div className="map-popup-details">
                                    <span>₹{parking.hourlyRate}/hr</span>
                                    <span>⭐ {parking.averageRating?.toFixed(1) || 'New'}</span>
                                    <span>{PARKING_TYPES[parking.parkingType] || 'Open'}</span>
                                </div>
                                <div className="map-popup-actions">
                                    <a href={`/parking/${parking.id}`} className="map-popup-link">
                                        View Details →
                                    </a>
                                    <a
                                        href={`https://www.google.com/maps/dir/?api=1&destination=${parseFloat(parking.latitude)},${parseFloat(parking.longitude)}`}
                                        target="_blank"
                                        rel="noopener noreferrer"
                                        className="map-popup-nav"
                                    >
                                        🧭 Navigate
                                    </a>
                                </div>
                            </div>
                        </Popup>
                    </Marker>
                ))}
            </MapContainer>
        </div>
    );
}
