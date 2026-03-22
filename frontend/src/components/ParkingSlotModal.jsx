import { useEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';

/**
 * ParkingSlotModal
 * Renders a popup with a visual top-down parking lot layout.
 * Handles any number of slots (50, 100, etc.) via scrollable rows.
 *
 * Props:
 *   isOpen       - boolean
 *   onClose      - () => void
 *   slotAvailability - [{ slotNumber, blockedForSelection }]
 *   selectedSlot - currently selected slot number (string or null)
 *   onSelect     - (slotNumber: string) => void
 *   hasTimeRange - boolean — whether start/end times are chosen
 */
export default function ParkingSlotModal({
    isOpen,
    onClose,
    slotAvailability,
    selectedSlot,
    onSelect,
    hasTimeRange,
}) {
    const overlayRef = useRef(null);

    // Close on Escape key
    useEffect(() => {
        if (!isOpen) return;
        const handler = (e) => { if (e.key === 'Escape') onClose(); };
        window.addEventListener('keydown', handler);
        return () => window.removeEventListener('keydown', handler);
    }, [isOpen, onClose]);

    // Prevent body scroll when open
    useEffect(() => {
        document.body.style.overflow = isOpen ? 'hidden' : '';
        return () => { document.body.style.overflow = ''; };
    }, [isOpen]);

    if (!isOpen) return null;

    const totalSlots = slotAvailability.length;
    // Split slots into rows of 6 (3 left lane + aisle + 3 right lane)
    const SLOTS_PER_ROW = 6;
    const rows = [];
    for (let i = 0; i < totalSlots; i += SLOTS_PER_ROW) {
        rows.push(slotAvailability.slice(i, i + SLOTS_PER_ROW));
    }

    const available = slotAvailability.filter(s => !s.blockedForSelection).length;
    const booked = totalSlots - available;

    return createPortal(
        <div
            ref={overlayRef}
            style={styles.overlay}
            onClick={(e) => { if (e.target === overlayRef.current) onClose(); }}
        >
            <div style={styles.modal}>
                {/* Header */}
                <div style={styles.header}>
                    <div>
                        <h2 style={styles.title}>🅿️ Select a Parking Slot</h2>
                        <p style={styles.subtitle}>
                            {hasTimeRange
                                ? `${available} available · ${booked} booked for your time`
                                : 'Select start & end time first to see real-time availability'}
                        </p>
                    </div>
                    <button style={styles.closeBtn} onClick={onClose} title="Close">✕</button>
                </div>

                {/* Legend */}
                <div style={styles.legend}>
                    <span style={styles.legendItem}>
                        <span style={{ ...styles.legendDot, background: '#10b981' }} /> Available
                    </span>
                    <span style={styles.legendItem}>
                        <span style={{ ...styles.legendDot, background: '#ef4444' }} /> Booked
                    </span>
                    <span style={styles.legendItem}>
                        <span style={{ ...styles.legendDot, background: '#6366f1', boxShadow: '0 0 8px #6366f1' }} /> Selected
                    </span>
                </div>

                {/* Parking lot visual */}
                <div style={styles.lotContainer}>
                    {/* Entry/Exit indicator */}
                    <div style={styles.entryRow}>
                        <div style={styles.entryArrow}>⬆ EXIT</div>
                        <div style={styles.road} />
                        <div style={styles.entryArrow}>ENTRY ⬇</div>
                    </div>

                    {/* Slot rows */}
                    <div style={styles.slotsArea}>
                        {rows.map((rowSlots, rowIdx) => {
                            const leftSlots = rowSlots.slice(0, 3);
                            const rightSlots = rowSlots.slice(3, 6);
                            return (
                                <div key={rowIdx} style={styles.slotRow}>
                                    {/* Left bank */}
                                    <div style={styles.slotBank}>
                                        {leftSlots.map(slot => (
                                            <SlotCell
                                                key={slot.slotNumber}
                                                slot={slot}
                                                isSelected={String(slot.slotNumber) === String(selectedSlot)}
                                                onSelect={onSelect}
                                            />
                                        ))}
                                        {/* Fill empty cells if row is incomplete */}
                                        {leftSlots.length < 3 && Array.from({ length: 3 - leftSlots.length }).map((_, i) => (
                                            <div key={`empty-l-${i}`} style={styles.emptyCell} />
                                        ))}
                                    </div>

                                    {/* Center driving aisle */}
                                    <div style={styles.aisle}>
                                        <span style={styles.aisleArrow}>↕</span>
                                    </div>

                                    {/* Right bank */}
                                    <div style={styles.slotBank}>
                                        {rightSlots.map(slot => (
                                            <SlotCell
                                                key={slot.slotNumber}
                                                slot={slot}
                                                isSelected={String(slot.slotNumber) === String(selectedSlot)}
                                                onSelect={onSelect}
                                            />
                                        ))}
                                        {rightSlots.length < 3 && Array.from({ length: 3 - rightSlots.length }).map((_, i) => (
                                            <div key={`empty-r-${i}`} style={styles.emptyCell} />
                                        ))}
                                    </div>
                                </div>
                            );
                        })}
                    </div>
                </div>

                {/* Footer */}
                <div style={styles.footer}>
                    {selectedSlot
                        ? <span style={styles.selectedInfo}>✅ Slot {selectedSlot} selected</span>
                        : <span style={styles.hintText}>Tap an available slot to select it</span>
                    }
                    <button
                        style={{
                            ...styles.confirmBtn,
                            opacity: selectedSlot ? 1 : 0.4,
                            cursor: selectedSlot ? 'pointer' : 'not-allowed',
                        }}
                        onClick={() => { if (selectedSlot) onClose(); }}
                        disabled={!selectedSlot}
                    >
                        Confirm Selection
                    </button>
                </div>
            </div>
            <style>{`
                @keyframes fadeInTooltip {
                    from { opacity: 0; transform: translateX(-50%) translateY(-4px); }
                    to   { opacity: 1; transform: translateX(-50%) translateY(0); }
                }
            `}</style>
        </div>
        , document.body);
}

function formatSlotDate(dateStr) {
    const d = new Date(dateStr);
    return d.toLocaleDateString('en-IN', { day: 'numeric', month: 'short' }) +
        ' ' + d.toLocaleTimeString('en-IN', { hour: '2-digit', minute: '2-digit', hour12: true });
}

function SlotCell({ slot, isSelected, onSelect }) {
    const [hovered, setHovered] = useState(false);
    const [tooltipPos, setTooltipPos] = useState({ top: 0, left: 0 });
    const cellRef = useRef(null);
    const isBooked = slot.blockedForSelection;
    const hasReservations = slot.reservations && slot.reservations.length > 0;

    let bgColor = '#1a2e1a';
    let borderColor = '#10b981';
    let textColor = '#10b981';
    let cursor = 'pointer';

    if (isBooked) {
        bgColor = '#2e1a1a';
        borderColor = '#ef4444';
        textColor = '#ef4444';
        cursor = 'not-allowed';
    }
    if (isSelected) {
        bgColor = 'rgba(99,102,241,0.25)';
        borderColor = '#6366f1';
        textColor = '#818cf8';
    }

    const handleMouseEnter = () => {
        if (cellRef.current) {
            const rect = cellRef.current.getBoundingClientRect();
            setTooltipPos({
                top: rect.bottom + 8,
                left: rect.left + rect.width / 2,
            });
        }
        setHovered(true);
    };

    return (
        <div ref={cellRef} style={{ position: 'relative', flex: 1 }}>
            <div
                onClick={() => !isBooked && onSelect(String(slot.slotNumber))}
                onMouseEnter={handleMouseEnter}
                onMouseLeave={() => setHovered(false)}
                title=""
                style={{
                    ...styles.slot,
                    background: bgColor,
                    border: `2px solid ${borderColor}`,
                    color: textColor,
                    cursor,
                    boxShadow: isSelected ? `0 0 12px ${borderColor}60` : 'none',
                    transform: isSelected ? 'scale(1.05)' : 'scale(1)',
                    width: '100%',
                }}
            >
                {isBooked && <div style={styles.carIcon}>🚗</div>}
                <span style={styles.slotNumber}>P{slot.slotNumber}</span>
                <span style={{ ...styles.slotStatus, color: textColor, fontSize: '0.6rem' }}>
                    {isBooked ? 'Booked' : isSelected ? 'Selected' : 'Free'}
                </span>
                <div style={{ ...styles.parkingLine, top: 0 }} />
                <div style={{ ...styles.parkingLine, bottom: 0 }} />
            </div>

            {/* Render tooltip via portal to escape overflow clipping */}
            {hovered && createPortal(
                <div style={{
                    ...styles.tooltip,
                    position: 'fixed',
                    top: tooltipPos.top,
                    left: tooltipPos.left,
                    transform: 'translateX(-50%)',
                }}>
                    <div style={styles.tooltipTitle}>Slot P{slot.slotNumber}</div>
                    {hasReservations ? (
                        slot.reservations.map((r, i) => (
                            <div key={i} style={styles.tooltipReservation}>
                                <span style={styles.tooltipIcon}>🔒</span>
                                <span style={styles.tooltipRange}>
                                    {formatSlotDate(r.startDateTime)}
                                    <span style={styles.tooltipArrow}> → </span>
                                    {formatSlotDate(r.endDateTime)}
                                </span>
                            </div>
                        ))
                    ) : (
                        <div style={styles.tooltipFree}>✓ No reservations</div>
                    )}
                    <div style={styles.tooltipArrowEl} />
                </div>,
                document.body
            )}
        </div>
    );
}

const styles = {
    overlay: {
        position: 'fixed',
        inset: 0,
        background: 'rgba(0,0,0,0.75)',
        backdropFilter: 'blur(6px)',
        zIndex: 9999,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        padding: '1rem',
        animation: 'fadeInOverlay 0.2s ease',
    },
    modal: {
        background: 'linear-gradient(145deg, #12121a, #1a1a25)',
        border: '1px solid rgba(255,255,255,0.1)',
        borderRadius: '20px',
        width: '100%',
        maxWidth: '700px',
        maxHeight: '90vh',
        display: 'flex',
        flexDirection: 'column',
        boxShadow: '0 25px 60px rgba(0,0,0,0.8)',
        overflow: 'hidden',
    },
    header: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'flex-start',
        padding: '1.5rem 1.5rem 1rem',
        borderBottom: '1px solid rgba(255,255,255,0.08)',
        flexShrink: 0,
    },
    title: {
        fontSize: '1.3rem',
        fontWeight: 700,
        color: '#fff',
        margin: 0,
    },
    subtitle: {
        fontSize: '0.85rem',
        color: '#a0a0b0',
        marginTop: '0.25rem',
    },
    closeBtn: {
        background: 'rgba(255,255,255,0.08)',
        border: '1px solid rgba(255,255,255,0.12)',
        color: '#a0a0b0',
        width: '32px',
        height: '32px',
        borderRadius: '50%',
        cursor: 'pointer',
        fontSize: '0.85rem',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        flexShrink: 0,
        transition: 'all 0.2s',
    },
    legend: {
        display: 'flex',
        gap: '1.5rem',
        padding: '0.75rem 1.5rem',
        background: 'rgba(255,255,255,0.03)',
        flexShrink: 0,
        flexWrap: 'wrap',
    },
    legendItem: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.5rem',
        fontSize: '0.8rem',
        color: '#a0a0b0',
    },
    legendDot: {
        width: '12px',
        height: '12px',
        borderRadius: '3px',
        flexShrink: 0,
    },
    lotContainer: {
        overflowY: 'auto',
        flex: 1,
        padding: '1rem 1.5rem',
        background: '#0d1a0d',
    },
    entryRow: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.5rem',
        marginBottom: '0.5rem',
        justifyContent: 'space-between',
    },
    entryArrow: {
        fontSize: '0.7rem',
        color: '#f59e0b',
        fontWeight: 700,
        letterSpacing: '0.05em',
        padding: '0.2rem 0.5rem',
        background: 'rgba(245, 158, 11, 0.1)',
        borderRadius: '4px',
        border: '1px solid rgba(245,158,11,0.3)',
    },
    road: {
        flex: 1,
        height: '2px',
        background: 'repeating-linear-gradient(90deg, #f59e0b 0, #f59e0b 10px, transparent 10px, transparent 20px)',
        margin: '0 0.5rem',
    },
    slotsArea: {
        display: 'flex',
        flexDirection: 'column',
        gap: '0.5rem',
    },
    slotRow: {
        display: 'flex',
        gap: '0.5rem',
        alignItems: 'stretch',
    },
    slotBank: {
        display: 'flex',
        gap: '0.4rem',
        flex: 1,
    },
    aisle: {
        width: '32px',
        flexShrink: 0,
        background: '#1a1a0d',
        borderRadius: '4px',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        position: 'relative',
    },
    aisleArrow: {
        color: '#f59e0b',
        fontSize: '1rem',
        opacity: 0.6,
    },
    emptyCell: {
        flex: 1,
    },
    slot: {
        flex: 1,
        minWidth: '70px',
        height: '90px',
        borderRadius: '8px',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        position: 'relative',
        transition: 'all 0.18s ease',
        userSelect: 'none',
        overflow: 'hidden',
    },
    carIcon: {
        fontSize: '1.2rem',
        lineHeight: 1,
        marginBottom: '2px',
        filter: 'grayscale(0.3)',
    },
    slotNumber: {
        fontWeight: 700,
        fontSize: '0.85rem',
        letterSpacing: '0.05em',
    },
    slotStatus: {
        marginTop: '2px',
        fontWeight: 500,
        textTransform: 'uppercase',
        letterSpacing: '0.06em',
    },
    parkingLine: {
        position: 'absolute',
        left: 0,
        right: 0,
        height: '2px',
        background: 'rgba(255,255,255,0.06)',
    },
    footer: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        padding: '1rem 1.5rem',
        borderTop: '1px solid rgba(255,255,255,0.08)',
        flexShrink: 0,
        background: 'rgba(255,255,255,0.02)',
        gap: '1rem',
        flexWrap: 'wrap',
    },
    selectedInfo: {
        color: '#10b981',
        fontWeight: 600,
        fontSize: '0.95rem',
    },
    hintText: {
        color: '#606070',
        fontSize: '0.85rem',
    },
    confirmBtn: {
        background: 'linear-gradient(135deg, #6366f1, #8b5cf6)',
        color: '#fff',
        border: 'none',
        borderRadius: '10px',
        padding: '0.65rem 1.5rem',
        fontWeight: 600,
        fontSize: '0.95rem',
        transition: 'all 0.2s',
    },
    tooltip: {
        position: 'absolute',
        top: 'calc(100% + 8px)',
        left: '50%',
        transform: 'translateX(-50%)',
        background: 'rgba(15, 15, 25, 0.97)',
        border: '1px solid rgba(255,255,255,0.12)',
        borderRadius: '10px',
        padding: '0.6rem 0.8rem',
        zIndex: 99999,
        minWidth: '200px',
        maxWidth: '260px',
        pointerEvents: 'none',
        boxShadow: '0 8px 24px rgba(0,0,0,0.6)',
        animation: 'fadeInTooltip 0.15s ease',
    },
    tooltipTitle: {
        fontSize: '0.75rem',
        fontWeight: 700,
        color: '#a0a0c0',
        textTransform: 'uppercase',
        letterSpacing: '0.07em',
        marginBottom: '0.4rem',
    },
    tooltipReservation: {
        display: 'flex',
        alignItems: 'flex-start',
        gap: '0.35rem',
        marginBottom: '0.3rem',
    },
    tooltipIcon: {
        fontSize: '0.7rem',
        flexShrink: 0,
        marginTop: '1px',
    },
    tooltipRange: {
        fontSize: '0.72rem',
        color: '#e2e2f0',
        lineHeight: 1.4,
    },
    tooltipArrow: {
        color: '#6366f1',
        fontWeight: 600,
    },
    tooltipFree: {
        fontSize: '0.75rem',
        color: '#10b981',
        fontWeight: 600,
    },
    tooltipArrowEl: {
        position: 'absolute',
        top: '-5px',
        left: '50%',
        transform: 'translateX(-50%) rotate(45deg)',
        width: '8px',
        height: '8px',
        background: 'rgba(15,15,25,0.97)',
        border: '1px solid rgba(255,255,255,0.12)',
        borderBottom: 'none',
        borderRight: 'none',
    },
};
