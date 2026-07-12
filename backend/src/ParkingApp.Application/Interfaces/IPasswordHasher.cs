namespace ParkingApp.Application.Interfaces;

/// <summary>
/// Application port for password hashing/verification.
/// Implemented in Infrastructure (e.g. BCrypt).
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string passwordHash);
}
