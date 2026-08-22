using System.Text;
using Content.Shared.Coordinates;
using Content.Shared.Interaction;
using Content.Shared.NPC.Systems;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Shared._Triad.SecretCoordinates;

public sealed partial class SecretCoordinatesSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private PaperSystem _paper = default!;
    [Dependency] private NpcFactionSystem _factions = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    [SubscribeLocalEvent]
    private void OnPaperMapInit(Entity<SecretCoordinatesPaperComponent> ent, ref MapInitEvent args)
    {
        if (_net.IsClient)
            return;

        if (!TryComp<PaperComponent>(ent.Owner, out var paperComp))
            return;

        var redactedCoordinateMsg = new FormattedMessage();

        var query = EntityQueryEnumerator<SecretCoordinatesEntityComponent, TransformComponent>();
        while (query.MoveNext(out var entity, out var coordComp, out var xform))
        {
            // Make sure the IDs are the same
            if (ent.Comp.LinkedId != coordComp.LinkedId)
                continue;

            var mapCoords = _transform.GetMapCoordinates(xform);
            var redactedText = RedactText(ent.Comp.RedactCharacters, mapCoords.Position.ToString());
            redactedCoordinateMsg.AddText("- " + redactedText);
            redactedCoordinateMsg.PushNewline();
        }

        var paperContent = Loc.GetString(ent.Comp.SecretCoordinatePaperContent, ("coordinates", redactedCoordinateMsg.ToString()));
        _paper.SetContent((ent.Owner, paperComp), paperContent);
    }

    public string RedactText(List<string> redactChars, string text)
    {
        var redactedText = new StringBuilder();
        var randomLengthVariance = _random.Next(1, 10);

        for (var i = 0; i < text.Length + randomLengthVariance; i++)
        {
            var randomCharacter = _random.Pick(redactChars);
            redactedText.Append(randomCharacter);
        }

        return redactedText.ToString();
    }

    [SubscribeLocalEvent]
    private void OnPaperActivateInWorld(Entity<SecretCoordinatesPaperComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TryBypassSecretCoordinates(ent, args.User);
    }

    public bool TryBypassSecretCoordinates(Entity<SecretCoordinatesPaperComponent> ent, EntityUid user)
    {
        if (!_factions.IsMemberOfAny(user, ent.Comp.BypassFactions))
            return false;

        RevealSecretCoordinates(ent, user);
        return true;
    }

    public void RevealSecretCoordinates(Entity<SecretCoordinatesPaperComponent> ent, EntityUid? user)
    {
        if (_net.IsClient)
            return;

        if (ent.Comp.Revealed)
            return;

        if (!TryComp<PaperComponent>(ent.Owner, out var paperComp))
            return;

        var coordinateMsg = new FormattedMessage();
        var anyFound = false;

        var query = EntityQueryEnumerator<SecretCoordinatesEntityComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var coordComp, out var xform))
        {
            // Make sure the IDs are the same
            if (ent.Comp.LinkedId != coordComp.LinkedId)
                continue;

            anyFound = true;

            var mapCoords = _transform.GetMapCoordinates(xform);
            coordinateMsg.AddText("- " + mapCoords.Position.ToString());
            coordinateMsg.PushNewline();
        }

        var popupText = Loc.GetString("secret-coordinates-coordinates-reveal-popup-success");

        if (!anyFound)
        {
            coordinateMsg.PushNewline();
            coordinateMsg.AddText(Loc.GetString("secret-coordinates-coordinates-unknown"));
            popupText = Loc.GetString("secret-coordinates-coordinates-reveal-popup-fail");
        }

        var paperContent = Loc.GetString(ent.Comp.SecretCoordinatePaperContent, ("coordinates", coordinateMsg.ToString()));
        _paper.SetContent((ent.Owner, paperComp), paperContent);
        ent.Comp.Revealed = true;

        if (user != null)
            _popup.PopupEntity(popupText, ent.Owner, PopupType.Medium);
    }
}
