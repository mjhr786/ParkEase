namespace ParkingApp.Domain.ValueObjects;

public record Address
{
    public string Street { get; init; }
    public string City { get; init; }
    public string State { get; init; }
    public string Country { get; init; }
    public string PostalCode { get; init; }
    public double Latitude { get; init; }
    public double Longitude { get; init; }

    public Address(string street, string city, string state, string country, string postalCode, double latitude, double longitude)
    {
        if (string.IsNullOrWhiteSpace(street))
            throw new ArgumentException("Street cannot be empty", nameof(street));
        if (string.IsNullOrWhiteSpace(city))
            throw new ArgumentException("City cannot be empty", nameof(city));
        if (string.IsNullOrWhiteSpace(state))
            throw new ArgumentException("State cannot be empty", nameof(state));
        if (string.IsNullOrWhiteSpace(country))
            throw new ArgumentException("Country cannot be empty", nameof(country));
        if (latitude < -90 || latitude > 90)
            throw new ArgumentException("Latitude must be between -90 and 90", nameof(latitude));
        if (longitude < -180 || longitude > 180)
            throw new ArgumentException("Longitude must be between -180 and 180", nameof(longitude));

        Street = street.Trim();
        City = city.Trim();
        State = state.Trim();
        Country = country.Trim();
        PostalCode = postalCode?.Trim() ?? string.Empty;
        Latitude = latitude;
        Longitude = longitude;
    }

    public string FullAddress => $"{Street}, {City}, {State}, {Country} {PostalCode}".Trim();

    public double DistanceToKm(Address other)
    {
        // Haversine formula for distance calculation
        const double earthRadiusKm = 6371;

        var dLat = DegreesToRadians(other.Latitude - Latitude);
        var dLon = DegreesToRadians(other.Longitude - Longitude);

        var lat1 = DegreesToRadians(Latitude);
        var lat2 = DegreesToRadians(other.Latitude);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2) * Math.Cos(lat1) * Math.Cos(lat2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return earthRadiusKm * c;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;

    public override string ToString() => FullAddress;
}
