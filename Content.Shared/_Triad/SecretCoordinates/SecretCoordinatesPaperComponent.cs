using Content.Shared.NPC.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Triad.SecretCoordinates;

/// <summary>
///     A paper that has coordinates to some vessel hidden. Upon being revealed, the coordinates are shown on the paper.
///     This can be formatted in many ways, and can be linked to any vessel with a secret coordinate entity.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SecretCoordinatesPaperComponent : Component
{
    /// <summary>
    ///     The linked secret coordinate ID, a string value. The secret coordinate entity should have a matching ID.
    /// </summary>
    [DataField]
    public string LinkedId = "Default";

    [DataField]
    public LocId SecretCoordinatePaperContent;

    /// <summary>
    ///     Set of characters used when redacting a set of coordinates
    ///     This is a list, so you could for example make a set of coordinates appear as "a$sg$gh$gnml$".
    /// </summary>
    [DataField]
    public List<string> RedactCharacters = new List<string> { "█" };

    [DataField]
    public bool Revealed = false;

    /// <summary>
    ///     NPC factions needed to bypass getting clues for the coordinates.
    /// </summary>
    [DataField]
    public List<ProtoId<NpcFactionPrototype>> BypassFactions = new();
}
