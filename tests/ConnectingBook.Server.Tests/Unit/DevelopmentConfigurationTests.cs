using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Project_Web_ConnectingBook.Server.Infrastructure;

namespace ConnectingBook.Server.Tests.Unit
{
    public class DevelopmentConfigurationTests
    {
        private class TestEnvironment(string name) : IHostEnvironment
        {
            public string EnvironmentName { get; set; } = name;
            public string ApplicationName { get; set; } = "ConnectingBook.Tests";
            public string ContentRootPath { get; set; } = ".";
            public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        }

        [Fact]
        public void Missing_development_key_is_random_in_memory_and_not_written_to_configuration()
        {
            var config = new ConfigurationBuilder().Build();
            var environment = new TestEnvironment(Environments.Development);
            var first = new JwtSettings(config, environment);
            var second = new JwtSettings(config, environment);
            Assert.True(first.Key.KeySize >= 256);
            Assert.False(first.Key.Key.SequenceEqual(second.Key.Key));
            Assert.Null(config["Jwt:SigningKey"]);
        }

        [Fact]
        public void Production_requires_an_explicit_signing_key()
        {
            var config = new ConfigurationBuilder().Build();
            Assert.Throws<InvalidOperationException>(() => new JwtSettings(config, new TestEnvironment(Environments.Production)));
        }

        [Fact]
        public void Explicit_short_key_is_rejected_even_in_development()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:SigningKey"] = "invalid-short-test-value" }).Build();
            Assert.Throws<InvalidOperationException>(() => new JwtSettings(config, new TestEnvironment(Environments.Development)));
        }

        [Fact]
        public void Local_database_default_is_development_only_and_never_contains_a_password()
        {
            var config = new ConfigurationBuilder().Build();
            var connection = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(DevelopmentDefaults.DatabaseConnection(config, new TestEnvironment(Environments.Development)));
            Assert.Equal("ConnectingBookSystemDb", connection.InitialCatalog);
            Assert.True(connection.IntegratedSecurity);
            Assert.Empty(connection.Password);
            Assert.Throws<InvalidOperationException>(() => DevelopmentDefaults.DatabaseConnection(config, new TestEnvironment(Environments.Production)));
        }
    }
}
