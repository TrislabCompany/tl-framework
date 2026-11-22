using System;
using System.Globalization;
using Trislab.Modules.Multitenancy.Provisioning;
using System.Linq;
using Trislab.Modules.Multitenancy.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace Multitenancy.Tests.Localization;

// Proves the MultitenancyResources catalog is embedded under the correct manifest name (ResourcesPath="" =>
// co-located marker + resx). A wrong manifest name flips ResourceNotFound and leaks raw keys; a
// sl-SI key the neutral catalog lacks is an orphan of a rename or delete. Both are caught here.
// A missing sl-SI entry is allowed: it falls back to English.
public sealed class MultitenancyResourcesTests
{
    private static IStringLocalizer BuildLocalizer()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalization(o => o.ResourcesPath = "");
        return services.BuildServiceProvider()
            .GetRequiredService<IStringLocalizerFactory>()
            .Create(typeof(MultitenancyResources));
    }

    private static List<string> KeysFor(string culture)
    {
        var localizer = BuildLocalizer();
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = culture.Length == 0
                ? CultureInfo.InvariantCulture
                : new CultureInfo(culture);
            return localizer.GetAllStrings(includeParentCultures: false)
                .Select(s => s.Name)
                .ToList();
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void Every_slSI_key_exists_in_the_neutral_catalog()
    {
        var neutral = KeysFor(string.Empty);   // MultitenancyResources.resx (English / fallback)
        var sl = KeysFor("sl-SI");                 // MultitenancyResources.sl-SI.resx

        neutral.ShouldNotBeEmpty();
        sl.Except(neutral, StringComparer.Ordinal)
            .OrderBy(k => k, StringComparer.Ordinal)
            .ShouldBeEmpty();
    }

    [Fact]
    public void Known_key_resolves_and_differs_between_en_and_pt()
    {
        var localizer = BuildLocalizer();
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("en-US");
            var en = localizer["Multitenancy.RootTenantCannotBeDeactivated"];
            en.ResourceNotFound.ShouldBeFalse(
                "resx did not resolve 'Multitenancy.RootTenantCannotBeDeactivated' for en-US — check ResourcesPath/resx manifest name.");
            en.Value.ShouldBe("The root tenant cannot be deactivated.");

            CultureInfo.CurrentUICulture = new CultureInfo("sl-SI");
            var sl = localizer["Multitenancy.RootTenantCannotBeDeactivated"];
            sl.ResourceNotFound.ShouldBeFalse(
                "resx did not resolve 'Multitenancy.RootTenantCannotBeDeactivated' for sl-SI — check the .sl-SI catalog manifest name.");
            sl.Value.ShouldBe("Korenskega najemnika ni mogoče deaktivirati.");

            sl.Value.ShouldNotBe(en.Value);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    // An enum handed to a message as an argument is looked up as "{EnumType}.{Member}" by
    // GlobalExceptionHandler. A member with no entry falls back to its C# name, which is a code
    // identifier, not display text, so every member needs a neutral entry; sl-SI may omit it and fall back.
    [Theory]
    [InlineData(typeof(TenantProvisioningStatus))]
    public void Every_enum_member_that_reaches_a_message_has_a_neutral_entry(Type enumType)
    {
        ArgumentNullException.ThrowIfNull(enumType);

        var neutral = KeysFor(string.Empty);

        foreach (var member in Enum.GetNames(enumType))
        {
            var key = $"{enumType.Name}.{member}";
            neutral.ShouldContain(key);
        }
    }
}
