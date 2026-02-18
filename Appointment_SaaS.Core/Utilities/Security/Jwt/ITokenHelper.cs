using Appointment_SaaS.Core.Utilities.Security.JWT;
using Appointment_SaaS.Core.Utilities.Security.Jwt;
using Appointment_SaaS.Core.Entities; // AccessToken'ı görmesi için

namespace Appointment_SaaS.Core.Utilities.Security.JWT;

public interface ITokenHelper
{
    AccessToken CreateToken(AppUser user, List<OperationClaim> operationClaims);
}