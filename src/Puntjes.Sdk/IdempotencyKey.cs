namespace Puntjes.Sdk;

/// <summary>Makes the idempotency_key a points-moving call needs. Send the same key again to repeat the call safely.</summary>
public static class IdempotencyKey
{
    public static string New() => Guid.NewGuid().ToString("D");
}
