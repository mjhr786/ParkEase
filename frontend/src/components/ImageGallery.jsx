
import { useState } from 'react';
import { API_BASE_URL } from '../config';

export default function ImageGallery({ images, title }) {
    const [selectedImageIndex, setSelectedImageIndex] = useState(null);

    if (!images || images.length === 0) {
        return (
            <div className="parking-image" style={{ height: '300px', marginBottom: '1.5rem', fontSize: '5rem', display: 'flex', alignItems: 'center', justifyContent: 'center', background: 'var(--color-bg-card)', borderRadius: 'var(--radius-md)' }}>
                🅿️
            </div>
        );
    }

    const handleNext = (e) => {
        e.stopPropagation();
        setSelectedImageIndex(prev => (prev + 1) % images.length);
    };

    const handlePrev = (e) => {
        e.stopPropagation();
        setSelectedImageIndex(prev => (prev - 1 + images.length) % images.length);
    };

    const handleClose = () => {
        setSelectedImageIndex(null);
    };

    const getImageUrl = (url) => {
        if (!url) return '';
        if (url.startsWith('http')) return url;
        return `${API_BASE_URL}${url}`;
    };

    return (
        <div style={{ marginBottom: '1.5rem' }}>
            {/* Main Grid */}
            <div style={{
                display: 'grid',
                gridTemplateColumns: 'repeat(auto-fill, minmax(200px, 1fr))',
                gap: '0.5rem'
            }}>
                {images.map((url, i) => (
                    <img
                        key={i}
                        src={getImageUrl(url)}
                        alt={`${title} - ${i + 1}`}
                        style={{
                            width: '100%',
                            height: '150px',
                            objectFit: 'cover',
                            borderRadius: 'var(--radius-md)',
                            cursor: 'pointer',
                            transition: 'transform 0.2s'
                        }}
                        onClick={() => setSelectedImageIndex(i)}
                        onMouseEnter={(e) => e.target.style.transform = 'scale(1.02)'}
                        onMouseLeave={(e) => e.target.style.transform = 'scale(1)'}
                        loading="lazy"
                    />
                ))}
            </div>

            {/* Lightbox Modal */}
            {selectedImageIndex !== null && (
                <div
                    style={{
                        position: 'fixed',
                        top: 0,
                        left: 0,
                        right: 0,
                        bottom: 0,
                        background: 'rgba(0, 0, 0, 0.9)',
                        zIndex: 2000,
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                    }}
                    onClick={handleClose}
                >
                    <button
                        onClick={handleClose}
                        style={{
                            position: 'absolute',
                            top: '20px',
                            right: '20px',
                            background: 'transparent',
                            border: 'none',
                            color: 'white',
                            fontSize: '2rem',
                            cursor: 'pointer',
                            padding: '10px',
                            zIndex: 2001
                        }}
                    >
                        ×
                    </button>

                    <button
                        onClick={handlePrev}
                        style={{
                            position: 'absolute',
                            left: '20px',
                            background: 'rgba(255, 255, 255, 0.1)',
                            border: 'none',
                            color: 'white',
                            fontSize: '2rem',
                            cursor: 'pointer',
                            padding: '10px 20px',
                            borderRadius: '50%',
                            transition: 'background 0.3s'
                        }}
                        onMouseEnter={(e) => e.target.style.background = 'rgba(255, 255, 255, 0.2)'}
                        onMouseLeave={(e) => e.target.style.background = 'rgba(255, 255, 255, 0.1)'}
                    >
                        ‹
                    </button>

                    <img
                        src={getImageUrl(images[selectedImageIndex])}
                        alt={`${title} - Full`}
                        style={{
                            maxWidth: '90vw',
                            maxHeight: '90vh',
                            objectFit: 'contain',
                            borderRadius: '4px'
                        }}
                        onClick={(e) => e.stopPropagation()} // Prevent closing when clicking image
                    />

                    <button
                        onClick={handleNext}
                        style={{
                            position: 'absolute',
                            right: '20px',
                            background: 'rgba(255, 255, 255, 0.1)',
                            border: 'none',
                            color: 'white',
                            fontSize: '2rem',
                            cursor: 'pointer',
                            padding: '10px 20px',
                            borderRadius: '50%',
                            transition: 'background 0.3s'
                        }}
                        onMouseEnter={(e) => e.target.style.background = 'rgba(255, 255, 255, 0.2)'}
                        onMouseLeave={(e) => e.target.style.background = 'rgba(255, 255, 255, 0.1)'}
                    >
                        ›
                    </button>

                    <div style={{
                        position: 'absolute',
                        bottom: '20px',
                        color: 'white',
                        background: 'rgba(0,0,0,0.5)',
                        padding: '5px 10px',
                        borderRadius: '15px',
                        fontSize: '0.9rem'
                    }}>
                        {selectedImageIndex + 1} / {images.length}
                    </div>
                </div>
            )}
        </div>
    );
}
