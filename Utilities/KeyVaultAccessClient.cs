using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

namespace MobileAutomation.Tests.Utilities // Adjust to match your project's namespace
{
    public static class KeyVaultAccessClient
    {
        /// <summary>
        /// Retrieves a plain-text secret string value from a specific Azure Key Vault.
        /// </summary>
        /// <param name="secretName">The exact identifier/name of your secret inside Azure</param>
        /// <param name="keyVaultName">The target Key Vault name (e.g., "digitalshared")</param>
        public static async Task<string> GetKeyVaultSecretAsync(string secretName, string keyVaultName)
        {
            if (string.IsNullOrWhiteSpace(secretName))
                throw new ArgumentException("Secret name cannot be null or empty.", nameof(secretName));
                
            if (string.IsNullOrWhiteSpace(keyVaultName))
                throw new ArgumentException("Key Vault name cannot be null or empty.", nameof(keyVaultName));

            // Dynamically constructs the vault URI endpoint
            string kvUri = $"https://{keyVaultName}.vault.azure.net";

            try
            {
                // DefaultAzureCredential automatically switches context between local development IDE logins 
                // and Azure DevOps Service Principals / Managed Identities managed by pipelines.
                var client = new SecretClient(new Uri(kvUri), new DefaultAzureCredential());

                // Asynchronously request the core secret bundle object from Azure
                KeyVaultSecret secret = await client.GetSecretAsync(secretName);

                // Extract and return the inner string content
                return secret.Value;
            }
            catch (Exception ex)
            {
                // Wrap the exception to help troubleshoot structural permission or path naming constraints
                throw new InvalidOperationException(
                    $"Failed to fetch secret '{secretName}' from vault '{keyVaultName}'. Internal details: {ex.Message}", ex);
            }
        }
    }
}
