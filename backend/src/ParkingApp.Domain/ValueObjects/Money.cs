namespace ParkingApp.Domain.ValueObjects;

public record Money
{
    public decimal Amount { get; init; }
    public string Currency { get; init; }

    public Money(decimal amount, string currency = "INR")
    {
        if (amount < 0)
            throw new ArgumentException("Amount cannot be negative", nameof(amount));
        
        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency cannot be empty", nameof(currency));

        Amount = amount;
        Currency = currency.ToUpperInvariant();
    }

    public static Money Zero(string currency = "INR") => new(0, currency);

    public static Money operator +(Money left, Money right)
    {
        if (left.Currency != right.Currency)
            throw new InvalidOperationException($"Cannot add money with different currencies: {left.Currency} and {right.Currency}");
        
        return new Money(left.Amount + right.Amount, left.Currency);
    }

    public static Money operator -(Money left, Money right)
    {
        if (left.Currency != right.Currency)
            throw new InvalidOperationException($"Cannot subtract money with different currencies: {left.Currency} and {right.Currency}");
        
        return new Money(left.Amount - right.Amount, left.Currency);
    }

    public static Money operator *(Money money, decimal multiplier)
    {
        return new Money(money.Amount * multiplier, money.Currency);
    }

    public static bool operator >(Money left, Money right)
    {
        if (left.Currency != right.Currency)
            throw new InvalidOperationException($"Cannot compare money with different currencies: {left.Currency} and {right.Currency}");
        
        return left.Amount > right.Amount;
    }

    public static bool operator <(Money left, Money right)
    {
        if (left.Currency != right.Currency)
            throw new InvalidOperationException($"Cannot compare money with different currencies: {left.Currency} and {right.Currency}");
        
        return left.Amount < right.Amount;
    }

    public static bool operator >=(Money left, Money right) => !(left < right);
    public static bool operator <=(Money left, Money right) => !(left > right);

    public override string ToString() => $"{Currency} {Amount:N2}";
}
