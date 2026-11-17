using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Witch.Infrastructure.Content;

namespace Witch.Editor
{
    public static class OrderVaultSync
    {
        private sealed class VaultOrder
        {
            public string StableId;
            public string Customer;
            public string FirstHerbId;
            public string SecondHerbId;
            public List<string> Bubbles = new List<string>();
            public string SourceDifficulty; // simple|mid|hard|tutorial
            public string TutorialCategory; // Warming|Cooling
        }

        [MenuItem("Witch/Content/Sync Orders From Vault")]
        public static void SyncFromMenu()
        {
            string report = RunSync();
            Debug.Log(report);
        }

        public static string RunSync()
        {
            var sb = new StringBuilder();
            try
            {
                string projectRoot = Directory.GetParent(UnityEngine.Application.dataPath).FullName;
                string workspaceRoot = Directory.GetParent(projectRoot).FullName;
                string vaultOrders = Path.Combine(workspaceRoot, "Witch Vaults", "Orders");

                var vault = new Dictionary<string, VaultOrder>(StringComparer.Ordinal);
                ParseVaultFile(Path.Combine(vaultOrders, "Simple Orders.md"), "simple", vault);
                ParseVaultFile(Path.Combine(vaultOrders, "Mid Orders.md"), "mid", vault);
                ParseVaultFile(Path.Combine(vaultOrders, "Hard Orders.md"), "hard", vault);
                ParseVaultFile(Path.Combine(vaultOrders, "Tutorial Orders.md"), "tutorial", vault);

                var customerByName = BuildCustomerMap();
                var orderAssets = FindOrderAssets();

                int updated = 0;
                var unexpected = new List<string>();
                var missingInUnity = new List<string>();
                var missingInVault = new List<string>();

                // Update existing 250 Simple/Mid/Hard
                foreach (var kv in vault.Where(k => k.Value.SourceDifficulty != "tutorial").OrderBy(k => k.Key))
                {
                    VaultOrder vo = kv.Value;
                    if (!orderAssets.TryGetValue(vo.StableId, out string assetPath))
                    {
                        missingInUnity.Add(vo.StableId);
                        continue;
                    }
                    OrderDefinition def = AssetDatabase.LoadAssetAtPath<OrderDefinition>(assetPath);
                    if (def == null) { missingInUnity.Add(vo.StableId + " (load failed)"); continue; }

                    var so = new SerializedObject(def);
                    // difficulty remap: Simple->1, Mid->2, Hard->3
                    int expectedDiff = vo.SourceDifficulty == "simple" ? 1 : vo.SourceDifficulty == "mid" ? 2 : 3;
                    so.FindProperty("difficulty").intValue = expectedDiff;
                    // bubbles
                    var bubblesProp = so.FindProperty("orderBubbles");
                    SetStringArray(bubblesProp, vo.Bubbles);
                    // verify preserved fields
                    string first = so.FindProperty("firstHerbId").stringValue;
                    string second = so.FindProperty("secondHerbId").stringValue;
                    string stable = so.FindProperty("stableId").stringValue;
                    if (stable != vo.StableId) unexpected.Add(vo.StableId + ": stableId mismatch");
                    if (first != vo.FirstHerbId) unexpected.Add(vo.StableId + ": firstHerbId " + first + " != " + vo.FirstHerbId);
                    if (second != vo.SecondHerbId) unexpected.Add(vo.StableId + ": secondHerbId " + second + " != " + vo.SecondHerbId);
                    // customer preserved/verified by name
                    var custProp = so.FindProperty("customer");
                    string unityCustName = GetCustomerDisplayName(custProp.objectReferenceValue as CustomerDefinition);
                    if (unityCustName != vo.Customer) unexpected.Add(vo.StableId + ": customer " + unityCustName + " != " + vo.Customer);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(def);
                    updated++;
                }

                // detect existing SMH not in vault
                foreach (var kv in orderAssets)
                {
                    if (kv.Key.StartsWith("order.simple.") || kv.Key.StartsWith("order.mid.") || kv.Key.StartsWith("order.hard."))
                    {
                        if (!vault.ContainsKey(kv.Key)) missingInVault.Add(kv.Key);
                    }
                }

                // Create 40 Tutorial orders
                EnsureFolder("Assets/Content/Orders/Tutorial");
                EnsureFolder("Assets/Content/Orders/Tutorial/Warming");
                EnsureFolder("Assets/Content/Orders/Tutorial/Cooling");

                int created = 0;
                var tutorialDefs = new List<OrderDefinition>();
                foreach (var vo in vault.Values.Where(v => v.SourceDifficulty == "tutorial").OrderBy(v => v.StableId))
                {
                    if (!customerByName.TryGetValue(vo.Customer, out CustomerDefinition cust))
                    {
                        unexpected.Add(vo.StableId + ": missing customer " + vo.Customer);
                        continue;
                    }
                    string folder = vo.TutorialCategory == "Warming" ? "Assets/Content/Orders/Tutorial/Warming" : "Assets/Content/Orders/Tutorial/Cooling";
                    string fileName = TutorialFileName(vo.StableId);
                    string assetPath = folder + "/" + fileName;
                    OrderDefinition def = AssetDatabase.LoadAssetAtPath<OrderDefinition>(assetPath);
                    bool isNew = def == null;
                    if (isNew)
                    {
                        def = ScriptableObject.CreateInstance<OrderDefinition>();
                        AssetDatabase.CreateAsset(def, assetPath);
                    }
                    var so = new SerializedObject(def);
                    so.FindProperty("stableId").stringValue = vo.StableId;
                    so.FindProperty("difficulty").intValue = 0; // Tutorial
                    SetStringArray(so.FindProperty("orderBubbles"), vo.Bubbles);
                    so.FindProperty("firstHerbId").stringValue = vo.FirstHerbId;
                    so.FindProperty("secondHerbId").stringValue = vo.SecondHerbId;
                    so.FindProperty("customer").objectReferenceValue = cust;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(def);
                    tutorialDefs.Add(def);
                    if (isNew) created++; else updated++;
                }

                // Register tutorial orders in catalog
                string catalogPath = "Assets/Content/GameContentCatalog.asset";
                var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalogDefinition>(catalogPath);
                int catalogAdded = 0;
                if (catalog != null)
                {
                    var cso = new SerializedObject(catalog);
                    var ordersProp = cso.FindProperty("orders");
                    var existing = new HashSet<string>(StringComparer.Ordinal);
                    for (int i = 0; i < ordersProp.arraySize; i++)
                    {
                        var od = ordersProp.GetArrayElementAtIndex(i).objectReferenceValue as OrderDefinition;
                        if (od != null) existing.Add(od.StableId);
                    }
                    foreach (var def in tutorialDefs.OrderBy(d => d.StableId))
                    {
                        if (existing.Contains(def.StableId)) continue;
                        int idx = ordersProp.arraySize;
                        ordersProp.InsertArrayElementAtIndex(idx);
                        ordersProp.GetArrayElementAtIndex(idx).objectReferenceValue = def;
                        catalogAdded++;
                    }
                    cso.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(catalog);
                }
                else
                {
                    unexpected.Add("Catalog not found: " + catalogPath);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                sb.AppendLine("ORDER_VAULT_SYNC_OK");
                sb.AppendLine("VAULT_SMH=" + vault.Count(v => v.Value.SourceDifficulty != "tutorial"));
                sb.AppendLine("VAULT_TUTORIAL=" + vault.Count(v => v.Value.SourceDifficulty == "tutorial"));
                sb.AppendLine("UPDATED_EXISTING_SMH=" + updated);
                sb.AppendLine("CREATED_TUTORIAL=" + created);
                sb.AppendLine("CATALOG_ADDED=" + catalogAdded);
                sb.AppendLine("MISSING_IN_UNITY=" + missingInUnity.Count);
                foreach (var m in missingInUnity) sb.AppendLine("  MISS_UNITY: " + m);
                sb.AppendLine("MISSING_IN_VAULT=" + missingInVault.Count);
                foreach (var m in missingInVault) sb.AppendLine("  MISS_VAULT: " + m);
                sb.AppendLine("UNEXPECTED=" + unexpected.Count);
                foreach (var u in unexpected) sb.AppendLine("  UNEXPECTED: " + u);
            }
            catch (Exception ex)
            {
                sb.AppendLine("ORDER_VAULT_SYNC_FAILED");
                sb.AppendLine(ex.ToString());
            }
            return sb.ToString();
        }

        private static string TutorialFileName(string stableId)
        {
            // order.tutorial.w01 -> Order_Tutorial_Warm01.asset, c -> Cool
            var m = Regex.Match(stableId, @"^order\.tutorial\.([wc])(\d+)$");
            if (!m.Success) return "Order_" + stableId.Replace('.', '_') + ".asset";
            string kind = m.Groups[1].Value == "w" ? "Warm" : "Cool";
            return "Order_Tutorial_" + kind + m.Groups[2].Value + ".asset";
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\','/');
            string name = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static void SetStringArray(SerializedProperty prop, List<string> values)
        {
            prop.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
                prop.GetArrayElementAtIndex(i).stringValue = values[i];
        }

        private static Dictionary<string, string> FindOrderAssets()
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string guid in AssetDatabase.FindAssets("t:OrderDefinition", new[] { "Assets/Content/Orders" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var def = AssetDatabase.LoadAssetAtPath<OrderDefinition>(path);
                if (def != null && !string.IsNullOrEmpty(def.StableId)) map[def.StableId] = path;
            }
            return map;
        }

        private static Dictionary<string, CustomerDefinition> BuildCustomerMap()
        {
            var map = new Dictionary<string, CustomerDefinition>(StringComparer.Ordinal);
            foreach (string guid in AssetDatabase.FindAssets("t:CustomerDefinition", new[] { "Assets/Content/Customers" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var def = AssetDatabase.LoadAssetAtPath<CustomerDefinition>(path);
                if (def != null && !string.IsNullOrEmpty(def.DisplayName)) map[def.DisplayName] = def;
            }
            return map;
        }

        private static string GetCustomerDisplayName(CustomerDefinition def)
        {
            return def == null ? "" : def.DisplayName;
        }

        private static void ParseVaultFile(string path, string difficulty, Dictionary<string, VaultOrder> dest)
        {
            if (!File.Exists(path)) return;
            string text = File.ReadAllText(path);
            string[] parts = Regex.Split(text, @"(?=^### )", RegexOptions.Multiline);
            foreach (string part in parts)
            {
                if (!part.StartsWith("### ")) continue;
                var vo = new VaultOrder { SourceDifficulty = difficulty };
                string[] lines = part.Replace("\r\n", "\n").Split('\n');
                bool inB = false;
                foreach (string line in lines)
                {
                    if (line.StartsWith("**Stable Id**:")) vo.StableId = line.Substring("**Stable Id**:".Length).Trim();
                    else if (line.StartsWith("**Customer**:")) vo.Customer = line.Substring("**Customer**:".Length).Trim();
                    else if (line.StartsWith("**First Herb Id**:")) vo.FirstHerbId = line.Substring("**First Herb Id**:".Length).Trim();
                    else if (line.StartsWith("**Second Herb Id**:")) vo.SecondHerbId = line.Substring("**Second Herb Id**:".Length).Trim();
                    else if (line.StartsWith("**Category**:")) vo.TutorialCategory = line.Substring("**Category**:".Length).Trim();
                    else if (line.Trim() == "**Order Bubbles**:") { inB = true; continue; }
                    else if (inB)
                    {
                        if (line.StartsWith("- ")) vo.Bubbles.Add(line.Substring(2));
                        else if (line.Trim() == "") continue;
                        else inB = false;
                    }
                }
                if (!string.IsNullOrEmpty(vo.StableId)) dest[vo.StableId] = vo;
            }
        }
    }
}
