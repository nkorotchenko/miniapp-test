using IdentityModel.Client;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Telegram.Bot.Types;
using WebAppBot.Config;

#pragma warning disable SYSLIB0023

namespace WebAppBot.Services
{
    public class UserInfo
    {
        public Guid Id { get; set; }

        public string FirstName { get; set; } = string.Empty;

        public string LastName { get; set; } = string.Empty;

        public string Patronymic { get; set; } = string.Empty;

        public bool Newbie { get; set; } = true;
    }

    public class AuthUserInfo
    {
        public long? ChatId { get; set; }

        public long? UserId { get; set; }

        public string? UserName { get; set; }

        public string? FirstName { get; set; }

        public string? LastName { get; set; }
    }

    public class AuthRecord
    {
        public AuthUserInfo? UserInfo { get; set; }

        public DateTimeOffset Created { get; set; } = DateTimeOffset.MinValue;

        public DateTimeOffset Expired { get; set; } = DateTimeOffset.MinValue;

        public string? AccessToken { get; set; }

        public string? RefreshToken { get; set; }
    }

    public class LoginRecord
    {
        public AuthUserInfo? UserInfo { get; set; }

        public string? CodeChallenge { get; set; }

        public string? CodeChallengeHash { get; set; }
    }

    public class AuthService
    {
        private readonly AuthConfig _config;

        private ConcurrentDictionary<string, LoginRecord> LoginStorage { get; set; } = new ConcurrentDictionary<string, LoginRecord>();

        private ConcurrentDictionary<string, AuthRecord> AuthStorage { get; set; } = new ConcurrentDictionary<string, AuthRecord>();

        public AuthService(AuthConfig config)
        {
            _config = config;
        }

        private void AddLoginRecord(string state, LoginRecord item)
        {
            LoginStorage.TryAdd(state, item);
        }

        private LoginRecord? GetLoginRecord(string state)
        {
            if (LoginStorage.TryGetValue(state, out var item))
            {
                return item;
            }

            return null;
        }

        private AuthRecord AddAuthRecord(string userName, AuthRecord item)
        {
            AuthStorage.TryAdd(userName, item);
            return item;
        }

        public AuthRecord? GetAuthRecord(string userName)
        {
            if (AuthStorage.TryGetValue(userName, out var item))
            {
                return item;
            }

            return null;
        }

        public string CreateLoginUrl(AuthUserInfo userInfo)
        {
            string nonceValue = Guid.NewGuid().ToString();
            string stateValue = Guid.NewGuid().ToString();
            var code_verifier = CalculateCryptographicRandomString(43);
            var challenge = CalculateSha256(code_verifier);

            AddLoginRecord(stateValue, new LoginRecord
            {
                UserInfo = userInfo,
                CodeChallenge = code_verifier,
                CodeChallengeHash = challenge
            });

            StringBuilder builder = new StringBuilder($"{_config.Authority}/connect/authorize");
            builder.Append("?response_type=code");
            builder.Append($"&client_id={_config.ClientId}");
            builder.Append($"&scope={_config.Scopes}");
            builder.Append($"&code_challenge={challenge}");
            builder.Append("&code_challenge_method=S256");
            builder.Append($"&state={stateValue}");
            builder.Append($"&nonce={nonceValue}");
            builder.Append($"&redirect_uri={_config.RedirectUrl}");

            return builder.ToString();
        }

        public async Task<AuthUserInfo?> ChallengeAsync(string code, string state)
        {
            var item = GetLoginRecord(state);
            if (item != null)
            {
                var httpClient = new HttpClient();
                var response = await httpClient.RequestAuthorizationCodeTokenAsync(new AuthorizationCodeTokenRequest
                {
                    Address = $"{_config.Authority}/connect/token",

                    ClientId = "frontend",
                    Code = code,
                    CodeVerifier = item.CodeChallenge,
                    RedirectUri = _config.RedirectUrl ?? "",
                });

                if (response != null && !response.IsError && item.UserInfo != null)
                {
                    AddAuthRecord(item.UserInfo.UserName ?? "", new AuthRecord
                    {
                        AccessToken = response.AccessToken,
                        RefreshToken = response.RefreshToken,
                        Created = DateTimeOffset.Now,
                        UserInfo = item.UserInfo,
                        Expired = DateTimeOffset.FromUnixTimeSeconds(response.ExpiresIn),
                    });

                    return item.UserInfo;
                }
            }

            return null;
        }

        public async Task<UserInfo?> GetUserInfoAsync(string userName)
        {
            var authInfo = GetAuthRecord(userName);
            if (authInfo != null)
            {
                var httpClient = new HttpClient();
                var info = await httpClient.GetUserInfoAsync(new UserInfoRequest
                {
                    Address = $"{_config.Authority}/connect/userinfo",
                    Token = authInfo.AccessToken,
                });

                Guid.TryParse(info.Claims.SingleOrDefault(x => x.Type == "sub")?.Value, out var userId);

                return new UserInfo
                {
                    Id = userId,
                    FirstName = info.Claims.SingleOrDefault(x => x.Type == "firstname")?.Value ?? String.Empty,
                    LastName = info.Claims.SingleOrDefault(x => x.Type == "lastname")?.Value ?? String.Empty,
                    Patronymic = info.Claims.SingleOrDefault(x => x.Type == "patronymic")?.Value ?? String.Empty,
                    Newbie = Convert.ToBoolean(info.Claims.SingleOrDefault(x => x.Type == "newbie2")?.Value)
                };
            }

            return null;
        }

        private string CalculateSha256(string input)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(input);
                var hash = sha.ComputeHash(bytes);

                return base64urlencode(hash);
            }
        }

        private string base64urlencode(byte[] arg)
        {
            string s = Convert.ToBase64String(arg); // Regular base64 encoder
            s = s.Split('=')[0]; // Remove any trailing '='s
            s = s.Replace('+', '-'); // 62nd char of encoding
            s = s.Replace('/', '_'); // 63rd char of encoding
            return s;
        }

        private string CalculateCryptographicRandomString(int maxSize)
        {
            char[] chars = new char[62];
            chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890".ToCharArray();

            // obsolete
            var crypto = new RNGCryptoServiceProvider();
            var data = new byte[maxSize];
            crypto.GetNonZeroBytes(data);

            StringBuilder result = new StringBuilder(maxSize);
            foreach (byte b in data)
            {
                result.Append(chars[b % (chars.Length - 1)]);
            }
            return result.ToString();
        }
    }
}
