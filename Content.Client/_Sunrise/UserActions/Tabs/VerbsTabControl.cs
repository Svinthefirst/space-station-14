﻿using System.Linq;
using System.Numerics;
using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Speech;
using Content.Shared.Whitelist;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Sunrise.UserActions.Tabs;

[GenerateTypedNameReferences]
public sealed partial class VerbsTabControl : BaseTabControl
{
    [Dependency] private readonly EntityManager _entManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly ISharedPlayerManager _playerManager = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;

    private TimeSpan _lastVerbTime;
    private static readonly TimeSpan VerbCooldown = TimeSpan.FromSeconds(0.5f);

    public VerbsTabControl()
    {
        RobustXamlLoader.Load(this);
        IoCManager.InjectDependencies(this);
    }

    public override bool UpdateState()
    {
        VerbsList.RemoveAllChildren();

        var player = _playerManager.LocalEntity;
        if (player is not { Valid: true })
            return false;

        var emotes = _prototypeManager.EnumeratePrototypes<EmotePrototype>()
            .Where(emote => IsVerbsAvailable(emote, player.Value))
            .OrderBy(x => x.Category)
            .ThenBy(x => x.ID)
            .ToList();

        if (emotes.Count == 0)
            return false;

        var currentRow = CreateNewRow();
        VerbsList.AddChild(currentRow);

        foreach (var emote in emotes)
        {
            var button = CreateVerbButton(emote);
            currentRow.AddChild(button);
        }

        UpdateButtonsLayout();
        return true;
    }

    private void UpdateButtonsLayout()
    {
        if (!VerbsList.Children.Any())
            return;

        var maxWidth = Math.Min(300, Width);
        var firstRow = VerbsList.Children.First() as BoxContainer;
        if (firstRow == null)
            return;

        var currentRow = firstRow;
        var rowWidth = 0f;

        var allButtons = VerbsList.Children
            .OfType<BoxContainer>()
            .SelectMany(row => row.Children.ToList())
            .ToList();

        foreach (var button in allButtons)
        {
            button.Parent?.RemoveChild(button);
        }

        foreach (var child in VerbsList.Children.Skip(1).ToList())
        {
            VerbsList.RemoveChild(child);
        }
        firstRow.RemoveAllChildren();

        foreach (var button in allButtons)
        {
            if (rowWidth + 100 > maxWidth)
            {
                currentRow = CreateNewRow();
                VerbsList.AddChild(currentRow);
                rowWidth = 0;
            }

            currentRow.AddChild(button);
            rowWidth += 100;
        }
    }

    private BoxContainer CreateNewRow()
    {
        return new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
        };
    }

    private Button CreateVerbButton(EmotePrototype emote)
    {
        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            MinSize = new Vector2(0, 24),
            Margin = new Thickness(1)
        };

        var label = new RichTextLabel
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            Text = Loc.GetString(emote.Name)
        };

        var button = new Button
        {
            StyleClasses = { "EmoteButton" },
            HorizontalExpand = true,
            MinSize = new Vector2(0, 24),
            Margin = new Thickness(1)
        };

        container.AddChild(label);
        button.AddChild(container);

        button.OnPressed += _ => OnPlayVerb(new ProtoId<EmotePrototype>(emote.ID));

        return button;
    }


    private bool IsVerbsAvailable(EmotePrototype emote, EntityUid player)
    {
        var whitelistSystem = _entManager.System<EntityWhitelistSystem>();

        if (emote.Category == EmoteCategory.Invalid || emote.Category != EmoteCategory.Verb)
            return false;

        if (!whitelistSystem.IsWhitelistPassOrNull(emote.Whitelist, player) ||
            whitelistSystem.IsBlacklistPass(emote.Blacklist, player))
            return false;

        if (!emote.Available &&
            _entManager.TryGetComponent<SpeechComponent>(player, out var speech) &&
            !speech.AllowedEmotes.Contains(emote.ID))
            return false;

        return true;
    }

    private void OnPlayVerb(ProtoId<EmotePrototype> protoId)
    {
        var currentTime = _gameTiming.CurTime;
        if (currentTime - _lastVerbTime < VerbCooldown)
            return;

        _lastVerbTime = currentTime;
        _entManager.RaisePredictiveEvent(new PlayEmoteMessage(protoId));
    }

    protected override void Resized()
    {
        UpdateButtonsLayout();
    }
}
