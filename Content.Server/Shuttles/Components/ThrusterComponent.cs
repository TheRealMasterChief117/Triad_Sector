using System.Numerics;
using Content.Server._NF.M_Emp;
using Content.Server.Shuttles.Systems;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Damage;
using Content.Shared.DeviceLinking; // Frontier
using Content.Shared.Whitelist; // Triad
using Robust.Shared.Audio; // Triad
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Prototypes;

namespace Content.Server.Shuttles.Components
{
    [RegisterComponent, NetworkedComponent, AutoGenerateComponentPause]
    [Access(typeof(ThrusterSystem))]
    public sealed partial class ThrusterComponent : Component
    {
        /// <summary>
        /// Whether the thruster has been force to be enabled / disabled (e.g. VV, interaction, etc.)
        /// </summary>
        [DataField, ViewVariables(VVAccess.ReadWrite)]
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// This determines whether the thruster is actually enabled for the purposes of thrust
        /// </summary>
        public bool IsOn;

        // Need to serialize this because RefreshParts isn't called on Init and this will break post-mapinit maps!
        [ViewVariables(VVAccess.ReadWrite), DataField("thrust")]
        public float Thrust = 200f; // 100f->200f Mono

        [DataField("baseThrust"), ViewVariables(VVAccess.ReadWrite)]
        public float BaseThrust = 200f; // 100f->200f Mono

        [DataField("thrusterType")]
        public ThrusterType Type = ThrusterType.Linear;

        [DataField("burnShape")] public List<Vector2> BurnPoly = new()
        {
            new Vector2(-0.4f, 0.5f),
            new Vector2(-0.1f, 1.2f),
            new Vector2(0.1f, 1.2f),
            new Vector2(0.4f, 0.5f)
        };

        /// <summary>
        /// How much damage is done per second to anything colliding with our thrust.
        /// </summary>
        [DataField("damage")] public DamageSpecifier? Damage = new();

        [DataField("requireSpace")]
        public bool RequireSpace = true;

        // Used for burns

        // Triad start
        [ViewVariables]
        public HashSet<EntityUid> Colliding = new();

        [DataField]
        public LocId BurnPopupOther = "thruster-comp-burn-others";

        [DataField]
        public LocId BurnPopupSelf = "thruster-comp-burn-self";

        [DataField]
        public SoundSpecifier? BurnSound = new SoundPathSpecifier("/Audio/Effects/lightburn.ogg");

        [DataField]
        public float MaximumThrusterBurnRange = 5.5f;

        [DataField]
        public float MaximumMobThrusterBurnRange = 2.0f;

        [DataField]
        public float MobBurnDamageMultiplier = 0.3f;

        [DataField]
        public float DistanceBurnDamageMultiplier = 0.85f;

        [DataField]
        public List<ThrusterBlockRay> BlockCheckRays = new();

        [DataField]
        public int RequiredRayQuality = 3;
        // Triad end

        public bool Firing = false;

        /// <summary>
        /// How often thruster deals damage.
        /// </summary>
        [DataField]
        public TimeSpan FireCooldown = TimeSpan.FromSeconds(2);

        [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
        public TimeSpan NextFire = TimeSpan.Zero;

        // Frontier: upgradeable parts, togglable thrust
        [DataField("machinePartThrust")]
        public ProtoId<MachinePartPrototype> MachinePartThrust = "Capacitor";

        [DataField("partRatingThrustMultiplier")]
        public float PartRatingThrustMultiplier = 1.25f; // Mono: 1.15->1.25 (frontier: 1.5->1.15)

        /// <summary>
        ///     Frontier - Amount of charge this needs from an APC per second to function.
        /// </summary>
        public float OriginalLoad { get; set; } = 0;

        /// <summary>
        ///     Frontier - Make linkable to buttons
        /// </summary>
        [DataField("onPort")] // Frontier
        public ProtoId<SinkPortPrototype> OnPort = "On"; // Frontier

        [DataField("offPort")] // Frontier
        public ProtoId<SinkPortPrototype> OffPort = "Off"; // Frontier

        [DataField("togglePort")] // Frontier
        public ProtoId<SinkPortPrototype> TogglePort = "Toggle"; // Frontier
        // End Frontier: upgradeable parts, togglable thrust

        // Mono
        /// <summary>
        ///     If we have a <see cref="ThermalSignatureComponent"/>, heat signature output per thrust while working.
        /// </summary>
        [DataField]
        public float HeatSignatureRatio = 40f;

        /// <summary>
        ///     Triad - Which type of entities block thruster paths?
        /// </summary>
        [DataField]
        public EntityWhitelist? BlockThrusterWhitelist;

        /// <summary>
        ///     Triad - Which type of entities can be burnt by thrusters?
        /// </summary>
        [DataField]
        public EntityWhitelist? BurnWhitelist;

        /// <summary>
        ///     Triad - Which type of entities cannot be burnt by thrusters?
        /// </summary>
        [DataField]
        public EntityWhitelist? BurnBlacklist;
    }

    public enum ThrusterType
    {
        Linear,
        // Angular meaning rotational.
        Angular,
    }

    // Triad Start
    [DataDefinition]
    public sealed partial class ThrusterBlockRay
    {
        /// <summary>
        /// The direction/angle the raycast goes, relative to the entity's rotation.
        ///
        /// In radians, clockwise from the direction the thruster is facing
        /// </summary>
        private Robust.Shared.Maths.Angle _angle;

        public Robust.Shared.Maths.Angle AngleInRadians() { return _angle; }

        /// <summary>
        /// The direction/angle the raycast goes, relative to the entity's rotation.
        ///
        /// In degrees, clockwise from the direction the thruster is facing
        /// </summary>
        [DataField(required: true)]
        public double Angle
        {
            get
            {
                var t = _angle.Degrees % 360.0d;
                if (t < 0)
                    t += 360.0d;

                return t - 180.0d;
            }
            set
            {
                var t = ( value + 180.0 ) % 360.0d;
                if (t < 0)
                    t += 360.0d;
                _angle = Robust.Shared.Maths.Angle.FromDegrees(t);
            }
        }

        public float OffsetX = 0f;
        public float OffsetY = 0f;

        /// <summary>
        /// How much is the ray offset from the 'origin' of the entity's position?
        /// Useful for large thrusters where their 'origin' is on the tile they rotate by.
        /// +y is an offset in the direction of the fire
        /// </summary>
        [DataField]
        public Vector2 Offset
        {
            get => new(OffsetX, OffsetY);
            set
            {
                OffsetX = value.X;
                OffsetY = value.Y;
            }
        }

        /// <summary>
        /// How much is this ray worth if it can see space
        /// </summary>
        [DataField]
        public int Quality = 2;
    }
    // Triad end
}
