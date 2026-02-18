using System;

namespace Frontend.Services
{
    public class SessionService
    {
        private static readonly Lazy<SessionService> _lazyInstance = new Lazy<SessionService>(() => new SessionService());
        public static SessionService Instance => _lazyInstance.Value;

        private string? _authToken;
        private string? _username; // Plaintext username (may be encrypted on server, this is what we display)
        private string? _userIdentifier; // What the user logged in with (plaintext username or email for lookup)
        private string? _vaultKey;

        public string? AuthToken
        {
            get => _authToken;
            set => _authToken = value;
        }

        public string? Username
        {
            get => _username;
            set => _username = value;
        }

        // VaultPassword is stored in memory ONLY after successful RouteGuard validation
        // Zero-Knowledge v2: This is NEVER stored on server, only in client memory
        public string? VaultPassword
        {
            get => _vaultKey;
            set => _vaultKey = value;
        }

        public string? UserIdentifier
        {
            get => _userIdentifier;
            set => _userIdentifier = value;
        }

        public bool IsLoggedIn => !string.IsNullOrEmpty(_authToken);

        public void Clear()
        {
            _authToken = null;
            _username = null;
            _userIdentifier = null;
            _vaultKey = null;
        }
    }
}
