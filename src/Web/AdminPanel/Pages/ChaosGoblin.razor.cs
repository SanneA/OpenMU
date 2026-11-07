// <copyright file="ChaosGoblin.razor.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.Pages;

using System.Threading;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.ItemCrafting;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Web.AdminPanel.Properties;
using MUnique.OpenMU.Web.Shared.Components.Toast;
using MUnique.OpenMU.Web.Shared.Services;

/// <summary>
/// A page which shows the crafting definitions (mixes) of the chaos machine, e.g. the chaos goblin.
/// </summary>
public partial class ChaosGoblin : ComponentBase, IAsyncDisposable
{
    private readonly List<MonsterDefinition> _monsters = [];

    private GameConfiguration? _gameConfiguration;

    private IContext? _persistenceContext;

    private CancellationTokenSource? _disposeCts;

    private Task? _loadTask;

    private string? _filter;

    /// <summary>
    /// Gets or sets the data source of the game configuration.
    /// </summary>
    [Inject]
    public IDataSource<GameConfiguration> DataSource { get; set; } = null!;

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
    public ILogger<ChaosGoblin> Logger { get; set; } = null!;

    /// <summary>
    /// Gets the monsters with their crafting definitions, grouped and filtered by the search term.
    /// </summary>
    private IEnumerable<(MonsterDefinition Monster, IReadOnlyList<ItemCrafting> Craftings)> CraftingGroups
    {
        get
        {
            foreach (var monster in this._monsters)
            {
                var craftings = monster.ItemCraftings
                    .Where(crafting => this.MatchesFilter(monster, crafting))
                    .OrderBy(crafting => crafting.Number)
                    .ToList();
                if (craftings.Count > 0)
                {
                    yield return (monster, craftings);
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

    private async Task LoadDataAsync(CancellationToken cancellationToken)
    {
        using var loading = this.LoadingService.ShowLoadingIndicator();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            this._persistenceContext = await this.DataSource.GetContextAsync(cancellationToken).ConfigureAwait(true);
            var gameConfiguration = await this.DataSource.GetOwnerAsync(cancellationToken: cancellationToken).ConfigureAwait(true);
            cancellationToken.ThrowIfCancellationRequested();

            this._monsters.Clear();
            this._monsters.AddRange(gameConfiguration.Monsters
                .Where(monster => monster.ItemCraftings.Count > 0)
                .OrderBy(monster => monster.Number));
            this._gameConfiguration = gameConfiguration;

            await this.InvokeAsync(this.StateHasChanged).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // expected when navigating away
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error loading the chaos machine craftings");
        }
    }

    private async Task OnSaveButtonClickAsync()
    {
        try
        {
            if (this._persistenceContext is { } context)
            {
                var success = await context.SaveChangesAsync().ConfigureAwait(true);
                this.ToastService.ShowSuccess(success ? Resources.SavedChanges : Resources.NoChangesToSave);
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
        if (this._persistenceContext?.HasChanges is not true)
        {
            return;
        }

        await this.DataSource.DiscardChangesAsync().ConfigureAwait(true);
        this._gameConfiguration = null;
        this._monsters.Clear();
        await this.LoadDataAsync(this._disposeCts?.Token ?? default).ConfigureAwait(true);
    }

    private bool MatchesFilter(MonsterDefinition monster, ItemCrafting crafting)
    {
        if (string.IsNullOrWhiteSpace(this._filter))
        {
            return true;
        }

        var filter = this._filter.Trim();
        return Matches(monster.Designation.ToString())
               || Matches(crafting.Name.ToString()) || Matches(crafting.Name.ValueInNeutralLanguage)
               || Matches(crafting.ItemCraftingHandlerClassName)
               || crafting.Number.ToString(System.Globalization.CultureInfo.InvariantCulture).Contains(filter, StringComparison.OrdinalIgnoreCase);

        bool Matches(string? text) => text?.Contains(filter, StringComparison.OrdinalIgnoreCase) is true;
    }
}
