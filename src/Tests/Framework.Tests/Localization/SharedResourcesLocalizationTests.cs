using System.Globalization;
using FSH.Framework.Core.Localization;
using FSH.Framework.Web.Localization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace Framework.Tests.Localization;

// Proves the whole resx wiring: SharedResources marker + co-located resx + ResourcesPath="" in
// AddHeroLocalization resolve to the embedded catalog under the current UI culture. If the manifest
// name or ResourcesPath is wrong, ResourceNotFound flips true and IStringLocalizer leaks the raw key.
public sealed class SharedResourcesLocalizationTests
{
    private static IStringLocalizer<SharedResources> BuildLocalizer()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHeroLocalization(configuration);
        return services.BuildServiceProvider().GetRequiredService<IStringLocalizer<SharedResources>>();
    }

    // Catalogs are named for specific cultures (SharedResources.sl-SI.resx), matching the front-end.
    // The consequence is deliberate and pinned here: only sl-SI is served Slovenian. A bare `sl` or
    // an unsupported variant like pt-PT walks its parent chain, finds no catalog of its own and lands
    // on the neutral (English) one, rather than being silently handed Brazilian strings.
    [Theory]
    [InlineData("sl-SI", "Não encontrado")]   // specific sl-SI resolves directly
    [InlineData("sl", "Not Found")]           // bare sl has no catalog -> neutral English
    [InlineData("pt-PT", "Not Found")]         // unsupported variant -> neutral English, NOT sl-SI
    [InlineData("en-US", "Not Found")]        // en-US falls back to the neutral (default) catalog
    public void Localizer_resolves_error_key_per_culture(string culture, string expected)
    {
        var localizer = BuildLocalizer();
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo(culture);
            var localized = localizer["Error.NotFound"];

            localized.ResourceNotFound.ShouldBeFalse(
                $"resx for '{culture}' did not resolve 'Error.NotFound' — check ResourcesPath/resx manifest name.");
            localized.Value.ShouldBe(expected);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    // A sl-SI translation is optional for contributors, which only holds if a key the .sl-SI resx
    // lacks still resolves (to English) under the production wiring instead of leaking the raw key.
    [Fact]
    public void Key_missing_from_the_slSI_catalog_falls_back_to_English()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHeroLocalization(configuration);
        var localizer = services.BuildServiceProvider()
            .GetRequiredService<IStringLocalizerFactory>()
            .Create(typeof(FallbackProbeResources));

        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("sl-SI");

            // Without this, a sl-SI satellite that never got built would pass the fallback assertion below.
            localizer["Probe.Translated"].Value.ShouldBe("Traduzido em português");

            var englishOnly = localizer["Probe.EnglishOnly"];
            englishOnly.ResourceNotFound.ShouldBeFalse();
            englishOnly.Value.ShouldBe("Only in English");
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }
}
