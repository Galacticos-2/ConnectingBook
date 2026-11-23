using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Cryptography;

namespace Project_Web_ConnectingBook.Server.Infrastructure
{
    public class JwtSettings(IConfiguration configuration, IHostEnvironment environment)
    {
        public string Issuer { get; } = configuration["Jwt:Issuer"] ?? "ConnectingBook";
        public string Audience { get; } = configuration["Jwt:Audience"] ?? "ConnectingBook.Client";
        public SymmetricSecurityKey Key { get; } = ReadKey(configuration, environment);

        private static SymmetricSecurityKey ReadKey(IConfiguration configuration, IHostEnvironment environment)
        {
            var value = configuration["Jwt:SigningKey"];
            if (string.IsNullOrWhiteSpace(value) && environment.IsDevelopment())
                return new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(64));
            if (string.IsNullOrWhiteSpace(value) || Encoding.UTF8.GetByteCount(value) < 32)
                throw new InvalidOperationException("Cần cung cấp Jwt__SigningKey tối thiểu 32 byte qua môi trường.");
            return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(value));
        }
    }
}
