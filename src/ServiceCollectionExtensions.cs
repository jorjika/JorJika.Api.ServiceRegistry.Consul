using Consul;
using JorJika.Api.ServiceRegistry.Consul.Models;
using System;
using JorJika.Api.ServiceRegistry.Consul;

#if !NET451
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
#endif

namespace Microsoft.Extensions.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {

#if !NET451
        /// <summary>
        /// Add consul object as service registry client
        /// </summary>
        /// <param name="services">IServiceCollection</param>
        /// <param name="config">Configuration Object mapped to config json file that includes "ConsulConfig" section.</param>
        public static void AddConsulClient(this IServiceCollection services, IConfiguration config)
        {
            services.Configure<ConsulConfig>(config.GetSection("ConsulConfig"));
            ConsulManager.Load(config);
            services.AddSingleton<IConsulClient, ConsulClient>(p => ConsulManager.Consul);
        }

        /// <summary>
        /// Add consul object as service registry client
        /// </summary>
        /// <param name="services">IServiceCollection</param>
        /// <param name="address">Consul address. Defaults to http://127.0.0.1:8500</param>
        /// <param name="token">Consul token if needed.</param>
        public static void AddConsulClient(this IServiceCollection services, string address = "http://127.0.0.1:8500", string token = null)
        {
            ConsulManager.Load(address, token);
            services.AddSingleton<IConsulClient, ConsulClient>(p => ConsulManager.Consul);
        }

#endif

    }
}
