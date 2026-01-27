using Unity.Netcode;
using UnityEngine;

public class DuplicateSkinEmissionMarker_ByRole : NetworkBehaviour
{
    [Header("Clothes renderers only (NOT body SkinMat)")]
    [SerializeField] private Renderer[] renderersToGlow;

    [Header("Colors when both players picked same skin")]
    [SerializeField] private Color travellerEmission = Color.green;
    [SerializeField] private Color navigatorEmission = Color.red;

    [Tooltip("Intensity multiplier (try 2-6).")]
    [SerializeField] private float intensity = 3f;

    private PlayerMovement _pm; // your existing NetworkBehaviour with role
    private MaterialPropertyBlock _mpb;

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    public override void OnNetworkSpawn()
    {
        _pm = GetComponent<PlayerMovement>();

        Subscribe();
        Apply(); // initial
    }

    private void OnDestroy() => Unsubscribe();

    private void Subscribe()
    {
        var cfg = GameConfigNet.Instance;
        if (cfg != null)
        {
            cfg.HostSkin.OnValueChanged += OnSkinChanged;
            cfg.ClientSkin.OnValueChanged += OnSkinChanged;
        }

        // If your role is a NetworkVariable, subscribe here (OPTION A).
        // ADAPT HERE: replace with your actual role NetworkVariable name/type if exists.
        // Example:
        // if (_pm != null) _pm.Role.OnValueChanged += OnRoleChanged;
    }

    private void Unsubscribe()
    {
        var cfg = GameConfigNet.Instance;
        if (cfg != null)
        {
            cfg.HostSkin.OnValueChanged -= OnSkinChanged;
            cfg.ClientSkin.OnValueChanged -= OnSkinChanged;
        }

        // OPTION A unsubscribe:
        // if (_pm != null) _pm.Role.OnValueChanged -= OnRoleChanged;
    }

    private void OnSkinChanged(int oldV, int newV) => Apply();

    // OPTION A handler (if Role is NetworkVariable):
    // private void OnRoleChanged(<RoleType> oldV, <RoleType> newV) => Apply();

    private void Apply()
    {
        var cfg = GameConfigNet.Instance;
        if (cfg == null || _pm == null) return;

        bool sameSkin = (cfg.HostSkin.Value == cfg.ClientSkin.Value);

        // ADAPT HERE: implement based on YOUR PlayerMovement.role
        // Example if you have: public PlayerRole role; where PlayerRole.Navigator/Traveller
        bool iAmNavigator = IsNavigatorByOwner();

        bool enable = sameSkin;
        Color emission = Color.black;

        if (enable)
        {
            emission = iAmNavigator ? navigatorEmission : travellerEmission;
            emission.a = 1f;
            emission *= intensity;
        }

        SetEmission(enable, emission);
    }
    private bool IsNavigatorByOwner()
    {
        // אצלכם: שרת = Traveller, לקוח = Navigator
        return OwnerClientId != NetworkManager.ServerClientId;
    }

    private static bool IsNavigatorFromPlayerMovement(PlayerMovement pm)
    {
        return pm != null && pm.Role == PlayerMovement.PlayerRole.Navigator;
    }


    private void SetEmission(bool enable, Color emission)
    {
        if (renderersToGlow == null || renderersToGlow.Length == 0) return;

        _mpb ??= new MaterialPropertyBlock();

        foreach (var r in renderersToGlow)
        {
            if (!r) continue;

            // Ensures per-player independence even if original material is shared:
            var mats = r.materials; // instanced per renderer
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (!m) continue;

                if (enable) m.EnableKeyword("_EMISSION");
                else m.DisableKeyword("_EMISSION");
            }

            _mpb.Clear();
            _mpb.SetColor(EmissionColorId, emission);
            r.SetPropertyBlock(_mpb);
        }
    }
}
