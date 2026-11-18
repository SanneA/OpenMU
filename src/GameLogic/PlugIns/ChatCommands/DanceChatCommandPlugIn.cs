// <copyright file="DanceChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;

using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands.Arguments;
using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// A chat command plugin which lets the character dance.
/// </summary>
[Guid("A483C4B7-DF8D-48CA-A4B2-14A7A412839C")]
[PlugIn]
[Display(Name = nameof(PlugInResources.DanceChatCommandPlugIn_Name), Description = nameof(PlugInResources.DanceChatCommandPlugIn_Description), ResourceType = typeof(PlugInResources))]
[ChatCommandHelp(Command, typeof(EmptyChatCommandArgs), CharacterStatus.Normal)]
public class DanceChatCommandPlugIn : ChatCommandPlugInBase<EmptyChatCommandArgs>, ISupportCustomConfiguration<DanceChatCommandPlugIn.DanceChatCommandConfiguration>, ISupportDefaultCustomConfiguration
{
    private const string Command = "/dance";

    /// <summary>
    /// Gets or sets the configuration.
    /// </summary>
    public DanceChatCommandConfiguration? Configuration { get; set; }

    /// <inheritdoc />
    public override string Key => Command;

    /// <inheritdoc />
    public override CharacterStatus MinCharacterStatusRequirement => CharacterStatus.Normal;

    /// <inheritdoc />
    public object CreateDefaultConfig() => new DanceChatCommandConfiguration();

    /// <inheritdoc />
    protected override async ValueTask DoHandleCommandAsync(Player player, EmptyChatCommandArgs arguments)
    {
        var configuration = this.Configuration ??= (DanceChatCommandConfiguration)this.CreateDefaultConfig();
        await player.ForEachWorldObserverAsync<IShowAnimationPlugIn>(
            p => p.ShowAnimationAsync(player, configuration.AnimationNumber, null, player.Rotation),
            true).ConfigureAwait(false);
    }

    /// <summary>
    /// The configuration of a <see cref="DanceChatCommandPlugIn"/>.
    /// </summary>
    public class DanceChatCommandConfiguration
    {
        /// <summary>
        /// Gets or sets the number of the animation which is sent to the clients to make the character dance.
        /// </summary>
        [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DanceChatCommandConfiguration_AnimationNumber_Name), Description = nameof(PlugInResources.DanceChatCommandConfiguration_AnimationNumber_Description))]
        public byte AnimationNumber { get; set; } = 14;
    }
}
