#if !NET451
using Microsoft.Extensions.Configuration;
#endif
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.Extensions.Configuration
{
#if !NET451
    public static class ConsulConfigurationExtensions
    {
        public static IConfigurationBuilder AddConsul(this IConfigurationBuilder configurationBuilder, IEnumerable<Uri> consulUrls, string consulPath, string token)
        {
            return configurationBuilder.Add(new ConsulConfigurationSource(consulUrls, consulPath, token));
        }

        public static IConfigurationBuilder AddConsul(this IConfigurationBuilder configurationBuilder, IEnumerable<string> consulUrls, string consulPath, string token)
        {
            return configurationBuilder.AddConsul(consulUrls.Select(u => new Uri(u)), consulPath, token);
        }

        public static IConfigurationBuilder AddConsul(this IConfigurationBuilder configurationBuilder, string consulUrl, string consulPath, string token)
        {
            return configurationBuilder.AddConsul(new Uri[] { new Uri(consulUrl) }, consulPath, token);
        }
    }
#endif
}
