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
using MUnique.OpenMU.Web.Shared.Components.Modal;
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

    private BasicModel.SimpleCraftingSettings _settings = null!;

    private Mock<IContext> _firstCategoryContext = null!;

    private Mock<IContext> _secondCategoryContext = null!;

    /// <summary>
    /// Sets up the page with two NPCs which offer one mix each.
    /// </summary>
    [SetUp]
    public void Setup()
    {
        this._previousUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");

        var jewelOfChaos = new BasicModel.ItemDefinition { Id = Guid.NewGuid(), Name = "Jewel of Chaos" };
        var requiredItem = new BasicModel.ItemCraftingRequiredItem
        {
            MinimumAmount = 1,
            MaximumAmount = 1,
            AddPercentage = 5,
            SuccessResult = MixResult.Disappear,
            FailResult = MixResult.StaysAsIs,
        };
        requiredItem.PossibleItems.Add(jewelOfChaos);

        this._settings = new BasicModel.SimpleCraftingSettings
        {
            SuccessPercent = 50,
            MaximumSuccessPercent = 100,
            Money = 1000,
            ResultItemLuckOptionChance = 25,
        };
        this._settings.RequiredItems.Add(requiredItem);

        var firstCrafting = new BasicModel.ItemCrafting { Number = 1, Name = "Chaos Weapon", SimpleCraftingSettings = this._settings };
        var secondCrafting = new BasicModel.ItemCrafting
        {
            Number = 2,
            Name = "Dark Horse",
            SimpleCraftingSettings = new BasicModel.SimpleCraftingSettings { SuccessPercent = 60 },
        };

        var firstMonster = new BasicModel.MonsterDefinition { Id = Guid.NewGuid(), Number = 238, Designation = "Test NPC" };
        firstMonster.ItemCraftings.Add(firstCrafting);
        var secondMonster = new BasicModel.MonsterDefinition { Id = Guid.NewGuid(), Number = 226, Designation = "Other NPC" };
        secondMonster.ItemCraftings.Add(secondCrafting);

        var configuration = new BasicModel.GameConfiguration();
        configuration.Monsters.Add(firstMonster);
        configuration.Monsters.Add(secondMonster);
        configuration.Items.Add(jewelOfChaos);

        this._firstCategoryContext = new Mock<IContext>();
        this._firstCategoryContext.Setup(c => c.GetByIdAsync(It.IsAny<Guid>(), typeof(ItemCrafting), It.IsAny<CancellationToken>()))
            .ReturnsAsync(firstCrafting);
        this._firstCategoryContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        this._secondCategoryContext = new Mock<IContext>();
        this._secondCategoryContext.Setup(c => c.GetByIdAsync(It.IsAny<Guid>(), typeof(ItemCrafting), It.IsAny<CancellationToken>()))
            .ReturnsAsync(secondCrafting);
        this._secondCategoryContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var contexts = new Queue<IContext>([this._firstCategoryContext.Object, this._secondCategoryContext.Object]);
        var persistenceContextProvider = new Mock<IPersistenceContextProvider>();
        persistenceContextProvider
            .Setup(p => p.CreateNewTypedContext(It.IsAny<Type>(), It.IsAny<bool>(), It.IsAny<GameConfiguration?>()))
            .Returns(() => contexts.Dequeue());

        var dataSource = new Mock<IDataSource<GameConfiguration>>();
        dataSource.Setup(s => s.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(configuration);

        this._context = new BunitContext();
        this._context.JSInterop.Mode = JSRuntimeMode.Loose;
        this._context.Services.AddLogging();
        this._context.Services.Configure<RequestLocalizationOptions>(options => options.AddSupportedUICultures("en"));
        this._context.Services.AddSingleton(dataSource.Object);
        this._context.Services.AddSingleton(persistenceContextProvider.Object);
        this._context.Services.AddSingleton(Mock.Of<IToastService>());
        this._context.Services.AddSingleton(Mock.Of<IModalService>());
        this._context.Services.AddSingleton(Mock.Of<ILookupController>());
        this._context.Services.AddSingleton(Mock.Of<IChangeNotificationService>());
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
    /// The page groups the mixes by their NPC, and every mix has its own form with a save button.
    /// </summary>
    [Test]
    public void ShowsOneFormWithSaveButtonPerMix()
    {
        var cut = this.RenderPage();

        Assert.That(cut.Markup, Does.Contain("238 - Test NPC"));
        Assert.That(cut.Markup, Does.Contain("226 - Other NPC"));
        var saveButtons = cut.FindAll("button").Where(button => button.TextContent.Trim() == "Save").ToList();
        Assert.That(saveButtons, Has.Count.EqualTo(2), "Every mix should have its own save button.");
    }

    /// <summary>
    /// The success rate of a mix is editable and updates the configuration.
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
    /// Saving a mix saves only the editing context of its own category.
    /// </summary>
    [Test]
    public void SavePersistsOnlyTheCategoryOfTheMix()
    {
        var cut = this.RenderPage();

        var firstForm = cut.FindAll("form").First();
        firstForm.Submit();

        this._firstCategoryContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        this._secondCategoryContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// The required items of a mix can be edited in its form.
    /// </summary>
    [Test]
    public void ShowsRequiredItemsOfTheMix()
    {
        var cut = this.RenderPage();

        Assert.That(cut.Markup, Does.Contain("Jewel of Chaos"), "The possible items should be shown.");
        Assert.That(
            cut.FindAll("button").Any(button => button.TextContent.Contains("Remove")),
            Is.True,
            "The required items should be editable.");
    }

    private IRenderedComponent<Mixes> RenderPage()
    {
        var cut = this._context.Render<Mixes>();
        cut.WaitForAssertion(() => Assert.That(cut.Markup, Does.Contain("Chaos Weapon")), TimeSpan.FromSeconds(10));
        return cut;
    }
}
