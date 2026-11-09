// <copyright file="MixesTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Tests.Pages;

using System.Globalization;
using System.Threading;
using Bunit;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.ItemCrafting;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Web.AdminPanel.Pages;
using MUnique.OpenMU.Web.Shared.Components.Toast;
using MUnique.OpenMU.Web.Shared.Services;
using BasicModel = MUnique.OpenMU.Persistence.BasicModel;

/// <summary>
/// Tests for the <see cref="Mixes"/> page.
/// </summary>
[TestFixture]
[NonParallelizable]
public class MixesTests
{
    private BunitContext _context = null!;

    private CultureInfo _previousUiCulture = null!;

    private Mock<IContext> _persistenceContext = null!;

    private BasicModel.SimpleCraftingSettings _settings = null!;

    /// <summary>
    /// Sets up the page with one mix of an NPC.
    /// </summary>
    [SetUp]
    public void Setup()
    {
        this._previousUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");

        var requiredItem = new BasicModel.ItemCraftingRequiredItem
        {
            MinimumAmount = 1,
            MaximumAmount = 1,
            AddPercentage = 5,
            SuccessResult = MixResult.Disappear,
            FailResult = MixResult.StaysAsIs,
        };

        this._settings = new BasicModel.SimpleCraftingSettings
        {
            SuccessPercent = 50,
            MaximumSuccessPercent = 100,
            Money = 1000,
            ResultItemLuckOptionChance = 25,
        };
        this._settings.RequiredItems.Add(requiredItem);

        var crafting = new BasicModel.ItemCrafting
        {
            Number = 1,
            Name = "Test mix",
            SimpleCraftingSettings = this._settings,
        };

        var monster = new BasicModel.MonsterDefinition
        {
            Id = Guid.NewGuid(),
            Number = 238,
            Designation = "Test NPC",
        };
        monster.ItemCraftings.Add(crafting);

        var configuration = new BasicModel.GameConfiguration();
        configuration.Monsters.Add(monster);

        this._persistenceContext = new Mock<IContext>();
        this._persistenceContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var dataSource = new Mock<IDataSource<GameConfiguration>>();
        dataSource.Setup(s => s.GetContextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(this._persistenceContext.Object);
        dataSource.Setup(s => s.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(configuration);

        this._context = new BunitContext();
        this._context.JSInterop.Mode = JSRuntimeMode.Loose;
        this._context.Services.Configure<RequestLocalizationOptions>(options => options.AddSupportedUICultures("en"));
        this._context.Services.AddSingleton(dataSource.Object);
        this._context.Services.AddSingleton(Mock.Of<IToastService>());
        this._context.Services.AddSingleton(new LoadingOverlayService());
        this._context.Services.AddSingleton(Mock.Of<ILogger<Mixes>>());
        this._context.ComponentFactories.AddStub<MUnique.OpenMU.Web.Shared.Components.Breadcrumb>();
    }

    /// <summary>
    /// Restores the culture and disposes the test context.
    /// </summary>
    [TearDown]
    public void Teardown()
    {
        this._context.Dispose();
        CultureInfo.CurrentUICulture = this._previousUiCulture;
    }

    /// <summary>
    /// The mix is shown with its success rate and the fail result of its required items.
    /// </summary>
    [Test]
    public void ShowsSuccessRateAndFailResult()
    {
        var cut = this.RenderPage();

        var inputValues = cut.FindAll("input[type=number]").Select(input => input.GetAttribute("value")).ToList();
        Assert.That(inputValues, Has.Some.EqualTo("50"), "The success rate should be shown.");
        Assert.That(
            cut.FindAll("select").Any(select => select.GetAttribute("value") == nameof(MixResult.StaysAsIs)),
            Is.True,
            "The fail result of the required item should be shown.");
    }

    /// <summary>
    /// Changing the success rate updates the configuration.
    /// </summary>
    [Test]
    public void ChangingSuccessRateUpdatesConfiguration()
    {
        var cut = this.RenderPage();

        var input = cut.FindAll("input[type=number]").First(element => element.GetAttribute("value") == "50");
        input.Change("75");

        Assert.That(this._settings.SuccessPercent, Is.EqualTo(75));
    }

    /// <summary>
    /// Saving the page persists the changes through the context.
    /// </summary>
    [Test]
    public void SavePersistsTheChanges()
    {
        var cut = this.RenderPage();

        cut.FindAll("button").First(button => button.TextContent.Contains("Save changes")).Click();

        this._persistenceContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private IRenderedComponent<Mixes> RenderPage()
    {
        var cut = this._context.Render<Mixes>();
        cut.WaitForAssertion(() => Assert.That(cut.Markup, Does.Contain("Test mix")), TimeSpan.FromSeconds(10));
        return cut;
    }
}
