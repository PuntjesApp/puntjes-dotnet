using Puntjes.Sdk.Generated;
using Puntjes.Sdk.Generated.Customers.Item.WalletPass;

namespace Puntjes.Sdk;

/// <summary>Downloads a customer's wallet pass file. The generated client reads only the JSON answer of this call.</summary>
public static class WalletPass
{
    public static async Task<byte[]> AppleAsync(PuntjesClient client, string customer, CancellationToken cancellationToken = default)
    {
        var request = client.Customers[customer].WalletPass.ToGetRequestInformation(c => c.QueryParameters.Platform = GetPlatformQueryParameterType.Apple);
        request.Headers.Remove("Accept");
        request.Headers.Add("Accept", "application/vnd.apple.pkpass");

        await using var stream = await client.Adapter.SendPrimitiveAsync<Stream>(request, cancellationToken: cancellationToken);
        if (stream is null)
        {
            return [];
        }

        using var bytes = new MemoryStream();
        await stream.CopyToAsync(bytes, cancellationToken);

        return bytes.ToArray();
    }
}
