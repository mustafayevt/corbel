using System.Reflection;
using Corbel.Domain.Entities;
using NetArchTest.Rules;
using Shouldly;
using Xunit;
using TestResult = NetArchTest.Rules.TestResult;

namespace Corbel.Api.Tests.Architecture;

/// <summary>
/// The architecture boundary tests: the domain stays persistence- and web-framework-agnostic and independent of
/// the outer Infrastructure/Features layers; infrastructure never reaches up into a feature; and the vertical
/// slices stay independent of one another. (All production types live in the single <c>Corbel.Api</c> assembly;
/// ASP.NET Core Identity is intentionally allowed because <c>AppUser</c> extends <c>IdentityUser</c>, and the
/// shared <c>Corbel.Common</c> error registry is allowed because the domain exceptions reference it.)
/// </summary>
public sealed class ArchitectureTests
{
    private static readonly Assembly ProductionAssembly = typeof(Note).Assembly;

    [Fact]
    public void Domain_does_not_depend_on_ef_core_or_aspnet_mvc_http()
    {
        var result = Types.InAssembly(ProductionAssembly)
            .That().ResideInNamespaceStartingWith("Corbel.Domain")
            .ShouldNot().HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore.Mvc",
                "Microsoft.AspNetCore.Http")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Explain(result));
    }

    [Fact]
    public void Domain_does_not_depend_on_the_outer_layers()
    {
        // The inner Domain layer must not reach outward into Infrastructure (EF mappings, services) or the
        // Features (vertical slices). It MAY reference the shared Corbel.Common error registry, so it isn't listed.
        var result = Types.InAssembly(ProductionAssembly)
            .That().ResideInNamespaceStartingWith("Corbel.Domain")
            .ShouldNot().HaveDependencyOnAny("Corbel.Infrastructure", "Corbel.Features")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Explain(result));
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_the_feature_slices()
    {
        // Persistence/auth infrastructure is a leaf the slices build on; it must never reach UP into a feature.
        var result = Types.InAssembly(ProductionAssembly)
            .That().ResideInNamespaceStartingWith("Corbel.Infrastructure")
            .ShouldNot().HaveDependencyOn("Corbel.Features")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Explain(result));
    }

    [Fact]
    public void Feature_slices_do_not_depend_on_each_other()
    {
        // Vertical-slice independence: one feature must not import another — they compose only at the host.
        string[] slices = ["Notes", "Auth", "Admin"];
        foreach (var slice in slices)
        {
            var siblings = slices
                .Where(s => !string.Equals(s, slice, StringComparison.Ordinal))
                .Select(s => $"Corbel.Features.{s}")
                .ToArray();
            var result = Types.InAssembly(ProductionAssembly)
                .That().ResideInNamespaceStartingWith($"Corbel.Features.{slice}")
                .ShouldNot().HaveDependencyOnAny(siblings)
                .GetResult();

            var offenders = result.FailingTypeNames is { Count: > 0 } names ? string.Join(", ", names) : "none";
            result.IsSuccessful.ShouldBeTrue(
                $"Feature slice '{slice}' must not depend on a sibling slice. Offending types: {offenders}");
        }
    }

    private static string Explain(TestResult result)
    {
        var offenders = result.FailingTypeNames is { Count: > 0 } names ? string.Join(", ", names) : "none";
        return "Corbel.Domain must stay independent of EF Core, ASP.NET MVC/HTTP, and the outer " +
               $"Infrastructure/Features layers. Offending types: {offenders}";
    }
}
