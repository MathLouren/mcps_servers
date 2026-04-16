using Microsoft.Extensions.Configuration;

namespace EprodutosAgents.Security;

public enum AccessProfile
{
    Customer,
    Employee
}

public sealed record AccessProfileOptions(AccessProfile Profile)
{
    public static AccessProfileOptions FromConfiguration(IConfiguration configuration)
    {
        var configuredProfile =
            configuration["EPRODUTOS_ACCESS_PROFILE"]
            ?? configuration["AccessProfile"]
            ?? configuration["Access:Profile"]
            ?? "customer";

        if (Enum.TryParse<AccessProfile>(configuredProfile, ignoreCase: true, out var profile))
        {
            return new AccessProfileOptions(profile);
        }

        throw new InvalidOperationException(
            "Perfil de acesso invalido. Use 'customer' ou 'employee' em EPRODUTOS_ACCESS_PROFILE.");
    }
}
