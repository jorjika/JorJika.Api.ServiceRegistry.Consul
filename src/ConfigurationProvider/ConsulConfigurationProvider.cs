#if !NET451
using Microsoft.Extensions.Configuration;
#endif
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Extensions.Configuration
{
#if !NET451
    public class ConsulConfigurationProvider : Microsoft.Extensions.Configuration.ConfigurationProvider
    {
        private const string ConsulIndexHeader = "X-Consul-Index";

        private readonly string _path;
        private readonly HttpClient _httpClient;
        private readonly IReadOnlyList<Uri> _consulUrls;
        private readonly Task _configurationListeningTask;
        private int _consulUrlIndex;
        private int _failureCount;
        private int _consulConfigurationIndex;
        private readonly string _token;

        public ConsulConfigurationProvider(IEnumerable<Uri> consulUrls, string path, string token)
        {
            _path = path;
            _token = token;
            _consulUrls = consulUrls.Select(u => new Uri(u, $"v1/kv/{path}")).ToList();

            if (_consulUrls.Count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(consulUrls));
            }

            _httpClient = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip }, true);
            _httpClient.DefaultRequestHeaders.Add("X-Consul-Token", token);

            _configurationListeningTask = new Task(ListenToConfigurationChanges);
        }

        public override void Load() => LoadAsync().ConfigureAwait(false).GetAwaiter().GetResult();

        private async Task LoadAsync()
        {
            try { Data = await ExecuteQueryAsync(); }
            catch { Data = new Dictionary<string, string>(); }

            if (_configurationListeningTask.Status == TaskStatus.Created)
                _configurationListeningTask.Start();
        }

        private async void ListenToConfigurationChanges()
        {
            while (true)
            {
                try
                {
                    if (_failureCount > _consulUrls.Count)
                    {
                        _failureCount = 0;
                        await Task.Delay(TimeSpan.FromMinutes(1));
                    }

                    Data = await ExecuteQueryAsync(true);
                    OnReload();
                    _failureCount = 0;
                }
                catch (TaskCanceledException)
                {
                    _failureCount = 0;
                }
                catch
                {
                    _consulUrlIndex = (_consulUrlIndex + 1) % _consulUrls.Count;
                    _failureCount++;
                }
            }
        }

        private async Task<IDictionary<string, string>> ExecuteQueryAsync(bool isBlocking = false)
        {
            var requestUri = isBlocking ? $"?recurse=true&index={_consulConfigurationIndex}" : "?recurse=true";
            using (var request = new HttpRequestMessage(HttpMethod.Get, new Uri(_consulUrls[_consulUrlIndex], requestUri)))
            using (var response = await _httpClient.SendAsync(request))
            {
                response.EnsureSuccessStatusCode();
                if (response.Headers.Contains(ConsulIndexHeader))
                {
                    var indexValue = response.Headers.GetValues(ConsulIndexHeader).FirstOrDefault();
                    int.TryParse(indexValue, out _consulConfigurationIndex);
                }

                var tokens = JToken.Parse(await response.Content.ReadAsStringAsync());
                return tokens
                    .Select(k => new KeyValuePair<string, string>
                        (
                            k.Value<string>("Key").Substring(_path.Length + 1),
                            k.Value<string>("Value") != null ? Encoding.UTF8.GetString(Convert.FromBase64String(k.Value<string>("Value"))) : null
                        ))
                    .Where(v => !string.IsNullOrWhiteSpace(v.Key))
                    //.SelectMany(Flatten)
                    .ToDictionary(v => ConfigurationPath.Combine(v.Key.Split('/')), v => v.Value, StringComparer.OrdinalIgnoreCase);
            }
        }

        private static IEnumerable<KeyValuePair<string, string>> Flatten(KeyValuePair<string, JToken> tuple)
        {
            if (!(tuple.Value is JObject value))
                yield break;

            foreach (var property in value)
            {
                var propertyKey = $"{tuple.Key}/{property.Key}";
                switch (property.Value.Type)
                {
                    case JTokenType.Object:
                        foreach (var item in Flatten(new KeyValuePair<string, JToken>(propertyKey, property.Value)))
                            yield return item;
                        break;
                    case JTokenType.Array:
                        break;
                    default:
                        yield return new KeyValuePair<string, string>(propertyKey, property.Value.Value<string>());
                        break;
                }
            }
        }
    }
#endif
}
