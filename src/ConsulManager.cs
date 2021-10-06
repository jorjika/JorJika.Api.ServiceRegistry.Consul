using Consul;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#if !NET451
using Microsoft.Extensions.Configuration;
#endif

namespace JorJika.Api.ServiceRegistry.Consul
{
    public class ConsulManager
    {
        public static ConsulClient Consul { get; private set; }

#if !NET451
        public static void Load(IConfiguration config)
        {
            var address = config["ConsulConfig:Address"];
            var token = config["ConsulConfig:Token"];

            Load(address, token);
        }
#endif

        public static void Load(string address = "http://127.0.0.1:8500", string token = null)
        {
            if (address == null)
                throw new ConsulConfigurationException("Address is not provided. Check config file.");

            Uri tempUri;
            if (!Uri.TryCreate(address, UriKind.Absolute, out tempUri))
                throw new UriFormatException("");

            if (Consul == null)
            {
                Consul = new ConsulClient(consulConfig =>
                {
                    consulConfig.Address = new Uri(address);
                    consulConfig.Token = token;
                });
            }
            else
            {
                Consul.Config.Address = new Uri(address);
                Consul.Config.Token = token;
            }

        }

        public static string GetValueByKey(string key) => GetValueByKeyAsync(key).GetAwaiter().GetResult();
        public static string GetValueByKeyOrNull(string key) => GetValueByKeyOrNullAsync(key).GetAwaiter().GetResult();
        public static Dictionary<string, string> GetKeyValueList(string prefix = "") => GetKeyValueListAsync(prefix).GetAwaiter().GetResult();
        public static bool KVSave(string key, string value) => KVSaveAsync(key, value).GetAwaiter().GetResult();

        public static Task<string> GetValueByKeyAsync(string key)
        {
            var result = GetValueByKeyOrNullAsync(key);
            if (result == null) throw new KeyNotFoundException($"Key {key} not found");

            return result;
        }
        public static async Task<string> GetValueByKeyOrNullAsync(string key)
        {
            QueryResult<KVPair> result = null;

            try
            {
                result = await Consul.KV.Get(key);
            }
            catch (ConsulRequestException ex)
            {
                throw new Exception($"Consul: {ex.Message}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Consul: {ex.Message}");
            }

            var response = result.Response;

            if (response == null) return null;

            return Encoding.UTF8.GetString(response.Value);
        }
        public static async Task<Dictionary<string, string>> GetKeyValueListAsync(string prefix = "")
        {
            var result = await Consul.KV.List(prefix);
            var response = result.Response?.Where(kv => kv.Value != null) ?? null;

            if (response == null) throw new Exception($"Keys not found");

            return response.ToDictionary(kv => kv.Key, kv => Encoding.UTF8.GetString(kv.Value).ToString(), StringComparer.OrdinalIgnoreCase);
        }
        public static async Task<bool> KVSaveAsync(string key, string value)
        {
            bool result = false;

            var queryResult = await Consul.KV.Get(key);

            var currentkv = queryResult.Response;

            if (currentkv != null)
            {
                currentkv.Value = Encoding.UTF8.GetBytes(value);
                var writeResult = await Consul.KV.Put(currentkv);
                result = writeResult.Response;
            }
            else
            {
                var kv = new KVPair(key);
                kv.Value = Encoding.UTF8.GetBytes(value);
                var writeResult = await Consul.KV.Put(kv);
                result = writeResult.Response;
            }

            return result;
        }
    }
}
