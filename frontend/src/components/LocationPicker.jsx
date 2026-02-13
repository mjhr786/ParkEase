import { useState, useEffect, useCallback } from 'react';
import { MapContainer, TileLayer, Marker, useMapEvents, useMap } from 'react-leaflet';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';

// Fix Leaflet default marker icons (broken in bundlers)
delete L.Icon.Default.prototype._getIconUrl;
L.Icon.Default.mergeOptions({
    iconRetinaUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png',
    iconUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png',
    shadowUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png',
});

function MapClickHandler({ onLocationSelect }) {
    useMapEvents({
        click(e) {
            onLocationSelect(e.latlng.lat, e.latlng.lng);
        },
    });
    return null;
}

function RecenterMap({ lat, lng }) {
    const map = useMap();
    useEffect(() => {
        if (lat && lng) {
            map.setView([lat, lng], 15);
        }
    }, [lat, lng, map]);
    return null;
}

export default function LocationPicker({ latitude, longitude, onLocationSelect }) {
    const [position, setPosition] = useState(null);
    const [locating, setLocating] = useState(false);

    const lat = parseFloat(latitude);
    const lng = parseFloat(longitude);
    const hasCoords = !isNaN(lat) && !isNaN(lng) && lat !== 0 && lng !== 0;

    useEffect(() => {
        if (hasCoords) {
            setPosition([lat, lng]);
        }
    }, []);

    const handleMapClick = useCallback((clickLat, clickLng) => {
        setPosition([clickLat, clickLng]);
        onLocationSelect(clickLat.toFixed(6), clickLng.toFixed(6));
    }, [onLocationSelect]);

    const handleUseMyLocation = () => {
        if (!navigator.geolocation) {
            alert('Geolocation is not supported by your browser');
            return;
        }
        setLocating(true);
        navigator.geolocation.getCurrentPosition(
            (pos) => {
                const { latitude: geoLat, longitude: geoLng } = pos.coords;
                setPosition([geoLat, geoLng]);
                onLocationSelect(geoLat.toFixed(6), geoLng.toFixed(6));
                setLocating(false);
            },
            (err) => {
                alert('Unable to get your location. Please click on the map instead.');
                setLocating(false);
            },
            { enableHighAccuracy: true, timeout: 10000 }
        );
    };

    const center = hasCoords ? [lat, lng] : [20.5937, 78.9629]; // Default: India center
    const zoom = hasCoords ? 15 : 5;

    return (
        <div className="location-picker">
            <div className="location-picker-header">
                <label className="form-label" style={{ marginBottom: 0 }}>
                    📍 Select Location on Map
                </label>
                <button
                    type="button"
                    className="btn btn-secondary btn-sm"
                    onClick={handleUseMyLocation}
                    disabled={locating}
                    style={{ fontSize: '0.8rem', padding: '0.3rem 0.75rem' }}
                >
                    {locating ? '⏳ Locating...' : '📍 Use My Location'}
                </button>
            </div>

            <div className="map-container" style={{ height: '300px', borderRadius: 'var(--radius-md)', overflow: 'hidden', marginTop: '0.5rem' }}>
                <MapContainer
                    center={center}
                    zoom={zoom}
                    style={{ height: '100%', width: '100%' }}
                    scrollWheelZoom={true}
                >
                    <TileLayer
                        attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
                        url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
                    />
                    <MapClickHandler onLocationSelect={handleMapClick} />
                    {position && (
                        <>
                            <Marker position={position} />
                            <RecenterMap lat={position[0]} lng={position[1]} />
                        </>
                    )}
                </MapContainer>
            </div>

            {position && (
                <div className="location-coords">
                    <span>Lat: <strong>{position[0].toFixed ? position[0].toFixed(6) : position[0]}</strong></span>
                    <span>Lng: <strong>{position[1].toFixed ? position[1].toFixed(6) : position[1]}</strong></span>
                </div>
            )}
            {!position && (
                <p className="location-hint">Click on the map or use "My Location" to set the parking spot location</p>
            )}
        </div>
    );
}
