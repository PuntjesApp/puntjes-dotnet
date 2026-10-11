using Puntjes.Sdk;
using Puntjes.Sdk.Exceptions;
using Puntjes.Sdk.Generated.Models;

var card = args.Length > 0 ? args[0] : throw new ArgumentException("Usage: dotnet run -- <customer email or card number> [amount in cents]");
var amount = args.Length > 1 ? int.Parse(args[1]) : 4200;

var puntjes = PuntjesClientFactory.Create(
    Environment.GetEnvironmentVariable("PUNTJES_CLIENT_ID")!,
    Environment.GetEnvironmentVariable("PUNTJES_CLIENT_SECRET")!,
    new PuntjesOptions { BaseUrl = Environment.GetEnvironmentVariable("PUNTJES_BASE_URL") });

try
{
    var customer = (await puntjes.Customers.Lookup.GetAsync(c => c.QueryParameters.Identifier = card))?.Data;
    Console.WriteLine($"Customer {customer?.Id} has {customer?.WalletBalance} points.");

    var transaction = (await puntjes.Transactions.PostAsync(new CreateTransactionData
    {
        Identifier = card,
        TotalAmount = amount,
        IdempotencyKey = IdempotencyKey.New(),
    }))?.Data;
    Console.WriteLine($"Transaction {transaction?.Id} earned {transaction?.PointsEarned} points.");
}
catch (PuntjesException e)
{
    Console.Error.WriteLine($"{e.GetType().Name} {e.Status} {e.RawCode}: {e.Message} (request {e.RequestId})");
    return 1;
}

return 0;
