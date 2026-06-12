using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Appointment_SaaS.Core.Utilities.Security.Jwt
{

    public class TokenOptions
    {
        public string Audience { get; set; } = string.Empty;
        public string Issuer { get; set; } = string.Empty;
        public int AccessTokenExpiration { get; set; }
        public string SecurityKey { get; set; } = string.Empty;
    }

}
