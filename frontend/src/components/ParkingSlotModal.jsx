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
                        <span style={{ ...styles.legendDot, background: 'var(--color-success)' }} /> Available
                    </span>
                    <span style={styles.legendItem}>
                        <span style={{ ...styles.legendDot, background: 'var(--color-error)' }} /> Booked
                    </span>
                    <span style={styles.legendItem}>
                        <span style={{ ...styles.legendDot, background: 'var(--color-primary)', boxShadow: '0 0 8px var(--color-primary)' }} /> Selected
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

    let bgColor = 'color-mix(in srgb, var(--color-success) 18%, var(--color-bg-primary))';
    let borderColor = 'var(--color-success)';
    let textColor = 'var(--color-success)';
    let cursor = 'pointer';

    if (isBooked) {
        bgColor = 'color-mix(in srgb, var(--color-error) 18%, var(--color-bg-primary))';
        borderColor = 'var(--color-error)';
        textColor = 'var(--color-error)';
        cursor = 'not-allowed';
    }
    if (isSelected) {
        bgColor = 'var(--color-primary-alpha)';
        borderColor = 'var(--color-primary)';
        textColor = 'var(--color-accent-light)';
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
        background: 'var(--overlay-bg)',
        backdropFilter: 'blur(6px)',
        zIndex: 9999,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        padding: '1rem',
        animation: 'fadeInOverlay 0.2s ease',
    },
    modal: {
        background: 'var(--gradient-card)',
        border: '1px solid var(--color-border)',
        borderRadius: '20px',
        width: '100%',
        maxWidth: '700px',
        maxHeight: '90vh',
        display: 'flex',
        flexDirection: 'column',
        boxShadow: 'var(--shadow-lg)',
        overflow: 'hidden',
    },
    header: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'flex-start',
        padding: '1.5rem 1.5rem 1rem',
        borderBottom: '1px solid var(--color-border)',
        flexShrink: 0,
    },
    title: {
        fontSize: '1.3rem',
        fontWeight: 700,
        color: 'var(--color-text-primary)',
        margin: 0,
    },
    subtitle: {
        fontSize: '0.85rem',
        color: 'var(--color-text-secondary)',
        marginTop: '0.25rem',
    },
    closeBtn: {
        background: 'var(--color-bg-glass)',
        border: '1px solid var(--control-border)',
        color: 'var(--color-text-secondary)',
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
        background: 'var(--color-surface-elevated)',
        flexShrink: 0,
        flexWrap: 'wrap',
    },
    legendItem: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.5rem',
        fontSize: '0.8rem',
        color: 'var(--color-text-secondary)',
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
        background: 'var(--color-bg-primary)',
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
        color: 'var(--color-warning)',
        fontWeight: 700,
        letterSpacing: '0.05em',
        padding: '0.2rem 0.5rem',
        background: 'color-mix(in srgb, var(--color-warning) 12%, transparent)',
        borderRadius: '4px',
        border: '1px solid color-mix(in srgb, var(--color-warning) 35%, transparent)',
    },
    road: {
        flex: 1,
        height: '2px',
        background: 'repeating-linear-gradient(90deg, var(--color-warning) 0, var(--color-warning) 10px, transparent 10px, transparent 20px)',
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
        background: 'var(--color-bg-tertiary)',
        borderRadius: '4px',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        position: 'relative',
    },
    aisleArrow: {
        color: 'var(--color-warning)',
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
        background: 'var(--color-border)',
    },
    footer: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        padding: '1rem 1.5rem',
        borderTop: '1px solid var(--color-border)',
        flexShrink: 0,
        background: 'var(--color-surface-elevated)',
        gap: '1rem',
        flexWrap: 'wrap',
    },
    selectedInfo: {
        color: 'var(--color-success)',
        fontWeight: 600,
        fontSize: '0.95rem',
    },
    hintText: {
        color: 'var(--color-text-muted)',
        fontSize: '0.85rem',
    },
    confirmBtn: {
        background: 'var(--gradient-primary)',
        color: 'var(--color-text-on-accent)',
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
        background: 'var(--dropdown-bg)',
        border: '1px solid var(--dropdown-border)',
        borderRadius: '10px',
        padding: '0.6rem 0.8rem',
        zIndex: 99999,
        minWidth: '200px',
        maxWidth: '260px',
        pointerEvents: 'none',
        boxShadow: 'var(--shadow-md)',
        animation: 'fadeInTooltip 0.15s ease',
    },
    tooltipTitle: {
        fontSize: '0.75rem',
        fontWeight: 700,
        color: 'var(--color-text-secondary)',
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
        color: 'var(--color-text)',
        lineHeight: 1.4,
    },
    tooltipArrow: {
        color: 'var(--color-primary)',
        fontWeight: 600,
    },
    tooltipFree: {
        fontSize: '0.75rem',
        color: 'var(--color-success)',
        fontWeight: 600,
    },
    tooltipArrowEl: {
        position: 'absolute',
        top: '-5px',
        left: '50%',
        transform: 'translateX(-50%) rotate(45deg)',
        width: '8px',
        height: '8px',
        background: 'var(--dropdown-bg)',
        border: '1px solid var(--dropdown-border)',
        borderBottom: 'none',
        borderRight: 'none',
    },
};
