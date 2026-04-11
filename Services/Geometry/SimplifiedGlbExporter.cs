using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Scenes;
using VERTEX = SharpGLTF.Geometry.VertexTypes.VertexPositionNormal;

namespace DXTnavis.Services.Geometry
{
    /// <summary>
    /// Simplified GLB exporter: merges leaf meshes by hierarchy ancestor,
    /// producing one node per group with PDS metadata as glTF extras.
    /// 93K+ objects → ~hundreds of merged nodes.
    /// </summary>
    public class SimplifiedGlbExporter
    {
        public event EventHandler<int> ProgressChanged;
        public event EventHandler<string> StatusChanged;

        /// <summary>
        /// Auto-detect a merge depth that yields a manageable number of groups (target: 50-1000).
        /// Walks the hierarchy tracking how many distinct ancestors exist at each depth.
        /// </summary>
        public static int AutoDetectMergeDepth(
            Dictionary<Guid, Autodesk.Navisworks.Api.ModelItem> modelItemMap,
            int minGroups = 50, int maxGroups = 1000)
        {
            // Compute depth of each item by walking ancestors
            var depthMap = new Dictionary<Guid, int>();
            var ancestorAtDepth = new Dictionary<int, HashSet<string>>();

            foreach (var kvp in modelItemMap)
            {
                var ancestors = new List<string>();
                var current = kvp.Value;
                while (current != null)
                {
                    ancestors.Add(current.DisplayName ?? current.ClassDisplayName ?? "?");
                    current = current.Parent;
                }
                ancestors.Reverse(); // root first

                int itemDepth = ancestors.Count - 1;
                depthMap[kvp.Key] = itemDepth;

                // Register ancestor paths at each depth level
                for (int d = 0; d < ancestors.Count; d++)
                {
                    if (!ancestorAtDepth.ContainsKey(d))
                        ancestorAtDepth[d] = new HashSet<string>();

                    string pathKey = string.Join("|", ancestors.Take(d + 1));
                    ancestorAtDepth[d].Add(pathKey);
                }
            }

            // Find the deepest level whose group count falls within [minGroups, maxGroups]
            int bestDepth = 2; // fallback
            foreach (var kvp in ancestorAtDepth.OrderBy(x => x.Key))
            {
                int groupCount = kvp.Value.Count;
                if (groupCount >= minGroups && groupCount <= maxGroups)
                {
                    bestDepth = kvp.Key;
                }
                else if (groupCount > maxGroups)
                {
                    break;
                }
                else
                {
                    // Still too few groups, keep going deeper
                    bestDepth = kvp.Key;
                }
            }

            Debug.WriteLine(string.Format("[SimplifiedGlb] AutoDetect: depth={0}, groups={1}",
                bestDepth, ancestorAtDepth.ContainsKey(bestDepth) ? ancestorAtDepth[bestDepth].Count : 0));
            return bestDepth;
        }

        /// <summary>
        /// Build ancestor path up to a given depth for grouping.
        /// Returns the path as "Ancestor0 > Ancestor1 > ... > AncestorN" truncated at mergeDepth.
        /// </summary>
        public static string GetGroupKey(Autodesk.Navisworks.Api.ModelItem item, int mergeDepth)
        {
            var ancestors = new List<string>();
            var current = item;
            while (current != null)
            {
                ancestors.Add(current.DisplayName ?? current.ClassDisplayName ?? "?");
                current = current.Parent;
            }
            ancestors.Reverse(); // root-first

            int takeCount = Math.Min(mergeDepth + 1, ancestors.Count);
            return string.Join(" > ", ancestors.Take(takeCount));
        }

        /// <summary>
        /// Check if a ModelItem is a PDS/SP3D component (piping, equipment, etc.)
        /// by looking for PDS-specific property category names.
        /// </summary>
        public static bool IsPdsComponent(Autodesk.Navisworks.Api.ModelItem item)
        {
            if (item == null) return false;

            foreach (var category in item.PropertyCategories)
            {
                if (category == null) continue;
                string catName = category.DisplayName ?? category.Name ?? "";
                string catInternal = category.Name ?? "";

                // SP3D / PDS property categories
                if (catName.IndexOf("PDS", StringComparison.OrdinalIgnoreCase) >= 0
                    || catName.IndexOf("SP3D", StringComparison.OrdinalIgnoreCase) >= 0
                    || catName.IndexOf("SmartPlant", StringComparison.OrdinalIgnoreCase) >= 0
                    || catName.IndexOf("AVEVA", StringComparison.OrdinalIgnoreCase) >= 0
                    || catName.IndexOf("Piping", StringComparison.OrdinalIgnoreCase) >= 0
                    || catName.IndexOf("Equipment", StringComparison.OrdinalIgnoreCase) >= 0
                    || catInternal.IndexOf("LcRevitData", StringComparison.OrdinalIgnoreCase) >= 0
                    || catInternal.IndexOf("LcSP3DDataDictionaryProperty", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Extract PDS/SP3D metadata from a ModelItem's property categories.
        /// Returns a flat dictionary of key-value pairs.
        /// </summary>
        public static Dictionary<string, string> ExtractPdsMetadata(Autodesk.Navisworks.Api.ModelItem item)
        {
            var metadata = new Dictionary<string, string>();
            if (item == null) return metadata;

            metadata["DisplayName"] = item.DisplayName ?? "";
            metadata["ClassDisplayName"] = item.ClassDisplayName ?? "";

            foreach (var category in item.PropertyCategories)
            {
                if (category == null) continue;
                string catName = category.DisplayName ?? category.Name ?? "";

                Autodesk.Navisworks.Api.DataPropertyCollection properties = null;
                try { properties = category.Properties; } catch { continue; }
                if (properties == null) continue;

                foreach (Autodesk.Navisworks.Api.DataProperty prop in properties)
                {
                    if (prop == null) continue;
                    string propName = prop.DisplayName ?? prop.Name ?? "";
                    string value = "";
                    try
                    {
                        value = prop.Value?.IsDisplayString == true
                            ? prop.Value.ToDisplayString()
                            : prop.Value?.ToString() ?? "";
                    }
                    catch { continue; }

                    if (string.IsNullOrWhiteSpace(value)) continue;

                    string key = string.Format("{0}/{1}", catName, propName);
                    metadata[key] = value;
                }
            }
            return metadata;
        }

        /// <summary>
        /// Merge PDS metadata from multiple items: collect all unique key-value pairs.
        /// For conflicts, join values with "; ".
        /// </summary>
        public static Dictionary<string, string> MergePdsMetadata(
            IEnumerable<Dictionary<string, string>> itemMetadatas)
        {
            var merged = new Dictionary<string, string>();
            foreach (var meta in itemMetadatas)
            {
                foreach (var kvp in meta)
                {
                    if (!merged.ContainsKey(kvp.Key))
                    {
                        merged[kvp.Key] = kvp.Value;
                    }
                    else if (merged[kvp.Key] != kvp.Value)
                    {
                        // Collect distinct values
                        var existing = new HashSet<string>(merged[kvp.Key].Split(new[] { "; " }, StringSplitOptions.RemoveEmptyEntries));
                        existing.Add(kvp.Value);
                        if (existing.Count <= 5) // cap to avoid huge strings
                            merged[kvp.Key] = string.Join("; ", existing);
                    }
                }
            }
            return merged;
        }

        /// <summary>
        /// Export a simplified GLB: group meshes by hierarchy ancestor at mergeDepth,
        /// merge all leaf meshes per group into a single node, attach PDS metadata as extras.
        /// </summary>
        /// <param name="meshEntries">All extracted mesh data with ObjectIds</param>
        /// <param name="modelItemMap">ObjectId → ModelItem mapping</param>
        /// <param name="mergeDepth">Hierarchy depth at which to group (0=root, 1=first child, etc.)</param>
        /// <param name="outputGlbPath">Output .glb path</param>
        /// <param name="outputMetadataPath">Output metadata JSON path (nullable)</param>
        /// <returns>(nodeCount, totalTriangles)</returns>
        public (int nodeCount, int totalTriangles) Export(
            IList<GltfMeshEntry> meshEntries,
            Dictionary<Guid, Autodesk.Navisworks.Api.ModelItem> modelItemMap,
            int mergeDepth,
            string outputGlbPath,
            string outputMetadataPath = null)
        {
            if (meshEntries == null || meshEntries.Count == 0)
                return (0, 0);

            // Step 1: Group mesh entries by ancestor key
            // PDS components are kept as individual nodes (not merged)
            OnStatusChanged("Grouping objects by hierarchy (PDS components kept individual)...");
            var groups = new Dictionary<string, List<GltfMeshEntry>>();
            var groupMetadata = new Dictionary<string, List<Dictionary<string, string>>>();
            int pdsComponentCount = 0;

            foreach (var entry in meshEntries)
            {
                if (entry.MeshData == null || entry.MeshData.VertexCount == 0)
                    continue;

                Autodesk.Navisworks.Api.ModelItem item;
                string groupKey;
                if (modelItemMap.TryGetValue(entry.ObjectId, out item))
                {
                    if (IsPdsComponent(item))
                    {
                        // PDS component: unique key so it stays as its own node
                        groupKey = string.Format("PDS:{0}:{1}",
                            item.DisplayName ?? entry.ObjectId.ToString("D"),
                            entry.ObjectId.ToString("N"));
                        pdsComponentCount++;
                    }
                    else
                    {
                        groupKey = GetGroupKey(item, mergeDepth);
                    }
                }
                else
                {
                    groupKey = "Unknown";
                }

                if (!groups.ContainsKey(groupKey))
                {
                    groups[groupKey] = new List<GltfMeshEntry>();
                    groupMetadata[groupKey] = new List<Dictionary<string, string>>();
                }
                groups[groupKey].Add(entry);

                if (item != null)
                    groupMetadata[groupKey].Add(ExtractPdsMetadata(item));
            }

            int mergedGroupCount = groups.Count - pdsComponentCount;
            OnStatusChanged(string.Format("{0} objects → {1} PDS components (individual) + {2} merged groups",
                meshEntries.Count, pdsComponentCount, mergedGroupCount));

            // Step 2: Build merged scene
            var scene = new SceneBuilder("DXTnavis_Simplified");
            int nodeCount = 0;
            int totalTriangles = 0;
            int groupIndex = 0;
            int totalGroups = groups.Count;

            // Metadata collection for JSON sidecar
            var metadataRecords = new List<Dictionary<string, object>>();

            foreach (var grp in groups)
            {
                string groupName = grp.Key;
                var entries = grp.Value;

                try
                {
                    // Build a single mesh from all entries in this group
                    var meshBuilder = new MeshBuilder<VERTEX>(
                        groupName.Length > 60 ? groupName.Substring(groupName.Length - 60) : groupName);

                    // Each sub-entry keeps its own material (color preservation)
                    int groupTriangles = 0;
                    foreach (var entry in entries)
                    {
                        var meshData = entry.MeshData;
                        bool hasNormals = meshData.Normals.Count > 0
                            && meshData.Normals.Count == meshData.Vertices.Count;

                        MaterialBuilder material;
                        if (meshData.DiffuseColor != null && meshData.DiffuseColor.Length >= 3)
                        {
                            float a = meshData.DiffuseColor.Length >= 4 ? meshData.DiffuseColor[3] : 1.0f;
                            string matName = string.Format("mat_{0}", entry.ObjectId.ToString("N").Substring(0, 8));
                            material = new MaterialBuilder(matName)
                                .WithDoubleSide(true)
                                .WithMetallicRoughnessShader()
                                .WithBaseColor(new Vector4(meshData.DiffuseColor[0], meshData.DiffuseColor[1], meshData.DiffuseColor[2], a))
                                .WithMetallicRoughness(0.1f, 0.5f);
                            if (a < 1.0f)
                                material.WithAlpha(AlphaMode.BLEND);
                        }
                        else
                        {
                            material = new MaterialBuilder("default_" + entry.ObjectId.ToString("N").Substring(0, 4))
                                .WithDoubleSide(true)
                                .WithMetallicRoughnessShader()
                                .WithBaseColor(new Vector4(0.8f, 0.8f, 0.8f, 1.0f))
                                .WithMetallicRoughness(0.1f, 0.5f);
                        }

                        var primitive = meshBuilder.UsePrimitive(material);
                        var verts = meshData.Vertices;
                        var norms = meshData.Normals;
                        var indices = meshData.Indices;

                        for (int t = 0; t + 2 < indices.Count; t += 3)
                        {
                            var v0 = MakeVertex(verts, norms, indices[t], hasNormals);
                            var v1 = MakeVertex(verts, norms, indices[t + 1], hasNormals);
                            var v2 = MakeVertex(verts, norms, indices[t + 2], hasNormals);
                            primitive.AddTriangle(v0, v1, v2);
                            groupTriangles++;
                        }
                    }

                    if (groupTriangles == 0)
                    {
                        groupIndex++;
                        continue;
                    }

                    scene.AddRigidMesh(meshBuilder, Matrix4x4.Identity)
                        .WithName(groupName);

                    totalTriangles += groupTriangles;
                    nodeCount++;

                    // Collect merged metadata for this group
                    Dictionary<string, string> mergedMeta;
                    if (groupMetadata.ContainsKey(groupName) && groupMetadata[groupName].Count > 0)
                    {
                        mergedMeta = MergePdsMetadata(groupMetadata[groupName]);
                    }
                    else
                    {
                        mergedMeta = new Dictionary<string, string>();
                    }

                    var record = new Dictionary<string, object>();
                    record["group"] = groupName;
                    record["objectCount"] = entries.Count;
                    record["triangles"] = groupTriangles;
                    record["metadata"] = mergedMeta;
                    metadataRecords.Add(record);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(string.Format("[SimplifiedGlb] Skip group '{0}': {1}", groupName, ex.Message));
                }

                groupIndex++;
                if (groupIndex % 10 == 0 || groupIndex == totalGroups)
                {
                    int pct = (int)(100.0 * groupIndex / totalGroups);
                    OnProgressChanged(pct);
                    OnStatusChanged(string.Format("Merging groups: {0}/{1}", groupIndex, totalGroups));
                }
            }

            if (nodeCount == 0)
                return (0, 0);

            // Step 3: Save GLB
            OnStatusChanged("Saving simplified GLB...");
            var model = scene.ToGltf2();
            model.Asset.Generator = "DXTnavis Simplified Export (SharpGLTF)";

            model.SaveGLB(outputGlbPath);

            // Step 4: Write metadata JSON sidecar
            if (!string.IsNullOrEmpty(outputMetadataPath))
            {
                WriteMetadataJson(metadataRecords, outputMetadataPath);
            }

            Debug.WriteLine(string.Format("[SimplifiedGlb] Saved {0} merged nodes, {1:N0} triangles → {2}",
                nodeCount, totalTriangles, outputGlbPath));
            return (nodeCount, totalTriangles);
        }

        private void WriteMetadataJson(List<Dictionary<string, object>> records, string path)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[");
            for (int i = 0; i < records.Count; i++)
            {
                var rec = records[i];
                var meta = (Dictionary<string, string>)rec["metadata"];
                sb.AppendLine("  {");
                sb.AppendFormat("    \"group\": {0},\n", EscJson((string)rec["group"]));
                sb.AppendFormat("    \"objectCount\": {0},\n", rec["objectCount"]);
                sb.AppendFormat("    \"triangles\": {0},\n", rec["triangles"]);
                sb.AppendLine("    \"metadata\": {");
                int j = 0;
                foreach (var kv in meta)
                {
                    sb.AppendFormat("      {0}: {1}", EscJson(kv.Key), EscJson(kv.Value));
                    sb.AppendLine(j < meta.Count - 1 ? "," : "");
                    j++;
                }
                sb.AppendLine("    }");
                sb.Append("  }");
                sb.AppendLine(i < records.Count - 1 ? "," : "");
            }
            sb.AppendLine("]");
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }

        private static string EscJson(string s)
        {
            if (s == null) return "null";
            return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                           .Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t") + "\"";
        }

        private static VERTEX MakeVertex(List<float> verts, List<float> norms, int index, bool hasNormals)
        {
            int i3 = index * 3;
            var pos = new Vector3(verts[i3], verts[i3 + 1], verts[i3 + 2]);
            Vector3 normal;
            if (hasNormals)
                normal = new Vector3(norms[i3], norms[i3 + 1], norms[i3 + 2]);
            else
                normal = Vector3.UnitY;
            return new VERTEX(pos, normal);
        }

        private void OnProgressChanged(int percentage)
        {
            ProgressChanged?.Invoke(this, percentage);
        }

        private void OnStatusChanged(string message)
        {
            StatusChanged?.Invoke(this, message);
        }
    }
}
