using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Scenes;
using VERTEX = SharpGLTF.Geometry.VertexTypes.VertexPositionNormal;

namespace DXTnavis.Services.Geometry
{
    /// <summary>
    /// SharpGLTF 기반 combined scene GLTF/GLB 내보내기.
    /// 개별 MeshData 객체를 하나의 씬에 named node로 추가하여
    /// 단일 scene.glb / scene.gltf 파일로 출력한다.
    /// </summary>
    public class GltfSceneExporter
    {
        public event EventHandler<int> ProgressChanged;
        public event EventHandler<string> StatusChanged;

        /// <summary>
        /// 여러 MeshData를 하나의 GLTF 씬으로 결합하여 저장한다.
        /// </summary>
        /// <param name="meshEntries">ObjectId → (MeshData, DisplayName) 매핑</param>
        /// <param name="outputPath">출력 경로 (.glb 또는 .gltf)</param>
        /// <returns>추가된 노드 수</returns>
        public int Export(IList<GltfMeshEntry> meshEntries, string outputPath)
        {
            if (meshEntries == null || meshEntries.Count == 0 || string.IsNullOrEmpty(outputPath))
                return 0;

            var scene = new SceneBuilder("DXTnavis_Export");

            // 기본 material (diffuse color 없는 객체용)
            var defaultMaterial = new MaterialBuilder("default")
                .WithDoubleSide(true)
                .WithMetallicRoughnessShader()
                .WithBaseColor(new Vector4(0.8f, 0.8f, 0.8f, 1.0f))
                .WithMetallicRoughness(0.1f, 0.5f);

            int nodeCount = 0;
            int total = meshEntries.Count;

            for (int idx = 0; idx < total; idx++)
            {
                var entry = meshEntries[idx];
                if (entry.MeshData == null || entry.MeshData.VertexCount == 0)
                    continue;

                try
                {
                    var meshData = entry.MeshData;
                    bool hasNormals = meshData.Normals.Count > 0
                        && meshData.Normals.Count == meshData.Vertices.Count;

                    // Material (per-object diffuse color 또는 기본)
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
                        material = defaultMaterial;
                    }

                    // Build mesh primitive
                    var meshBuilder = new MeshBuilder<VERTEX>(entry.NodeName ?? entry.ObjectId.ToString("D"));
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
                    }

                    scene.AddRigidMesh(meshBuilder, Matrix4x4.Identity)
                        .WithName(entry.NodeName ?? entry.ObjectId.ToString("D"));

                    nodeCount++;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(string.Format("[GltfSceneExporter] Skip {0}: {1}",
                        entry.ObjectId, ex.Message));
                }

                if (idx % 100 == 0 || idx == total - 1)
                {
                    int pct = (int)(100.0 * (idx + 1) / total);
                    ProgressChanged?.Invoke(this, pct);
                    StatusChanged?.Invoke(this, string.Format("GLTF scene: {0}/{1} objects", idx + 1, total));
                }
            }

            if (nodeCount == 0)
                return 0;

            // Build and save
            var model = scene.ToGltf2();
            model.Asset.Generator = "DXTnavis (SharpGLTF)";

            bool isGlb = outputPath.EndsWith(".glb", StringComparison.OrdinalIgnoreCase);
            if (isGlb)
                model.SaveGLB(outputPath);
            else
                model.SaveGLTF(outputPath);

            Debug.WriteLine(string.Format("[GltfSceneExporter] Saved {0} nodes → {1}", nodeCount, outputPath));
            return nodeCount;
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
    }

    /// <summary>
    /// GLTF scene export 입력 항목
    /// </summary>
    public class GltfMeshEntry
    {
        public Guid ObjectId { get; set; }
        public string NodeName { get; set; }
        public MeshData MeshData { get; set; }
    }
}
