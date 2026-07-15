using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace BobsBookstoreClassic.Data
{
    public sealed class BookstoreConfiguration
    {
        private static BookstoreConfiguration _instance;
        private static readonly object _lock = new object();

        private readonly Dictionary<string, string> _appSettings = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _connectionStrings = new Dictionary<string, string>();

        private BookstoreConfiguration(IConfiguration configuration)
        {
            // Load app settings from IConfiguration (replaces ConfigurationManager.AppSettings)
            foreach (var kvp in configuration.AsEnumerable())
            {
                if (kvp.Value != null)
                {
                    _appSettings[kvp.Key] = kvp.Value;
                }
            }

            // Environment variables override configuration values
            foreach (var key in _appSettings.Keys.ToList())
            {
                if (Environment.GetEnvironmentVariable(key) != null)
                {
                    _appSettings[key] = Environment.GetEnvironmentVariable(key);
                }
            }

            // Load connection strings from IConfiguration (replaces ConfigurationManager.ConnectionStrings)
            var connectionStringsSection = configuration.GetSection("ConnectionStrings");
            foreach (var child in connectionStringsSection.GetChildren())
            {
                _connectionStrings[child.Key] = child.Value;
            }
        }

        /// <summary>
        /// Initializes the singleton instance with an IConfiguration provided by the host via DI.
        /// Must be called once at application startup before any other usage.
        /// </summary>
        public static void Initialize(IConfiguration configuration)
        {
            lock (_lock)
            {
                _instance = new BookstoreConfiguration(configuration);
            }
        }

        private static BookstoreConfiguration Instance
        {
            get
            {
                if (_instance == null)
                {
                    throw new InvalidOperationException(
                        "BookstoreConfiguration has not been initialized. Call Initialize(IConfiguration) at application startup.");
                }
                return _instance;
            }
        }

        public static void AddSetting(string key, string value)
        {
            Instance._appSettings[key] = value;
        }

        public static string GetSetting(string key)
        {
            Instance._appSettings.TryGetValue(key, out var value);
            return value;
        }

        public static T GetSetting<T>(string key)
        {
            Instance._appSettings.TryGetValue(key, out var value);
            if (value == null) return default;
            return (T)Convert.ChangeType(value, typeof(T));
        }

        public static void AddConnectionString(string key, string value)
        {
            Instance._connectionStrings[key] = value;
        }

        public static string GetConnectionString(string key)
        {
            Instance._connectionStrings.TryGetValue(key, out var value);
            return value;
        }
    }
}
