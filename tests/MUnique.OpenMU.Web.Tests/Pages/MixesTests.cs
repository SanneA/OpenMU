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

    private Mock<IContext> _editContext = null!;

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

        var jewelOfChaos = new BasicModel.ItemDefinition { Id = Guid.NewGuid(), Name = "Jewel of Chaos" };
        requiredItem.PossibleItems.Add(jewelOfChaos);

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
            Name = "Chaos Weapon",
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
        configuration.Items.Add(jewelOfChaos);

        this._editContext = new Mock<IContext>();
        this._editContext.Setup(c => c.GetAsync(typeof(ItemCrafting), It.IsAny<CancellationToken>())).ReturnsAsync(new object[] { crafting });
        this._editContext.Setup(c => c.GetAsync(typeof(MUnique.OpenMU.DataModel.Configuration.Items.ItemDefinition), It.IsAny<CancellationToken>())).ReturnsAsync(new object[] { jewelOfChaos });
        this._editContext.Setup(c => c.GetAsync(typeof(MUnique.OpenMU.DataModel.Configuration.Items.ItemOptionType), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<object>());
        this._editContext.Setup(c => c.CreateNew<ItemCraftingRequiredItem>(It.IsAny<object?[]>()))
            .Returns(() => new BasicModel.ItemCraftingRequiredItem());
        this._editContext.Setup(c => c.DeleteAsync(It.IsAny<ItemCraftingRequiredItem>())).ReturnsAsync(true);
        this._editContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var persistenceContextProvider = new Mock<IPersistenceContextProvider>();
        persistenceContextProvider
            .Setup(p => p.CreateNewTypedContext(It.IsAny<Type>(), It.IsAny<bool>(), It.IsAny<GameConfiguration?>()))
            .Returns(this._editContext.Object);

        var dataSource = new Mock<IDataSource<GameConfiguration>>();
        dataSource.Setup(s => s.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(configuration);
        dataSource.Setup(s => s.ForceDiscardChangesAsync()).Returns(ValueTask.CompletedTask);
        dataSource.Setup(s => s.DiscardChangesAsync()).Returns(ValueTask.CompletedTask);

        this._context = new BunitContext();
        this._context.JSInterop.Mode = JSRuntimeMode.Loose;
        this._context.Services.Configure<RequestLocalizationOptions>(options => options.AddSupportedUICultures("en"));
        this._context.Services.AddSingleton(dataSource.Object);
        this._context.Services.AddSingleton(persistenceContextProvider.Object);
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
    /// Saving the page persists the changes through the editing context.
    /// </summary>
    [Test]
    public void SavePersistsTheChanges()
    {
        var cut = this.RenderPage();

        cut.FindAll("button").First(button => button.TextContent.Contains("Save changes")).Click();

        this._editContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// A new required item is created and added to the mix.
    /// </summary>
    [Test]
    public void AddsRequiredItem()
    {
        var cut = this.RenderPage();

        cut.FindAll("button").First(button => button.TextContent.Contains("Add required item")).Click();

        Assert.That(this._settings.RequiredItems, Has.Count.EqualTo(2));
        this._editContext.Verify(c => c.CreateNew<ItemCraftingRequiredItem>(It.IsAny<object?[]>()), Times.Once);
    }

    /// <summary>
    /// A required item is removed and deleted.
    /// </summary>
    [Test]
    public void RemovesRequiredItem()
    {
        var cut = this.RenderPage();
        var requiredItem = this._settings.RequiredItems.Single();

        cut.FindAll("button").First(button => button.TextContent.Contains("Remove")).Click();

        Assert.That(this._settings.RequiredItems, Is.Empty);
        this._editContext.Verify(c => c.DeleteAsync(requiredItem), Times.Once);
    }

    private IRenderedComponent<Mixes> RenderPage()
    {
        var cut = this._context.Render<Mixes>();
        cut.WaitForAssertion(() => Assert.That(cut.Markup, Does.Contain("Chaos Weapon")), TimeSpan.FromSeconds(10));
        return cut;
    }
}
