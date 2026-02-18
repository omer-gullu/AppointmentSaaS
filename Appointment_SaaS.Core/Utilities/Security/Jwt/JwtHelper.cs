using System;
using System.Collections.Generic;
using System.Linq; // Select() kullanabilmek için ŞART
using System.IdentityModel.Tokens.Jwt; // JwtSecurityToken için ŞART
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Appointment_SaaS.Core.Utilities.Security.Jwt;
using Appointment_SaaS.Core.Entities;

namespace Appointment_SaaS.Core.Utilities.Security.JWT;
public class JwtHelper : ITokenHelper
{
    private readonly IConfiguration _configuration;
    private readonly TokenOptions _tokenOptions;

    public JwtHelper(IConfiguration configuration)
    {
        _configuration = configuration;
        _tokenOptions = _configuration.GetSection("TokenOptions").Get<TokenOptions>();
    }

    public AccessToken CreateToken(AppUser user, List<OperationClaim> operationClaims)
    {
        var securityKey = SecurityKeyHelper.CreateSecurityKey(_tokenOptions.SecurityKey);
        var signingCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {

            new(ClaimTypes.NameIdentifier, user.AppUserID.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}")
        };

        // Yetkileri (Admin, Personel vb.) ekliyoruz
        claims.AddRange(operationClaims.Select(oc => new Claim(ClaimTypes.Role, oc.Name)));

        var jwt = new JwtSecurityToken(
            issuer: _tokenOptions.Issuer,
            audience: _tokenOptions.Audience,
            claims: claims,
            notBefore: DateTime.Now,
            expires: DateTime.Now.AddMinutes(_tokenOptions.AccessTokenExpiration),
            signingCredentials: signingCredentials
        );

        return new AccessToken
        {
            Token = new JwtSecurityTokenHandler().WriteToken(jwt),
            Expiration = jwt.ValidTo
        };
    }

  
}