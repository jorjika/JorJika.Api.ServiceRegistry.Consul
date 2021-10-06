using System;
using System.Collections.Generic;
using System.Text;

namespace JorJika.Api.ServiceRegistry.Consul.Models
{
    public class ConsulConfig
    {
        public string Address { get; set; }
        public string Token { get; set; }
        public string ApiName { get; set; }
        public string ApiDomain { get; set; }
        public string[] Tags { get; set; }
    }
}
