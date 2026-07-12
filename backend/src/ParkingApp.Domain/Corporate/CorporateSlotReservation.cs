using ParkingApp.Domain.Enums;

namespace ParkingApp.Domain.Corporate;

public sealed record CorporateSlotReservation(CorporateSlotType SlotType, int? SlotNumber)
{
    public static CorporateSlotReservation Fixed(int slotNumber) => new(CorporateSlotType.Fixed, slotNumber);

    public static CorporateSlotReservation Shared(int slotNumber) => new(CorporateSlotType.Shared, slotNumber);
}
