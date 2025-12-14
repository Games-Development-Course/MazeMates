#if UNITY_EDITOR
using System;
using System.Text;
using UnityEditor;
using UnityEngine;
using Unity.Netcode;

public static class FindNetcodePrefabByHash
{
    private const uint TargetHash = 4076429229;

    [MenuItem("Tools/Netcode/Find Prefab By Hash (4076429229)")]
    public static void FindDefault() => Find(TargetHash);

    public static void Find(uint targetHash)
    {
        var guids = AssetDatabase.FindAssets("t:Prefab");
        int foundTotal = 0;

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!prefab)
                continue;

            if (!prefab.TryGetComponent<NetworkObject>(out _))
                continue;

            foundTotal += CountMatches(targetHash, prefab, path, guid);
        }

        if (foundTotal == 0)
        {
            Debug.LogError(
                $"❌ No NetworkObject prefab matched hash {targetHash}. "
                    + $"If this still happens, it might be a SCENE object hash or a prefab generated at runtime / not under Assets/."
            );
        }
        else
        {
            Debug.Log($"✅ Done. Total matches = {foundTotal}");
        }
    }

    private static int CountMatches(uint targetHash, GameObject prefab, string path, string guid)
    {
        int hits = 0;

        void Check(string label, string key)
        {
            if (string.IsNullOrEmpty(key))
                return;

            uint h = XXHash32.Hash(key);
            if (h != targetHash)
                return;

            hits++;
            Debug.Log(
                $"✅ MATCH hash={targetHash} via {label}\nPrefab='{prefab.name}'\nPath={path}\nKey='{key}'",
                prefab
            );

            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
        }

        // 1) prefab name
        Check("prefab.name", prefab.name);

        // 2) asset path
        Check("asset path", path);

        // 3) guid
        Check("guid", guid);

        // 4) GlobalObjectId (Editor-only)
        try
        {
            var goid = GlobalObjectId.GetGlobalObjectIdSlow(prefab);
            string goidStr = goid.ToString();
            Check("GlobalObjectId.ToString()", goidStr);
            Check("GlobalObjectId lower", goidStr.ToLowerInvariant());
        }
        catch
        { /* ignore */
        }

        // 5) some common combos
        Check("name|guid", $"{prefab.name}|{guid}");
        Check("path|guid", $"{path}|{guid}");

        return hits;
    }

    // =========================
    // Public XXHash32 (seed=0)
    // =========================
    private static class XXHash32
    {
        private const uint PRIME32_1 = 2654435761U;
        private const uint PRIME32_2 = 2246822519U;
        private const uint PRIME32_3 = 3266489917U;
        private const uint PRIME32_4 = 668265263U;
        private const uint PRIME32_5 = 374761393U;

        public static uint Hash(string input)
        {
            var data = Encoding.UTF8.GetBytes(input);
            return Hash(data, 0, data.Length, 0);
        }

        private static uint Hash(byte[] data, int offset, int length, uint seed)
        {
            int index = offset;
            int end = offset + length;
            uint h32;

            if (length >= 16)
            {
                int limit = end - 16;

                uint v1 = seed + PRIME32_1 + PRIME32_2;
                uint v2 = seed + PRIME32_2;
                uint v3 = seed + 0;
                uint v4 = seed - PRIME32_1;

                while (index <= limit)
                {
                    v1 = Round(v1, ReadUInt32(data, index));
                    index += 4;
                    v2 = Round(v2, ReadUInt32(data, index));
                    index += 4;
                    v3 = Round(v3, ReadUInt32(data, index));
                    index += 4;
                    v4 = Round(v4, ReadUInt32(data, index));
                    index += 4;
                }

                h32 =
                    RotateLeft(v1, 1) + RotateLeft(v2, 7) + RotateLeft(v3, 12) + RotateLeft(v4, 18);
                h32 = MergeRound(h32, v1);
                h32 = MergeRound(h32, v2);
                h32 = MergeRound(h32, v3);
                h32 = MergeRound(h32, v4);
            }
            else
            {
                h32 = seed + PRIME32_5;
            }

            h32 += (uint)length;

            while (index <= end - 4)
            {
                h32 += ReadUInt32(data, index) * PRIME32_3;
                h32 = RotateLeft(h32, 17) * PRIME32_4;
                index += 4;
            }

            while (index < end)
            {
                h32 += data[index] * PRIME32_5;
                h32 = RotateLeft(h32, 11) * PRIME32_1;
                index++;
            }

            h32 ^= h32 >> 15;
            h32 *= PRIME32_2;
            h32 ^= h32 >> 13;
            h32 *= PRIME32_3;
            h32 ^= h32 >> 16;

            return h32;
        }

        private static uint Round(uint acc, uint input)
        {
            acc += input * PRIME32_2;
            acc = RotateLeft(acc, 13);
            acc *= PRIME32_1;
            return acc;
        }

        private static uint MergeRound(uint acc, uint val)
        {
            val = Round(0, val);
            acc ^= val;
            acc = acc * PRIME32_1 + PRIME32_4;
            return acc;
        }

        private static uint RotateLeft(uint value, int count) =>
            (value << count) | (value >> (32 - count));

        private static uint ReadUInt32(byte[] data, int index) =>
            (uint)(
                data[index]
                | (data[index + 1] << 8)
                | (data[index + 2] << 16)
                | (data[index + 3] << 24)
            );
    }
}
#endif
