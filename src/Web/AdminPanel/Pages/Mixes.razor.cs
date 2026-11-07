// <copyright file="Mixes.razor.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.Pages;

using System.Threading;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Configuration.ItemCrafting;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Web.AdminPanel.Properties;
using MUnique.OpenMU.Web.Shared.Components.Toast;
using MUnique.OpenMU.Web.Shared.Services;

/// <summary>
/// A page which shows the crafting definitions (mixes) of the chaos machine and the other NPCs which offer them.
/// </summary>
public partial class Mixes : ComponentBase, IAsyncDisposable
{
    private readonly List<(string Title, IReadOnlyList<ItemCrafting> Craftings)> _groups = [];

    private readonly Dictionary<Guid, ItemCrafting> _craftingsById = [];

    private readonly Dictionary<Guid, ItemDefinition> _itemsById = [];

    private readonly Dictionary<Guid, ItemOptionType> _optionTypesById = [];

    private GameConfiguration? _gameConfiguration;

    private IContext? _editContext;

    private CancellationTokenSource? _disposeCts;

    private Task? _loadTask;

    private string? _filter;

    /// <summary>
    /// Gets or sets the data source of the game configuration.
    /// </summary>
    [Inject]
    public IDataSource<GameConfiguration> DataSource { get; set; } = null!;

    /// <summary>
    /// Gets or sets the persistence context provider.
    /// </summary>
    [Inject]
    public IPersistenceContextProvider PersistenceContextProvider { get; set; } = null!;

    /// <summary>
    /// Gets or sets the toast service.
    /// </summary>
    [Inject]
    public IToastService ToastService { get; set; } = null!;

    /// <summary>
    /// Gets or sets the loading overlay service.
    /// </summary>
    [Inject]
    public LoadingOverlayService LoadingService { get; set; } = null!;

    /// <summary>
    /// Gets or sets the logger.
    /// </summary>
    [Inject]
    public ILogger<Mixes> Logger { get; set; } = null!;

    private IEnumerable<ItemDefinition> Items => this._itemsById.Values.OrderBy(item => item.Name.ValueInNeutralLanguage, StringComparer.CurrentCultureIgnoreCase);

    private IEnumerable<ItemOptionType> OptionTypes => this._optionTypesById.Values.OrderBy(type => type.Name.ToString(), StringComparer.CurrentCultureIgnoreCase);

    /// <summary>
    /// Gets the monsters with their crafting definitions, grouped and filtered by the search term.
    /// </summary>
    private IEnumerable<(string Title, IReadOnlyList<ItemCrafting> Craftings)> CraftingGroups
    {
        get
        {
            foreach (var (title, craftings) in this._groups)
            {
                var filtered = craftings.Where(crafting => this.MatchesFilter(title, crafting)).ToList();
                if (filtered.Count > 0)
                {
                    yield return (title, filtered);
                }
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await (this._disposeCts?.CancelAsync() ?? Task.CompletedTask).ConfigureAwait(false);
        this._disposeCts?.Dispose();
        this._disposeCts = null;

        try
        {
            await (this._loadTask ?? Task.CompletedTask).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // expected when navigating away
        }
        catch
        {
            // and we should not throw exceptions in the dispose method
        }

        this._editContext?.Dispose();
        this._editContext = null;
    }

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        var cts = new CancellationTokenSource();
        this._disposeCts = cts;
        this._loadTask = Task.Run(() => this.LoadDataAsync(cts.Token), cts.Token);
        await base.OnParametersSetAsync().ConfigureAwait(true);
    }

    private static string Caption(Type type, string propertyName) => type.GetPropertyCaption(propertyName);

    private static string? Description(Type type, string propertyName) => type.GetPropertyDescription(propertyName);

    private static IEnumerable<string> GetSelectedValues(ChangeEventArgs args)
    {
        return args.Value switch
        {
            string[] values => values,
            string single => [single],
            _ => [],
        };
    }

    private async Task LoadDataAsync(CancellationToken cancellationToken)
    {
        using var loading = this.LoadingService.ShowLoadingIndicator();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var gameConfiguration = await this.DataSource.GetOwnerAsync(cancellationToken: cancellationToken).ConfigureAwait(true);
            var editContext = this.PersistenceContextProvider.CreateNewTypedContext(typeof(ItemCrafting), true, gameConfiguration);
            var craftings = (await editContext.GetAsync(typeof(ItemCrafting), cancellationToken).ConfigureAwait(true))
                .OfType<ItemCrafting>()
                .ToList();
            var items = (await editContext.GetAsync(typeof(ItemDefinition), cancellationToken).ConfigureAwait(true))
                .OfType<ItemDefinition>()
                .ToList();
            var optionTypes = (await editContext.GetAsync(typeof(ItemOptionType), cancellationToken).ConfigureAwait(true))
                .OfType<ItemOptionType>()
                .ToList();
            cancellationToken.ThrowIfCancellationRequested();

            this._craftingsById.Clear();
            foreach (var crafting in craftings)
            {
                this._craftingsById[crafting.GetId()] = crafting;
            }

            this._itemsById.Clear();
            foreach (var item in items)
            {
                this._itemsById[item.GetId()] = item;
            }

            this._optionTypesById.Clear();
            foreach (var optionType in optionTypes)
            {
                this._optionTypesById[optionType.GetId()] = optionType;
            }

            var groups = new List<(string Title, IReadOnlyList<ItemCrafting> Craftings)>();
            foreach (var monster in gameConfiguration.Monsters.Where(monster => monster.ItemCraftings.Count > 0).OrderBy(monster => monster.Number))
            {
                var monsterCraftings = monster.ItemCraftings
                    .Where(crafting => this._craftingsById.ContainsKey(crafting.GetId()))
                    .Select(crafting => this._craftingsById[crafting.GetId()])
                    .OrderBy(crafting => crafting.Number)
                    .ToList();
                if (monsterCraftings.Count > 0)
                {
                    groups.Add(($"{monster.Number} - {monster.Designation}", monsterCraftings));
                }
            }

            this._groups.Clear();
            this._groups.AddRange(groups);
            this._gameConfiguration = gameConfiguration;

            this._editContext?.Dispose();
            this._editContext = editContext;

            await this.InvokeAsync(this.StateHasChanged).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // expected when navigating away
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error loading the mixes");
        }
    }

    private async Task OnSaveButtonClickAsync()
    {
        try
        {
            if (this._editContext is { } context)
            {
                var success = await context.SaveChangesAsync().ConfigureAwait(true);
                this.ToastService.ShowSuccess(success ? Resources.SavedChanges : Resources.NoChangesToSave);
                await this.DataSource.ForceDiscardChangesAsync().ConfigureAwait(true);
                this._craftingsById.Clear();
                this._groups.Clear();
                this._editContext.Dispose();
                this._editContext = null;
                await this.LoadDataAsync(this._disposeCts?.Token ?? default).ConfigureAwait(true);
            }
            else
            {
                this.ToastService.ShowError(Resources.FailedByUninitializedContext);
            }
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "An unexpected error occurred on save: {Message}", ex.Message);
            this.ToastService.ShowError(string.Format(Resources.UnexpectedErrorOccurred, ex.Message));
        }
    }

    private async Task OnDiscardButtonClickAsync()
    {
        if (this._editContext?.HasChanges is not true)
        {
            return;
        }

        await this.DataSource.DiscardChangesAsync().ConfigureAwait(true);
        this._editContext.Dispose();
        this._editContext = null;
        this._craftingsById.Clear();
        this._groups.Clear();
        await this.LoadDataAsync(this._disposeCts?.Token ?? default).ConfigureAwait(true);
    }

    private void OnAddRequiredItem(ItemCrafting crafting)
    {
        if (crafting.SimpleCraftingSettings is not { } settings || this._editContext is not { } context)
        {
            return;
        }

        var requiredItem = context.CreateNew<ItemCraftingRequiredItem>();
        requiredItem.MinimumAmount = 1;
        requiredItem.MaximumAmount = 1;
        requiredItem.SuccessResult = MixResult.Disappear;
        requiredItem.FailResult = MixResult.Disappear;
        settings.RequiredItems.Add(requiredItem);
    }

    private async Task OnRemoveRequiredItemAsync(ItemCrafting crafting, ItemCraftingRequiredItem requiredItem)
    {
        if (crafting.SimpleCraftingSettings is not { } settings || this._editContext is not { } context)
        {
            return;
        }

        settings.RequiredItems.Remove(requiredItem);
        await context.DeleteAsync(requiredItem).ConfigureAwait(true);
    }

    private void OnPossibleItemsChanged(ItemCraftingRequiredItem requiredItem, ChangeEventArgs args)
    {
        requiredItem.PossibleItems.Clear();
        foreach (var id in GetSelectedValues(args))
        {
            if (Guid.TryParse(id, out var parsedId) && this._itemsById.TryGetValue(parsedId, out var item))
            {
                requiredItem.PossibleItems.Add(item);
            }
        }
    }

    private void OnRequiredItemOptionsChanged(ItemCraftingRequiredItem requiredItem, ChangeEventArgs args)
    {
        requiredItem.RequiredItemOptions.Clear();
        foreach (var id in GetSelectedValues(args))
        {
            if (Guid.TryParse(id, out var parsedId) && this._optionTypesById.TryGetValue(parsedId, out var optionType))
            {
                requiredItem.RequiredItemOptions.Add(optionType);
            }
        }
    }

    private bool MatchesFilter(string monsterTitle, ItemCrafting crafting)
    {
        if (string.IsNullOrWhiteSpace(this._filter))
        {
            return true;
        }

        var filter = this._filter.Trim();
        return Matches(monsterTitle)
               || Matches(crafting.Name.ToString()) || Matches(crafting.Name.ValueInNeutralLanguage)
               || Matches(crafting.ItemCraftingHandlerClassName)
               || crafting.Number.ToString(System.Globalization.CultureInfo.InvariantCulture).Contains(filter, StringComparison.OrdinalIgnoreCase);

        bool Matches(string? text) => text?.Contains(filter, StringComparison.OrdinalIgnoreCase) is true;
    }
}
