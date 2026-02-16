import { MapContainer, TileLayer, Marker, Popup, useMap } from 'react-leaflet';
import { useEffect, useMemo, useRef } from 'react';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';

// Fix Leaflet default marker icons
delete L.Icon.Default.prototype._getIconUrl;
L.Icon.Default.mergeOptions({
    iconRetinaUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png',
    iconUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png',
    shadowUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png',
});

// Highlighted marker icon (larger, different color)
const highlightedIcon = new L.Icon({
    iconUrl: 'https://raw.githubusercontent.com/pointhi/leaflet-color-markers/master/img/marker-icon-2x-red.png',
    shadowUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png',
    iconSize: [30, 49],
    iconAnchor: [15, 49],
    popupAnchor: [1, -40],
    shadowSize: [41, 41],
});

const PARKING_TYPES = ['Open', 'Covered', 'Garage', 'Street', 'Underground'];

// Component to auto-fit bounds when parking data changes
function FitBounds({ spaces }) {
    const map = useMap();
    useEffect(() => {
        if (spaces.length > 0) {
            const bounds = L.latLngBounds(
                spaces.map(p => [parseFloat(p.latitude), parseFloat(p.longitude)])
            );
            map.fitBounds(bounds, { padding: [40, 40], maxZoom: 15 });
        }
    }, [spaces, map]);
    return null;
}

export default function LocationMap({ parkingSpaces = [], singleLocation = null, height = '400px', highlightedId = null, onMarkerHover = null }) {
    const markerRefs = useRef({});

    // Auto-open popup for highlighted marker
    useEffect(() => {
        if (highlightedId && markerRefs.current[highlightedId]) {
            markerRefs.current[highlightedId].openPopup();
        }
    }, [highlightedId]);

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
    const validSpaces = useMemo(() =>
        parkingSpaces.filter(p => {
            const lat = parseFloat(p.latitude);
            const lng = parseFloat(p.longitude);
            return !isNaN(lat) && !isNaN(lng) && !(lat === 0 && lng === 0);
        }),
        [parkingSpaces]
    );

    if (validSpaces.length === 0) {
        return (
            <div className="map-unavailable" style={{ height }}>
                <span>🗺️</span>
                <p>Search for parking to see locations on the map</p>
            </div>
        );
    }

    const defaultCenter = [20.5937, 78.9629]; // India center

    return (
        <div className="map-container" style={{ height, borderRadius: 'var(--radius-md)', overflow: 'hidden' }}>
            <MapContainer
                center={defaultCenter}
                zoom={5}
                style={{ height: '100%', width: '100%' }}
                scrollWheelZoom={true}
            >
                <TileLayer
                    attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
                    url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
                />
                <FitBounds spaces={validSpaces} />
                {validSpaces.map(parking => (
                    <Marker
                        key={parking.id}
                        position={[parseFloat(parking.latitude), parseFloat(parking.longitude)]}
                        icon={parking.id === highlightedId ? highlightedIcon : new L.Icon.Default()}
                        ref={(ref) => {
                            if (ref) {
                                markerRefs.current[parking.id] = ref;
                            } else {
                                delete markerRefs.current[parking.id];
                            }
                        }}
                        eventHandlers={{
                            mouseover: () => onMarkerHover?.(parking.id),
                            mouseout: () => onMarkerHover?.(null),
                        }}
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
