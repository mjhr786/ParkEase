/**
 * BookedSlots - Reusable component to display active reservations for a parking space.
 *
 * Props:
 *  reservations  - array of { startDateTime, endDateTime, slotNumber? }
 *  compact       - bool (default false). When true, uses tighter layout for search cards.
 *  totalSpots    - number (optional). When 1, hides slot header since there is only one spot.
 */
const formatDateTime = (dateStr) => {
    const d = new Date(dateStr);
    return d.toLocaleDateString('en-IN', { day: 'numeric', month: 'short' }) +
        ' ' + d.toLocaleTimeString('en-IN', { hour: '2-digit', minute: '2-digit' });
};

export default function BookedSlots({ reservations, compact = false, totalSpots }) {
    if (!reservations || reservations.length === 0) return null;

    const isSingleSpot = totalSpots === 1;

    const groupedReservations = reservations.reduce((acc, reservation) => {
        const slotKey = Number.isInteger(reservation.slotNumber)
            ? `slot-${reservation.slotNumber}`
            : 'slot-unassigned';

        if (!acc[slotKey]) {
            acc[slotKey] = {
                slotNumber: reservation.slotNumber,
                reservations: []
            };
        }

        acc[slotKey].reservations.push(reservation);
        return acc;
    }, {});

    const sortedGroups = Object.values(groupedReservations).sort((a, b) => {
        if (!Number.isInteger(a.slotNumber)) return 1;
        if (!Number.isInteger(b.slotNumber)) return -1;
        return a.slotNumber - b.slotNumber;
    });

    // Determine whether to show the slot label header
    const showSlotLabel = !isSingleSpot;

    if (compact) {
        return (
            <div style={{
                marginTop: '0.75rem',
                padding: '0.5rem',
                background: 'rgba(99, 102, 241, 0.1)',
                borderRadius: '6px',
                fontSize: '0.75rem',
                border: '1px solid rgba(99, 102, 241, 0.2)'
            }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '0.25rem', marginBottom: '0.25rem' }}>
                    <span>📅</span>
                    <strong style={{ color: 'var(--color-primary)' }}>Booked Slots:</strong>
                </div>
                <div style={{
                    display: 'flex',
                    flexDirection: 'column',
                    gap: '4px',
                    maxHeight: '80px',
                    overflowY: 'auto',
                    paddingRight: '4px'
                }} className="custom-scrollbar">
                    {sortedGroups.map((group) => (
                        <div key={group.slotNumber ?? 'unassigned'} style={{
                            color: 'var(--color-text-muted)',
                            padding: '4px',
                            background: 'rgba(255, 255, 255, 0.05)',
                            borderRadius: '4px'
                        }}>
                            {showSlotLabel && (
                                <div style={{ fontWeight: 600, marginBottom: '2px' }}>
                                    {Number.isInteger(group.slotNumber) ? `Slot ${group.slotNumber}` : 'Slot not assigned'}
                                </div>
                            )}
                            {group.reservations.map((res, idx) => (
                                <div key={idx}>
                                    {formatDateTime(res.startDateTime)} - {formatDateTime(res.endDateTime)}
                                    {res.userName && <span style={{ marginLeft: '4px', opacity: 0.8 }}>({res.userName})</span>}
                                </div>
                            ))}
                        </div>
                    ))}
                </div>
            </div>
        );
    }

    return (
        <div className="card mt-2">
            <div style={{
                padding: '0.75rem',
                background: 'rgba(99, 102, 241, 0.1)',
                borderRadius: '8px',
                border: '1px solid rgba(99, 102, 241, 0.2)',
                fontSize: '0.85rem'
            }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.75rem' }}>
                    <span>📅</span>
                    <strong style={{ color: 'var(--color-primary)' }}>Booked Slots:</strong>
                </div>
                <div style={{
                    display: 'flex',
                    flexDirection: 'column',
                    gap: '6px',
                    maxHeight: '200px',
                    overflowY: 'auto',
                    paddingRight: '6px'
                }} className="custom-scrollbar">
                    {sortedGroups.map((group) => (
                        <div key={group.slotNumber ?? 'unassigned'} style={{
                            color: 'var(--color-text-muted)',
                            padding: '8px 10px',
                            background: 'rgba(255, 255, 255, 0.05)',
                            borderRadius: '6px',
                            border: '1px solid rgba(255, 255, 255, 0.05)'
                        }}>
                            {showSlotLabel && (
                                <div style={{ color: 'var(--color-text)', fontWeight: 600, marginBottom: '6px' }}>
                                    {Number.isInteger(group.slotNumber) ? `Slot ${group.slotNumber}` : 'Slot not assigned'}
                                </div>
                            )}
                            {group.reservations.map((res, idx) => (
                                <div
                                    key={idx}
                                    style={{
                                        display: 'flex',
                                        alignItems: 'center',
                                        gap: '0.5rem',
                                        marginBottom: idx === group.reservations.length - 1 ? 0 : '4px'
                                    }}
                                >
                                    <span>🔒</span>
                                    <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', flexWrap: 'wrap' }}>
                                        <span>{formatDateTime(res.startDateTime)}</span>
                                        <span style={{ opacity: 0.5 }}>→</span>
                                        <span>{formatDateTime(res.endDateTime)}</span>
                                        {res.userName && (
                                            <span style={{ marginLeft: '0.25rem', color: 'rgba(255, 255, 255, 0.7)', fontSize: '0.85em', fontStyle: 'italic' }}>
                                                • {res.userName}
                                            </span>
                                        )}
                                    </div>
                                </div>
                            ))}
                        </div>
                    ))}
                </div>
                <p style={{ marginTop: '0.75rem', fontSize: '0.8rem', color: 'var(--color-text-muted)', fontStyle: 'italic', marginBottom: 0 }}>
                    * These slots are already booked for the shown periods. Pick a different slot or time.
                </p>
            </div>
        </div>
    );
}

