using System.Security.Claims;
using nInvoices.Api.Infrastructure;
using Shouldly;

namespace nInvoices.Api.Tests.Infrastructure;

[TestFixture]
public sealed class KeycloakRoleClaimsTests
{
    private static ClaimsPrincipal Principal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "Bearer"));

    [Test]
    public void AddRealmRoles_RealmAccessWithRoles_AddsRoleClaims()
    {
        var principal = Principal(new Claim(KeycloakRoleClaims.RealmAccessClaim,
            """{"roles":["user","admin","offline_access"]}"""));

        KeycloakRoleClaims.AddRealmRoles(principal);

        principal.IsInRole("user").ShouldBeTrue();
        principal.IsInRole("admin").ShouldBeTrue();
        principal.IsInRole("offline_access").ShouldBeTrue();
    }

    [Test]
    public void AddRealmRoles_NotApprovedYet_HasNoUserRole()
    {
        var principal = Principal(new Claim(KeycloakRoleClaims.RealmAccessClaim,
            """{"roles":["offline_access","uma_authorization"]}"""));

        KeycloakRoleClaims.AddRealmRoles(principal);

        principal.IsInRole(KeycloakRoleClaims.AppUserRole).ShouldBeFalse();
    }

    [TestCase("not json")]
    [TestCase("[\"user\"]")]
    [TestCase("{\"roles\":\"user\"}")]
    [TestCase("{\"roles\":[1, null]}")]
    public void AddRealmRoles_MalformedClaim_GrantsNothing(string value)
    {
        var principal = Principal(new Claim(KeycloakRoleClaims.RealmAccessClaim, value));

        KeycloakRoleClaims.AddRealmRoles(principal);

        principal.FindAll(ClaimTypes.Role).ShouldBeEmpty();
    }

    [Test]
    public void AddRealmRoles_NoRealmAccess_GrantsNothing()
    {
        var principal = Principal(new Claim("sub", "abc"));

        KeycloakRoleClaims.AddRealmRoles(principal);

        principal.FindAll(ClaimTypes.Role).ShouldBeEmpty();
    }

    [Test]
    public void AddRealmRoles_CalledTwice_DoesNotDuplicate()
    {
        var principal = Principal(new Claim(KeycloakRoleClaims.RealmAccessClaim, """{"roles":["user"]}"""));

        KeycloakRoleClaims.AddRealmRoles(principal);
        KeycloakRoleClaims.AddRealmRoles(principal);

        principal.FindAll(ClaimTypes.Role).Count().ShouldBe(1);
    }
}
