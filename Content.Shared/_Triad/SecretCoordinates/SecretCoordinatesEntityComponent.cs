using Robust.Shared.GameStates;

namespace Content.Shared._Triad.SecretCoordinates;

/// <summary>
///     Used by SecretcoordinatesPaperComponent entities to denote where exactly a secret coordinate is.
///     For example, this is used by Solarian Caches to mark the location of the cache.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SecretCoordinatesEntityComponent : Component
{
    /// <summary>
    ///     The linked secret coordinate ID, a string value. The secret coordinate paper should have a matching ID.
    /// </summary>
    [DataField]
    public string LinkedId = "Default";
}
