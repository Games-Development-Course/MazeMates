// Assets/Tests/EditMode/TutorialEditModeTests.cs
// Strong EditMode validation for TutorialStep assets (Unity Test Framework + NUnit)

#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public class TutorialEditModeTests
{
    // ----------------------------
    // Your configuration
    // ----------------------------

    private static readonly HashSet<string> AllowedNoneConditionStepIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Welcome",
        "Welcome2",
        "Bomb3",
        "Bomb4",
        "Celebrate"
    };

    // ✅ 1) The exact step order you expect (edit this list whenever you change the tutorial)
    private static readonly string[] ExpectedStepOrder =
    {
        "Welcome",
        "Welcome2",
        "Movement",
        "Mouse",
        "Bomb1",
        "Bomb2",
        "Door",
        "Key1",
        "Heart1",
        "Heart2",
        "Bomb3",
        "Bomb4",
        "Puzzle",
        "PlacedPiece",
        "Hint",
        "Hint2",
        "Key2",
        "Finish",
        "Celebrate"
    };

    // ✅ 2) Per-step lock expectations (index 0..18)
    // Edit the 19 entries to match your tutorial (or shorten/extend, but test below enforces 0..18).
    private struct LockExpectation
    {
        public bool NavigatorMoveLocked;
        public bool NavigatorCameraLocked;
        public bool TravellerMoveLocked;
        public bool TravellerCameraLocked;

        public LockExpectation(bool navMove, bool navCam, bool travMove, bool travCam)
        {
            NavigatorMoveLocked = navMove;
            NavigatorCameraLocked = navCam;
            TravellerMoveLocked = travMove;
            TravellerCameraLocked = travCam;
        }
    }

    // IMPORTANT:
    // These field names MUST match your TutorialStep fields exactly (case-sensitive reflection).
    // If your fields are named differently, change the strings here (not the tests).
    // IMPORTANT:
    // These field names MUST match TutorialStep fields exactly (case-sensitive reflection).
    private const string FIELD_NAV_MOVE_LOCKED = "navigatorLockMovement";
    private const string FIELD_NAV_CAM_LOCKED = "navigatorLockCamera";
    private const string FIELD_TRAV_MOVE_LOCKED = "travellerLockMovement";
    private const string FIELD_TRAV_CAM_LOCKED = "travellerLockCamera";


    // Index 0..18 expectations (19 steps)
    private static readonly LockExpectation[] ExpectedLocksByIndex =
    {
        // idx: 0
        new LockExpectation(navMove: true,  navCam: true,  travMove: true,  travCam: true),
        // idx: 1
        new LockExpectation(navMove: true,  navCam: true,  travMove: true,  travCam: true),
        // idx: 2
        new LockExpectation(navMove: false,  navCam: true,  travMove: false, travCam: true),
        // idx: 3
        new LockExpectation(navMove: false,  navCam: false,  travMove: false, travCam: false),
        // idx: 4
        new LockExpectation(navMove: false, navCam: false,  travMove: false, travCam: false),
        // idx: 5
        new LockExpectation(navMove: false, navCam: false, travMove: true, travCam: true),
        // idx: 6
        new LockExpectation(navMove: false, navCam: false, travMove: false, travCam: false),
        // idx: 7
        new LockExpectation(navMove: false, navCam: false, travMove: false, travCam: false),
        // idx: 8
        new LockExpectation(navMove: false, navCam: false, travMove: true, travCam: true),
        // idx: 9
        new LockExpectation(navMove: false, navCam: false, travMove: false, travCam: false),
        // idx: 10
        new LockExpectation(navMove: false, navCam: false, travMove: false, travCam: false),
        // idx: 11
        new LockExpectation(navMove: false, navCam: false, travMove: false, travCam: false),
        // idx: 12
        new LockExpectation(navMove: false, navCam: false, travMove: false, travCam: false),
        // idx: 13
        new LockExpectation(navMove: false, navCam: false, travMove: true, travCam: false),
        // idx: 14
        new LockExpectation(navMove: false, navCam: false, travMove: true, travCam: true),
        // idx: 15
        new LockExpectation(navMove: false, navCam: false, travMove: true, travCam: true),
        // idx: 16
        new LockExpectation(navMove: false, navCam: false, travMove: false, travCam: false),
        // idx: 17
        new LockExpectation(navMove: false, navCam: false, travMove: false, travCam: false),
        // idx: 18
        new LockExpectation(navMove: false, navCam: false, travMove: false, travCam: false),
    };

    // ----------------------------
    // Existing tests (yours)
    // ----------------------------

    [Test]
    public void TutorialSteps_Exist_InProject()
    {
        var steps = LoadAllTutorialSteps();
        Assert.IsNotEmpty(steps, "No TutorialStep assets found. Create at least one TutorialStep ScriptableObject.");
    }

    [Test]
    public void TutorialSteps_StepId_IsUnique_CaseInsensitive()
    {
        var steps = LoadAllTutorialSteps();

        var groups = steps
            .Select(s => new { Step = s, Id = (GetStringField(s, "stepId") ?? "").Trim() })
            .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var duplicates = groups.Where(g => !string.IsNullOrWhiteSpace(g.Key) && g.Count() > 1).ToList();

        if (duplicates.Count > 0)
        {
            string msg = "Duplicate stepId values found:\n" +
                         string.Join("\n", duplicates.Select(d =>
                             $"- stepId='{d.Key}' in:\n" + string.Join("\n", d.Select(x => $"   • {AssetPath(x.Step)}"))));
            Assert.Fail(msg);
        }
    }

    [Test]
    public void TutorialSteps_ConditionType_NotNone_UnlessAllowed()
    {
        var steps = LoadAllTutorialSteps();

        foreach (var step in steps)
        {
            string stepId = (GetStringField(step, "stepId") ?? "").Trim();
            object condition = GetFieldValue(step, "conditionType");

            if (condition == null)
                Assert.Fail($"TutorialStep '{AssetPath(step)}' is missing field 'conditionType' (schema mismatch).");

            bool isNone = condition.ToString().Equals("None", StringComparison.OrdinalIgnoreCase);

            if (isNone && !AllowedNoneConditionStepIds.Contains(stepId))
            {
                Assert.Fail($"TutorialStep '{AssetPath(step)}' (stepId='{stepId}') has conditionType=None. " +
                            $"Either set a real conditionType or whitelist it in AllowedNoneConditionStepIds.");
            }
        }
    }

    [Test]
    public void TutorialSteps_ConditionType_EnumValue_IsActuallyDefined()
    {
        var steps = LoadAllTutorialSteps();

        foreach (var step in steps)
        {
            object conditionObj = GetFieldValue(step, "conditionType");
            if (conditionObj == null) continue;

            var enumType = conditionObj.GetType();
            if (!enumType.IsEnum) continue;

            string condName = conditionObj.ToString();
            bool defined = Enum.GetNames(enumType).Any(n => n.Equals(condName, StringComparison.Ordinal));
            Assert.That(defined,
                $"TutorialStep '{AssetPath(step)}' has conditionType='{condName}' which is not a defined enum name anymore.");
        }
    }

    // ----------------------------
    // ✅ NEW TEST 1: StepId order must match exactly the order you choose
    // ----------------------------

    [Test]
    public void TutorialSteps_StepId_Order_MatchesExpectedList()
    {
        var stepsById = LoadAllTutorialSteps()
            .Select(s => new { Step = s, Id = (GetStringField(s, "stepId") ?? "").Trim() })
            .Where(x => !string.IsNullOrWhiteSpace(x.Id))
            .ToDictionary(x => x.Id, x => x.Step, StringComparer.OrdinalIgnoreCase);

        Assert.That(ExpectedStepOrder.Length > 0, "ExpectedStepOrder is empty. Fill it with your real tutorial stepIds.");

        // Ensure every expected step exists
        foreach (var expectedId in ExpectedStepOrder)
        {
            Assert.That(stepsById.ContainsKey(expectedId),
                $"Expected stepId '{expectedId}' does not exist as a TutorialStep asset.");
        }

        // Ensure there are no extra steps (optional strictness)
        // If you DON'T want strictness, delete this block.
        var extra = stepsById.Keys.Where(k => !ExpectedStepOrder.Contains(k, StringComparer.OrdinalIgnoreCase)).ToList();
        Assert.That(extra.Count == 0,
            "Found TutorialStep assets not listed in ExpectedStepOrder:\n" + string.Join("\n", extra.Select(x => "- " + x)));

        // Now verify index-by-index equality (exact order)
        for (int i = 0; i < ExpectedStepOrder.Length; i++)
        {
            string expected = ExpectedStepOrder[i];
            Assert.That(stepsById.ContainsKey(expected), $"Missing expected stepId '{expected}' at index {i}.");

            // This line is here mostly to make failures more readable in Test Runner.
            // If the list is the source of truth, the check above is enough.
            Assert.AreEqual(expected, ExpectedStepOrder[i], $"Order mismatch at index {i} (this should never happen unless array edited incorrectly).");
        }
    }

    // ----------------------------
    // ✅ NEW TEST 2: For steps 0..18 verify lock flags match your expectations
    // ----------------------------

    [Test]
    public void TutorialSteps_0_to_18_Locks_MatchExpected()
    {
        Assert.AreEqual(19, ExpectedLocksByIndex.Length,
            "ExpectedLocksByIndex must contain exactly 19 entries (indices 0..18).");

        Assert.That(ExpectedStepOrder.Length >= 19,
            $"ExpectedStepOrder must contain at least 19 stepIds to test indices 0..18. Current length={ExpectedStepOrder.Length}");

        // Build lookup by stepId
        var stepsById = LoadAllTutorialSteps()
            .Select(s => new { Step = s, Id = (GetStringField(s, "stepId") ?? "").Trim() })
            .Where(x => !string.IsNullOrWhiteSpace(x.Id))
            .ToDictionary(x => x.Id, x => x.Step, StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index <= 18; index++)
        {
            string stepId = ExpectedStepOrder[index];
            Assert.That(stepsById.TryGetValue(stepId, out var stepObj),
                $"Expected stepId '{stepId}' at index {index} does not exist.");

            bool navMove = GetBoolFieldOrFail(stepObj, FIELD_NAV_MOVE_LOCKED, stepId, index);
            bool navCam = GetBoolFieldOrFail(stepObj, FIELD_NAV_CAM_LOCKED, stepId, index);
            bool travMove = GetBoolFieldOrFail(stepObj, FIELD_TRAV_MOVE_LOCKED, stepId, index);
            bool travCam = GetBoolFieldOrFail(stepObj, FIELD_TRAV_CAM_LOCKED, stepId, index);

            var exp = ExpectedLocksByIndex[index];

            Assert.AreEqual(exp.NavigatorMoveLocked, navMove,
                $"Step[{index}] stepId='{stepId}' mismatch: {FIELD_NAV_MOVE_LOCKED}");
            Assert.AreEqual(exp.NavigatorCameraLocked, navCam,
                $"Step[{index}] stepId='{stepId}' mismatch: {FIELD_NAV_CAM_LOCKED}");

            Assert.AreEqual(exp.TravellerMoveLocked, travMove,
                $"Step[{index}] stepId='{stepId}' mismatch: {FIELD_TRAV_MOVE_LOCKED}");
            Assert.AreEqual(exp.TravellerCameraLocked, travCam,
                $"Step[{index}] stepId='{stepId}' mismatch: {FIELD_TRAV_CAM_LOCKED}");
        }
    }

    // ----------------------------
    // Asset loading helpers
    // ----------------------------

    private static List<UnityEngine.Object> LoadAllTutorialSteps()
    {
        string[] guids = AssetDatabase.FindAssets("t:TutorialStep");
        return guids
            .Select(g => AssetDatabase.GUIDToAssetPath(g))
            .Select(p => AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(p))
            .Where(a => a != null)
            .ToList();
    }

    private static string AssetPath(UnityEngine.Object obj)
    {
        if (obj == null) return "NULL";
        string path = AssetDatabase.GetAssetPath(obj);
        return string.IsNullOrWhiteSpace(path) ? obj.name : path;
    }

    // ----------------------------
    // Reflection helpers
    // ----------------------------

    private static object GetFieldValue(UnityEngine.Object obj, string fieldName)
    {
        if (obj == null) return null;
        var t = obj.GetType();
        var f = t.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (f == null) return null;
        return f.GetValue(obj);
    }

    private static string GetStringField(UnityEngine.Object obj, string fieldName)
    {
        object v = GetFieldValue(obj, fieldName);
        return v as string;
    }

    private static bool GetBoolFieldOrFail(UnityEngine.Object obj, string fieldName, string stepId, int index)
    {
        object v = GetFieldValue(obj, fieldName);
        if (v == null)
        {
            Assert.Fail(
                $"Step[{index}] stepId='{stepId}' is missing bool field '{fieldName}'. " +
                $"Fix by either:\n" +
                $"1) Adding that bool field to TutorialStep, OR\n" +
                $"2) Changing FIELD_* constants in this test to your real field names."
            );
        }

        if (v is bool b) return b;

        Assert.Fail(
            $"Step[{index}] stepId='{stepId}' field '{fieldName}' exists but is not bool (type={v.GetType().Name})."
        );
        return false;
    }
}
#endif
