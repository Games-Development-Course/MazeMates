// TutorialManager.cs
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TutorialManager : NetworkBehaviour
{
    [Header("Config")]
    public TutorialStep[] steps;

    [Header("HUD")]
    public TutorialHUD travellerHUD;
    public TutorialHUD navigatorHUD;

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

    // ============================================
    // AUTO COLLIDERS REGISTRY
    // ============================================
    public static List<TutorialColliderAuto> autoColliders = new List<TutorialColliderAuto>();

    public static void RegisterAutoCollider(TutorialColliderAuto c)
    {
        if (c != null && !autoColliders.Contains(c))
            autoColliders.Add(c);
    }

    // ============================================================
    // NETWORK START
    // ============================================================

    public override void OnNetworkSpawn()
    {
        if (IsServer)
            TutorialActive.Value = false;
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
    // CLIENT CONNECTED — APPLY CURRENT LOCKS TO NEW PLAYER
    // ============================================================

    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;
        if (!TutorialActive.Value) return;
        if (currentIndex < 0 || currentIndex >= steps.Length) return;

        var step = Current;
        SendLocksToSpecificClient(clientId, step);
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
        if (!IsServer || !stepActive) return;
        if (currentIndex < 0 || currentIndex >= steps.Length) return;

        TutorialStep step = Current;
        float elapsedHUD = Time.time - hudShownTime;

        // Auto complete
        if (!step.completeOnCondition &&
            step.autoCompleteAfter > 0 &&
            Time.time - stepStartTime >= step.autoCompleteAfter)
        {
            CompleteCurrentStep();
            return;
        }

        // Complete on condition
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

        ClearHUDClientRpc();
        currentIndex++;

        if (currentIndex >= steps.Length)
        {
            TutorialActive.Value = false;
            stepActive = false;
            return;
        }

        stepActive = true;
        conditionSatisfied = false;

        stepStartTime = Time.time;
        hudShownTime = Time.time;

        travellerMoved = navigatorMoved = false;
        travellerLooked = navigatorLooked = false;

        // מחכה פריים אחד — כל האובייקטים יספיקו לעבור Awake
        StartCoroutine(DelayedColliderStep(Current.stepId));

        ApplyLocks(Current);
        ShowStepHUD(Current);
        Current.onStepStart?.Invoke();

        Debug.Log($"[Tutorial] Step started: index={currentIndex} id={Current.stepId} condition={Current.conditionType}");
    }

    private IEnumerator DelayedColliderStep(string id)
    {
        yield return null;
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
    // LOCK SYSTEM – SERVER SIDE
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

    // ============================================================
    // LOCK SYSTEM – CLIENT RPC
    // ============================================================

    [ClientRpc]
    private void ApplyLocksClientRpc(
        bool travellerLockMovement,
        bool travellerLockCamera,
        bool navigatorLockMovement,
        bool navigatorLockCamera,
        ClientRpcParams rpcParams = default)
    {
        // Host = מטייל, Client = נווט
        bool iAmTraveller = IsHost;
        bool iAmNavigator = !IsHost;

        Debug.Log($"[TutorialManager] ApplyLocks on {(IsHost ? "HOST" : "CLIENT")} | " +
                  $"Trav(M:{travellerLockMovement},C:{travellerLockCamera}) " +
                  $"Nav(M:{navigatorLockMovement},C:{navigatorLockCamera})");

        // ---- Movement ----
        foreach (var m in Object.FindObjectsByType<PlayerMovement1P>(FindObjectsSortMode.None))
        {
            if (!m.IsOwner) continue;

            if (iAmTraveller)
            {
                m.SetFrozen(travellerLockMovement);
                Debug.Log("[TutorialManager] Movement lock applied to TRAVELLER local player");
            }
            else if (iAmNavigator)
            {
                m.SetFrozen(navigatorLockMovement);
                Debug.Log("[TutorialManager] Movement lock applied to NAVIGATOR local player");
            }
        }

        // ---- Camera ----
        foreach (var c in Object.FindObjectsByType<PlayerCamera1P>(FindObjectsSortMode.None))
        {
            if (!c.IsOwner) continue;

            if (iAmTraveller)
            {
                c.SetCameraFrozen(travellerLockCamera);
                Debug.Log("[TutorialManager] Camera lock applied to TRAVELLER local player");
            }
            else if (iAmNavigator)
            {
                c.SetCameraFrozen(navigatorLockCamera);
                Debug.Log("[TutorialManager] Camera lock applied to NAVIGATOR local player");
            }
        }
    }

    // ============================================================
    // HUD
    // ============================================================

    private void ShowStepHUD(TutorialStep step)
    {
        SetTravellerHUDMessageClientRpc(step.travellerMessage);
        SetNavigatorHUDMessageClientRpc(step.navigatorMessage);
    }

    [ClientRpc]
    private void ClearHUDClientRpc()
    {
        if (IsHost)
            travellerHUD?.Clear();
        else
            navigatorHUD?.Clear();
    }

    [ClientRpc]
    private void SetTravellerHUDMessageClientRpc(string msg)
    {
        if (IsHost)
            travellerHUD?.ShowMessage(msg);
    }

    [ClientRpc]
    private void SetNavigatorHUDMessageClientRpc(string msg)
    {
        if (!IsHost)
            navigatorHUD?.ShowMessage(msg);
    }

    // ============================================================
    // COMPLETE CURRENT STEP
    // ============================================================

    private void MarkConditionSatisfiedInternal()
    {
        Debug.Log($"[Tutorial] Condition satisfied on step {currentIndex} ({Current.stepId})");
        conditionSatisfied = true;
    }

    private void CompleteCurrentStep()
    {
        if (!IsServer || !stepActive) return;

        Debug.Log($"[Tutorial] Completing step {currentIndex} ({Current.stepId})");
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
    // MOVEMENT + LOOK EVENTS
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

        if (step.conditionType == TutorialConditionType.BothMoved)
        {
            if (travellerMoved && navigatorMoved)
            {
                MarkConditionSatisfiedInternal();
            }
        }
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

        if (step.conditionType == TutorialConditionType.BothLookedAround)
        {
            if (travellerLooked && navigatorLooked)
            {
                MarkConditionSatisfiedInternal();
            }
        }
    }

    public bool IsTutorialRunningForStep(string stepId)
    {
        if (!TutorialActive.Value) return false;

        if (currentIndex < 0 || currentIndex >= steps.Length)
            return false;

        return steps[currentIndex].stepId == stepId;
    }

    // ============================================================
    // CONDITION EVENTS
    // ============================================================

    public void NotifyNavigatorRemovedBomb()
    {
        Debug.Log($"[Tutorial] NotifyNavigatorRemovedBomb received | server={IsServer} stepActive={stepActive} index={currentIndex} cond={(currentIndex >= 0 && currentIndex < steps.Length ? Current.conditionType.ToString() : "N/A")}");
        Check(TutorialConditionType.NavigatorRemoveBomb);
    }

    public void NotifyNavigatorOpenedNormalDoor() => Check(TutorialConditionType.NavigatorOpenNormalDoor);
    public void NotifyNavigatorOpenedPuzzleDoor() => Check(TutorialConditionType.NavigatorOpenPuzzleDoor);
    public void NotifyNavigatorOpenedExitDoor() => Check(TutorialConditionType.NavigatorOpenExitDoor);
    public void NotifyNavigatorPlacedHeart() => Check(TutorialConditionType.NavigatorPlaceHeart);
    public void NotifyNavigatorGaveLifebuoy() => Check(TutorialConditionType.NavigatorGiveLifebuoy);

    public void NotifyTravellerPickedKey() => Check(TutorialConditionType.TravellerPickedKey);
    public void NotifyTravellerPickedHeart() => Check(TutorialConditionType.TravellerPickedHeart);

    public void NotifyPuzzleSolved() => Check(TutorialConditionType.PuzzleSolved);
    public void NotifyBothReachedExit() => Check(TutorialConditionType.BothReachedExit);
    public void NotifyCustomEvent() => Check(TutorialConditionType.CustomEvent);

    private void Check(TutorialConditionType t)
    {
        if (!IsServer || !stepActive) return;

        Debug.Log($"[Tutorial] Check() called with {t} | Current={Current.conditionType}");

        if (Current.conditionType == t)
            MarkConditionSatisfiedInternal();
    }
}
