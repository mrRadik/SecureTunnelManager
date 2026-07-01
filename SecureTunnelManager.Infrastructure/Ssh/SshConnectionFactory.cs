using Renci.SshNet;
using SecureTunnelManager.Core.Models;
using SecureTunnelManager.Core.Services;

namespace SecureTunnelManager.Infrastructure.Ssh;

/// <summary>
/// Builds SSH.NET authentication methods for password or private key.
/// </summary>
internal static class SshConnectionFactory
{
    public static Task<AuthenticationMethod[]> BuildAuthMethodsAsync(
        AuthMethod authMethod,
        string username,
        int? credentialId,
        string? privateKeyPath,
        int? passphraseCredentialId,
        ICredentialService credentialService,
        CancellationToken cancellationToken) =>
        BuildAuthMethodsAsync(
            authMethod,
            username,
            credentialId,
            privateKeyPath,
            passphraseCredentialId,
            inlinePassword: null,
            inlineKeyPassphrase: null,
            credentialService,
            cancellationToken);

    public static async Task<AuthenticationMethod[]> BuildAuthMethodsAsync(
        AuthMethod authMethod,
        string username,
        int? credentialId,
        string? privateKeyPath,
        int? passphraseCredentialId,
        string? inlinePassword,
        string? inlineKeyPassphrase,
        ICredentialService credentialService,
        CancellationToken cancellationToken)
    {
        if (authMethod == AuthMethod.PrivateKey)
        {
            if (string.IsNullOrWhiteSpace(privateKeyPath))
                throw new InvalidOperationException("Private key path is required for key authentication.");

            if (!File.Exists(privateKeyPath))
                throw new FileNotFoundException("Private key file not found.", privateKeyPath);

            var passphrase = inlineKeyPassphrase;
            if (string.IsNullOrEmpty(passphrase) && passphraseCredentialId.HasValue)
                passphrase = await credentialService.GetPasswordAsync(passphraseCredentialId.Value, cancellationToken).ConfigureAwait(false);

            var keyFile = string.IsNullOrEmpty(passphrase)
                ? new PrivateKeyFile(privateKeyPath)
                : new PrivateKeyFile(privateKeyPath, passphrase);

            return [new PrivateKeyAuthenticationMethod(username, keyFile)];
        }

        if (!string.IsNullOrEmpty(inlinePassword))
            return [new PasswordAuthenticationMethod(username, inlinePassword)];

        if (!credentialId.HasValue)
            throw new InvalidOperationException("Credential is required for password authentication.");

        var password = await credentialService.GetPasswordAsync(credentialId.Value, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Credential password could not be retrieved. Is the vault unlocked?");

        return [new PasswordAuthenticationMethod(username, password)];
    }
}
