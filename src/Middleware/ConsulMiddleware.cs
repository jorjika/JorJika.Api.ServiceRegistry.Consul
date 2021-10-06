using Consul;
using JorJika.Api.ServiceRegistry.Consul.Helpers;
using JorJika.Api.ServiceRegistry.Consul.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using JorJika.Api.ServiceRegistry.Consul;

#if !NET451
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
#endif


namespace Microsoft.AspNetCore.Builder
{

    public static class ConsulMiddleware
    {

#if !NET451
        private static string localIp { get; set; }

        /// <summary>
        /// Registers service to consul service registry. Addes /HealthCheck and /LocalInstanceInfo. If you are using response wrapper better to exclude these URLs.
        /// </summary>
        /// <param name="app">IApplicationBuilder</param>
        /// <param name="lifetime">This object is used to register ApplicationStop event and deregister service from consul</param>
        /// <param name="healthCheckUrl">Health Check url, if you write "check" url will be http://yourservice/check. Default value is "HealthCheck"</param>
        /// <param name="instanceInfoUrl">Instance Information url, if you write "instanceinfo" url will be http://yourservice/instanceinfo. Default value is "Info"</param>
        /// <param name="deregisterInactiveServiceAfterMinutes">When service goes down unexpectedly and cant deregister itself, consul will deregister service after given minutes. Default is 90 minutes.</param>
        /// <param name="checks">Custom Checks for your service</param>
        /// <param name="ApiName">Api name</param>
        /// <param name="additionalTags">Additional tags to add in consul</param>
        public static void UseConsul(this IApplicationBuilder app, IApplicationLifetime lifetime, string healthCheckUrl = "HealthCheck", string instanceInfoUrl = "Info", int deregisterInactiveServiceAfterMinutes = 90, IEnumerable<AgentServiceCheck> checks = null, string ApiName = null, string[] additionalTags = null, string ApiDomain = null, string OverrideServerIPAddress = null)
        {
            app.Map($"/{healthCheckUrl}", a =>
            {
                a.Run(async context =>
                {
                    await context.Response.WriteAsync("OK");
                });
            });


            // Retrieve Consul client from DI
            var consulClient = ConsulManager.Consul;
            var consulConfigOptions = app.ApplicationServices.GetRequiredService<IOptions<ConsulConfig>>();
            ConsulConfig consulConfig = new ConsulConfig();


            if (consulConfigOptions?.Value?.Address != null)
                consulConfig = consulConfigOptions.Value;
            else
            {
                consulConfig.ApiName = ApiName;
                consulConfig.ApiDomain = ApiDomain;

                var consulTags = new List<string>();
                consulTags.Add(consulConfig.ApiName.ToLower());

                if (string.IsNullOrWhiteSpace(ApiDomain))
                    consulTags.Add($"urlprefix-/{consulConfig.ApiName.ToLower()} strip=/{consulConfig.ApiName.ToLower()}");
                else
                    consulTags.Add($"urlprefix-{consulConfig.ApiDomain.ToLower()}/");

                if ((additionalTags?.Any() ?? false) == true)
                    consulTags.AddRange(additionalTags.AsEnumerable());

                consulConfig.Tags = consulTags.ToArray();
            }


            // Setup logger
            var loggingFactory = app.ApplicationServices
                            .GetRequiredService<ILoggerFactory>();
            var logger = loggingFactory.CreateLogger<IApplicationBuilder>();

            // Get server IP address
            var features = app.Properties["server.Features"] as FeatureCollection;
            var addresses = features.Get<IServerAddressesFeature>();
            var address = addresses.Addresses.First();

            string serverIp = "127.0.0.1";
            var serverIPEnvVariable = Environment.GetEnvironmentVariable("_ServerIpAddress");
            if (!string.IsNullOrWhiteSpace(serverIPEnvVariable) && IPAddress.TryParse(serverIPEnvVariable, out var ip))
                serverIp = serverIPEnvVariable;

            // Register service with consul
            var uri = new Uri(address.Replace("*", serverIp));

            //Instance Info Check
            app.Map($"/{instanceInfoUrl}", a =>
            {
                a.Run(async context =>
                {
                    if (string.IsNullOrWhiteSpace(localIp))
                        try { localIp = IPHelper.GetLocalIp(); }
                        catch { localIp = "Undefined"; }


                    var result = $"{Environment.NewLine}";
                    result += $"Host: {Environment.MachineName}{Environment.NewLine}";
                    result += $"Binded Host: {(string.IsNullOrWhiteSpace(OverrideServerIPAddress) ? uri.Host : OverrideServerIPAddress)}{Environment.NewLine}";
                    result += $"LocalIp: {localIp}";

                    await context.Response.WriteAsync(result);
                });
            });


            var agentChecks = new List<AgentServiceCheck>();

            agentChecks.Add(
                new AgentServiceCheck()
                {
                    HTTP = $"{uri.Scheme}://{(string.IsNullOrWhiteSpace(OverrideServerIPAddress) ? uri.Host : OverrideServerIPAddress)}:{uri.Port}/{healthCheckUrl}",
                    Interval = TimeSpan.FromSeconds(5),
                    DeregisterCriticalServiceAfter = TimeSpan.FromMinutes(deregisterInactiveServiceAfterMinutes)
                });

            //Adding Custom Checks
            if ((checks?.Any() ?? false))
            {
                foreach (var check in checks)
                {
                    check.HTTP = check.HTTP.Replace("[BASE_URL]", $"{uri.Scheme}://{(string.IsNullOrWhiteSpace(OverrideServerIPAddress) ? uri.Host : OverrideServerIPAddress)}:{uri.Port}");
                    if (check.Interval == null)
                        check.Interval = TimeSpan.FromSeconds(5);

                    if (check.DeregisterCriticalServiceAfter == null)
                        check.DeregisterCriticalServiceAfter = TimeSpan.FromMinutes(deregisterInactiveServiceAfterMinutes);
                }

                agentChecks.AddRange(checks);
            }

            //dynamic class check

            //

            var registration = new AgentServiceRegistration()
            {
                ID = $"{consulConfig.ApiName}-{Environment.MachineName}-{uri.Port}",
                Name = consulConfig.ApiName,
                Address = $"{(string.IsNullOrWhiteSpace(OverrideServerIPAddress) ? uri.Host : OverrideServerIPAddress)}",
                Port = uri.Port,
                Tags = consulConfig.Tags,
                Checks = agentChecks.ToArray()
            };

            logger.LogInformation("Registering with Consul");

            try
            {
                consulClient.Agent.ServiceDeregister(registration.ID).Wait();
            }
            catch
            {
                logger.LogInformation("Cant detect service in consul to deregister.");
            }

            try
            {
                consulClient.Agent.ServiceRegister(registration).Wait();
            }
            catch (Exception ex)
            {
                logger.LogWarning($"Cant register in consul. {ex.Message}");
            }

            lifetime.ApplicationStopping.Register(() =>
            {
                logger.LogInformation("Deregistering from Consul");
                try
                {
                    consulClient.Agent.ServiceDeregister(registration.ID).Wait();
                }
                catch (Exception ex)
                {
                    //ignored
                    logger.LogInformation($"Cant deregister service [{registration.Name}]. Error: {ex.Message}");
                }
            });
        }
#endif

    }
}
