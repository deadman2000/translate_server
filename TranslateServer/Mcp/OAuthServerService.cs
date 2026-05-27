using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace TranslateServer.Mcp
{
    /// <summary>
    /// Minimal built-in OAuth 2.0 Authorization Server for MCP.
    /// Supports Authorization Code Flow + PKCE.
    /// </summary>
    public class OAuthServerService
    {
        private readonly McpOptions _options;
        private readonly ConcurrentDictionary<string, AuthorizationCode> _codes = new();

        public OAuthServerService(IOptions<McpOptions> options)
        {
            _options = options.Value;
        }

        public OAuthClient? GetClient(string clientId)
        {
            if (_options.OAuthClients == null) return null;
            return _options.OAuthClients.FirstOrDefault(c => c.ClientId == clientId && c.Enabled);
        }

        public bool ValidateRedirectUri(OAuthClient client, string redirectUri)
        {
            if (client.RedirectUris == null || client.RedirectUris.Count == 0)
                return false;

            return client.RedirectUris.Any(uri => 
                string.Equals(uri.TrimEnd('/'), redirectUri.TrimEnd('/'), StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Creates a new authorization code after user consent.
        /// </summary>
        public string CreateAuthorizationCode(string clientId, string redirectUri, string? codeChallenge, string? codeChallengeMethod, string userId, string agentName)
        {
            var code = Guid.NewGuid().ToString("N");

            var authCode = new AuthorizationCode
            {
                Code = code,
                ClientId = clientId,
                RedirectUri = redirectUri,
                CodeChallenge = codeChallenge,
                CodeChallengeMethod = codeChallengeMethod ?? "S256",
                UserId = userId,
                AgentName = agentName,
                ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                Used = false
            };

            _codes[code] = authCode;

            // Cleanup expired codes
            CleanupExpiredCodes();

            return code;
        }

        public AuthorizationCode? ConsumeAuthorizationCode(string code, string redirectUri, string? codeVerifier)
        {
            if (!_codes.TryRemove(code, out var authCode))
                return null;

            if (authCode.Used || authCode.ExpiresAt < DateTime.UtcNow)
                return null;

            if (!string.Equals(authCode.RedirectUri, redirectUri, StringComparison.Ordinal))
                return null;

            // PKCE validation
            if (!string.IsNullOrEmpty(authCode.CodeChallenge))
            {
                if (string.IsNullOrEmpty(codeVerifier))
                    return null;

                string computedChallenge;
                if (authCode.CodeChallengeMethod == "S256")
                {
                    using var sha256 = SHA256.Create();
                    var hash = sha256.ComputeHash(Encoding.ASCII.GetBytes(codeVerifier));
                    computedChallenge = Base64UrlEncode(hash);
                }
                else
                {
                    computedChallenge = codeVerifier; // plain
                }

                if (!string.Equals(computedChallenge, authCode.CodeChallenge, StringComparison.Ordinal))
                    return null;
            }

            authCode.Used = true;
            return authCode;
        }

        /// <summary>
        /// Issues a JWT access token for a successfully exchanged authorization code.
        /// </summary>
        public string IssueAccessToken(AuthorizationCode authCode, int expiresInMinutes = 60)
        {
            var jwtOptions = _options.Jwt ?? new McpJwtOptions();

            var key = GetSigningKey();
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, authCode.UserId),
                new Claim("client_id", authCode.ClientId),
                new Claim("name", authCode.AgentName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("scope", "mcp:access")
            };

            var token = new JwtSecurityToken(
                issuer: jwtOptions.Issuer ?? "https://translate-server.local",
                audience: jwtOptions.Audience ?? "mcp",
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expiresInMinutes),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private SymmetricSecurityKey GetSigningKey()
        {
            // Use a stable key from configuration or generate a dev key
            var keyMaterial = _options.Jwt?.SigningKey;

            if (string.IsNullOrEmpty(keyMaterial))
            {
                // Development fallback - in production this should come from config
                keyMaterial = "ThisIsADevelopmentSigningKeyForMCPMustBeAtLeast32CharsLong123!";
            }

            return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyMaterial));
        }

        private static string Base64UrlEncode(byte[] input)
        {
            return Convert.ToBase64String(input)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }

        private void CleanupExpiredCodes()
        {
            var now = DateTime.UtcNow;
            var expired = _codes.Where(kvp => kvp.Value.ExpiresAt < now).Select(kvp => kvp.Key).ToList();
            foreach (var key in expired)
            {
                _codes.TryRemove(key, out _);
            }
        }
    }

    public class AuthorizationCode
    {
        public string Code { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string RedirectUri { get; set; } = string.Empty;
        public string? CodeChallenge { get; set; }
        public string CodeChallengeMethod { get; set; } = "S256";
        public string UserId { get; set; } = string.Empty;
        public string AgentName { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public bool Used { get; set; }
    }
}
