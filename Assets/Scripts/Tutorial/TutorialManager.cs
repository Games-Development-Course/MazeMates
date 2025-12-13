// ======================= TutorialManager.cs =======================
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class TutorialManager : NetworkBehaviour
{
    [Header("Config")]
    public TutorialStep[] steps;

    [Header("HUD")]
    public TutorialHUD travellerHUD;
    public TutorialHUD navigatorHUD;

    [Header("Scene References")]
    [SerializeField] private Transform travellerRoot;
    [SerializeField] private Camera travellerCamera;

    [Header("Waiting Messages")]
    [TextArea] public string travellerWaitingForNavigator = "ממתין לנווט…";
    [TextArea] public string navigatorWaitingForTraveller = "ממתין למטייל…";

    public float defaultAutoStepDelay = 0.15f;

    public NetworkVariable<bool> TutorialActive =
        new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private int currentIndex = -1;
    private bool stepActive;
    private bool conditionSatisfied;

    private float stepStartTime;
    private float hudShownTime;

    private bool travellerMoved, navigatorMoved;
    private bool travellerLooked, navigatorLooked;

    private TutorialStep Current => steps[currentIndex];

    public static List<TutorialColliderAuto> autoColliders = new List<TutorialColliderAuto>();

    public static void RegisterAutoCollider(TutorialColliderAuto c)
    {
        if (c != null && !autoColliders.Contains(c))
            autoColliders.Add(c);
    }

    // ============================================================
    // NETWORK SPAWN
    // ============================================================

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
            TutorialActive.Value = false;

        // ✅ במולטיפלייר פליי מוד יש היררכייה אחת -> לא מכבים HUD-ים פה
        if (travellerHUD != null) travellerHUD.gameObject.SetActive(true);
        if (navigatorHUD != null) navigatorHUD.gameObject.SetActive(true);

        Debug.Log($"[TUTORIAL][OnNetworkSpawn] LocalClientId={NetworkManager.Singleton?.LocalClientId} IsHost={IsHost} IsClient={IsClient} IsServer={IsServer}");
    }


    private void OnEnable()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    private void OnDisable()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }

    // ============================================================
    // CLIENT CONNECTED
    // ============================================================

    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;
        if (!TutorialActive.Value) return;
        if (currentIndex < 0 || currentIndex >= steps.Length) return;

        SendLocksToSpecificClient(clientId, Current);
    }

    private void SendLocksToSpecificClient(ulong clientId, TutorialStep step)
    {
        var p = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { clientId }
            }
        };

        ApplyLocksClientRpc(
            step.travellerLockMovement,
            step.travellerLockCamera,
            step.navigatorLockMovement,
            step.navigatorLockCamera,
            p
        );
    }

    // ============================================================
    // START TUTORIAL
    // ============================================================

    public void StartTutorial()
    {
        if (!IsServer) return;

        currentIndex = -1;
        TutorialActive.Value = true;
        NextStep();
    }

    private void Update()
    {
        if (!IsServer) return;
        if (!stepActive) return;

        if (currentIndex < 0 || currentIndex >= steps.Length) return;

        TutorialStep step = Current;

        float elapsedHUD = Time.time - hudShownTime;

        if (!step.completeOnCondition &&
            step.autoCompleteAfter > 0 &&
            Time.time - stepStartTime >= step.autoCompleteAfter)
        {
            CompleteCurrentStep();
            return;
        }

        if (step.completeOnCondition && conditionSatisfied)
        {
            if (elapsedHUD < step.minHUDDuration)
                return;

            CompleteCurrentStep();
        }
    }

    // ============================================================
    // STEP FLOW
    // ============================================================

    private void NextStep()
    {
        if (!IsServer) return;

        currentIndex++;

        if (currentIndex >= steps.Length)
        {
            Debug.Log("[TUTORIAL] Finished all steps.");

            stepActive = false;
            TutorialActive.Value = false;
            return;
        }

        Debug.Log($"[TUTORIAL] Next step index={currentIndex} stepId={Current.stepId} cond={Current.conditionType}");

        stepActive = true;
        conditionSatisfied = false;

        stepStartTime = Time.time;
        hudShownTime = Time.time;

        travellerMoved = navigatorMoved = false;
        travellerLooked = navigatorLooked = false;

        StartCoroutine(DelayedColliderStep(Current.stepId, 0.2f));

        ApplyLocks(Current);
        ShowStepHUD(Current);

        // סיבוב המטייל
        AlignTravellerForStep(Current);

        Current.onStepStart?.Invoke();
    }

    private IEnumerator DelayedColliderStep(string id, float delay)
    {
        yield return new WaitForSeconds(delay);
        StepStartedCollidersClientRpc(id);
    }

    [ClientRpc]
    private void StepStartedCollidersClientRpc(string stepId)
    {
        foreach (var c in autoColliders)
        {
            if (c != null)
                c.OnStepStarted(stepId);
        }
    }

    // ============================================================
    // LOCKS
    // ============================================================

    private void ApplyLocks(TutorialStep s)
    {
        ApplyLocksClientRpc(
            s.travellerLockMovement,
            s.travellerLockCamera,
            s.navigatorLockMovement,
            s.navigatorLockCamera
        );
    }

    [ClientRpc]
    private void ApplyLocksClientRpc(
        bool travellerLockMovement,
        bool travellerLockCamera,
        bool navigatorLockMovement,
        bool navigatorLockCamera,
        ClientRpcParams rpcParams = default)
    {
        bool iAmTraveller = IsHost;
        bool iAmNavigator = !IsHost;

        Debug.Log($"[TUTORIAL] ApplyLocks {(IsHost ? "HOST/Traveller" : "CLIENT/Navigator")} " +
                  $"Trav(M:{travellerLockMovement},C:{travellerLockCamera}) " +
                  $"Nav(M:{navigatorLockMovement},C:{navigatorLockCamera})");

        foreach (var m in Object.FindObjectsByType<PlayerMovement1P>(FindObjectsSortMode.None))
        {
            if (!m.IsOwner) continue;

            if (iAmTraveller)
                m.SetFrozen(travellerLockMovement);
            else
                m.SetFrozen(navigatorLockMovement);
        }

        foreach (var c in Object.FindObjectsByType<PlayerCamera1P>(FindObjectsSortMode.None))
        {
            if (!c.IsOwner) continue;

            if (iAmTraveller)
                c.SetCameraFrozen(travellerLockCamera);
            else
                c.SetCameraFrozen(navigatorLockCamera);
        }
    }

    // ============================================================
    // HUD
    // ============================================================

    private void ShowStepHUD(TutorialStep step)
    {
        Debug.Log($"[TUTORIAL] ShowStepHUD stepId={step.stepId}");
        SetTravellerHUDMessageClientRpc(step.travellerMessage);
        SetNavigatorHUDMessageClientRpc(step.navigatorMessage);
    }

    [ClientRpc]
    private void ClearHUDClientRpc()
    {
        if (IsHost)
        {
            Debug.Log("[TUTORIAL] ClearHUDClientRpc -> HOST clears travellerHUD");
            travellerHUD?.Clear();
        }
        else
        {
            Debug.Log("[TUTORIAL] ClearHUDClientRpc -> CLIENT clears navigatorHUD");
            navigatorHUD?.Clear();
        }
    }

    [ClientRpc]
    private void SetTravellerHUDMessageClientRpc(string msg)
    {
        Debug.Log($"[TUTORIAL][RPC] SetTravellerHUDMessageClientRpc side={(IsHost ? "HOST/Traveller" : "CLIENT/Navigator")} msg='{msg}'");

        if (!IsHost) return; // רק ה-Host (מטייל)

        if (travellerHUD == null)
        {
            Debug.LogWarning("[TUTORIAL] travellerHUD is NULL on host");
            return;
        }

        travellerHUD.ShowMessage(msg);
    }

    [ClientRpc]
    private void SetNavigatorHUDMessageClientRpc(string msg)
    {
        Debug.Log($"[TUTORIAL][RPC] SetNavigatorHUDMessageClientRpc side={(IsHost ? "HOST/Traveller" : "CLIENT/Navigator")} msg='{msg}'");

        if (IsHost) return; // רק ה-Client (נווט)

        if (navigatorHUD == null)
        {
            Debug.LogWarning("[TUTORIAL] navigatorHUD is NULL on client");
            return;
        }

        navigatorHUD.ShowMessage(msg);
    }

    // ============================================================
    // COMPLETE STEP
    // ============================================================

    private void MarkConditionSatisfiedInternal()
    {
        Debug.Log("reached markconditionsatisfiedinternal()");
        Debug.Log($"[TUTORIAL] Condition satisfied for step '{Current.stepId}'");

        conditionSatisfied = true;
    }

    private void CompleteCurrentStep()
    {
        Debug.Log($"[TUTORIAL] Step COMPLETE  {Current.stepId}");

        if (!IsServer || !stepActive)
        {
            Debug.Log("[TUTORIAL] Step complete aborted: not server or not active");
            return;
        }

        stepActive = false;
        Current.onStepComplete?.Invoke();
        StartCoroutine(GoNext());
    }

    private IEnumerator GoNext()
    {
        yield return new WaitForSeconds(defaultAutoStepDelay);
        NextStep();
    }

    // ============================================================
    // MOVEMENT + LOOK
    // ============================================================

    public void NotifyTravellerMoved() => HandleMovement(ref travellerMoved, true);
    public void NotifyNavigatorMoved() => HandleMovement(ref navigatorMoved, false);

    private void HandleMovement(ref bool flag, bool isTraveller)
    {
        if (!IsServer || !stepActive) return;

        flag = true;

        var step = Current;

        if (step.conditionType == TutorialConditionType.TravellerMoved && isTraveller)
            MarkConditionSatisfiedInternal();

        if (step.conditionType == TutorialConditionType.NavigatorMoved && !isTraveller)
            MarkConditionSatisfiedInternal();

        if (step.conditionType == TutorialConditionType.BothMoved &&
            travellerMoved && navigatorMoved)
            MarkConditionSatisfiedInternal();
    }

    public void NotifyTravellerLooked() => HandleLook(ref travellerLooked, true);
    public void NotifyNavigatorLooked() => HandleLook(ref navigatorLooked, false);

    private void HandleLook(ref bool flag, bool isTraveller)
    {
        if (!IsServer || !stepActive) return;

        flag = true;

        var step = Current;

        if (step.conditionType == TutorialConditionType.TravellerLookedAround && isTraveller)
            MarkConditionSatisfiedInternal();

        if (step.conditionType == TutorialConditionType.NavigatorLookedAround && !isTraveller)
            MarkConditionSatisfiedInternal();

        if (step.conditionType == TutorialConditionType.BothLookedAround &&
            travellerLooked && navigatorLooked)
            MarkConditionSatisfiedInternal();
    }

    public bool IsTutorialRunningForStep(string stepId)
    {
        if (!TutorialActive.Value)
            return false;

        if (currentIndex < 0 || currentIndex >= steps.Length)
            return false;

        return steps[currentIndex].stepId == stepId;
    }

    // ============================================================
    // CONDITION EVENTS
    // ============================================================

    public void NotifyNavigatorRemovedBomb() => NotifyNavigatorCondition(TutorialConditionType.NavigatorRemoveBomb);

    public void NotifyNavigatorOpenedNormalDoor()
    {
        NotifyNavigatorCondition(TutorialConditionType.NavigatorOpenNormalDoor);
        Debug.Log("reached NotifyNavigatorOpenedNormalDoor");
    }

    public void NotifyNavigatorOpenedPuzzleDoor() => NotifyNavigatorCondition(TutorialConditionType.NavigatorOpenPuzzleDoor);

    public void NotifyNavigatorOpenedExitDoor()
    {
        NotifyNavigatorCondition(TutorialConditionType.NavigatorOpenExitDoor);
        ActivateDiscoMode();
    }

    public void NotifyNavigatorPlacedHeart() => NotifyNavigatorCondition(TutorialConditionType.NavigatorPlaceHeart);
    public void NotifyNavigatorGaveLifebuoy() => NotifyNavigatorCondition(TutorialConditionType.NavigatorGiveLifebuoy);
    public void NotifyTravellerPlacedPuzzlePiece() => NotifyNavigatorCondition(TutorialConditionType.TravellerPlacedPuzzlePiece);

    public void NotifyTravellerPickedKey() => Check(TutorialConditionType.TravellerPickedKey);
    public void NotifyTravellerPickedHeart() => Check(TutorialConditionType.TravellerPickedHeart);
    public void NotifyPuzzleSolved() => Check(TutorialConditionType.PuzzleSolved);
    public void NotifyBothReachedExit() => Check(TutorialConditionType.BothReachedExit);
    public void NotifyCustomEvent() => Check(TutorialConditionType.CustomEvent);

    private void NotifyNavigatorCondition(TutorialConditionType condition)
    {
        Debug.Log("reached NotifyNavigatorCondition");

        if (IsServer)
        {
            Check(condition);
        }
        else
        {
            NotifyNavigatorConditionServerRpc(condition);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void NotifyNavigatorConditionServerRpc(TutorialConditionType condition)
    {
        Debug.Log("reached NotifyNavigatorConditionServerRpc(");
        Debug.Log($"[TUTORIAL] Server received navigator condition: {condition}");

        Check(condition);
    }

    private void Check(TutorialConditionType t)
    {
        Debug.Log("Reached check()");
        Debug.Log($"[TUTORIAL][CHECK] cond={t} step={Current.stepId} expect={Current.conditionType} active={stepActive}");

        if (!IsServer) return;
        if (!stepActive) return;

        if (Current.conditionType != t)
        {
            Debug.Log("[TUTORIAL][CHECK] Condition mismatch — ignored");
            return;
        }

        Debug.Log($"[TUTORIAL][CHECK] MATCH!  Step '{Current.stepId}' satisfied");

        MarkConditionSatisfiedInternal();
    }

    // ============================================================
    // TRAVELLER ROTATION / LOOK
    // ============================================================

    private void AlignTravellerForStep(TutorialStep step)
    {
        if (!step.rotateTravellerOnStepStart)
            return;

        if (string.IsNullOrEmpty(step.travellerLookTargetId))
            return;

        RotateTravellerClientRpc(step.travellerLookTargetId);
    }

    [ClientRpc]
    private void RotateTravellerClientRpc(string targetId)
    {
        if (!IsHost) return; // רק אצל המטייל

        if (travellerRoot == null)
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.traveller != null)
                travellerRoot = gm.traveller.transform;
        }

        if (travellerRoot == null)
        {
            Debug.LogWarning("[TUTORIAL] travellerRoot is null – cannot rotate");
            return;
        }

        Transform target = TutorialLookTarget.Get(targetId);
        if (target == null)
        {
            Debug.LogWarning($"[TUTORIAL] No TutorialLookTarget with id '{targetId}'");
            return;
        }

        Vector3 dir = target.position - travellerRoot.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.0001f)
            return;

        Quaternion rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        travellerRoot.rotation = rot;

        if (travellerCamera == null)
        {
            var cam = travellerRoot.GetComponentInChildren<Camera>();
            if (cam != null)
                travellerCamera = cam;
        }

        if (travellerCamera != null)
        {
            Vector3 euler = travellerCamera.transform.eulerAngles;
            euler.y = rot.eulerAngles.y;
            travellerCamera.transform.eulerAngles = euler;
        }
    }

    public void ActivateDiscoMode()
    {
        var disco = FindObjectOfType<DiscoTime>();
        if (disco != null)
        {
            disco.EnableDisco();
            Debug.Log("🎉 Disco Mode Activated!");
        }
    }

    // ============================================================
    // REGISTER TRAVELLER
    // ============================================================

    public void RegisterTraveller(Transform root)
    {
        travellerRoot = root;
    }

    public void RegisterTravellerCamera(Camera cam)
    {
        travellerCamera = cam;
    }
}
