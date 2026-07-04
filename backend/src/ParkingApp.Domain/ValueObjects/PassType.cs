using ParkingApp.Domain.Enums;

namespace ParkingApp.Domain.ValueObjects;

public sealed record PassType
{
    public PassTypeKind Kind { get; private init; }

    private PassType()
    {
    }

    private PassType(PassTypeKind kind)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), "Unsupported parking pass type.");
        }

        Kind = kind;
    }

    public static PassType Monthly() => new(PassTypeKind.Monthly);
    public static PassType Weekly() => new(PassTypeKind.Weekly);
    public static PassType Corporate() => new(PassTypeKind.Corporate);
    public static PassType From(PassTypeKind kind) => new(kind);

    public bool IsCorporate => Kind == PassTypeKind.Corporate;

    public override string ToString() => Kind.ToString();
}
