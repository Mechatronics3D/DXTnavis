using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.ComApi;
using Autodesk.Navisworks.Api.Interop.ComApi;
using DXTnavis.Models.Geometry;

namespace DXTnavis.Services.Geometry
{
    /// <summary>
    /// Navisworks COM API를 통해 Mesh 데이터(vertices, triangles)를 추출하는 서비스
    /// Phase 15.3: Optional Mesh Export
    ///
    /// 참고: COM API InwOaFragment3.GenerateSimplePrimitives() 사용
    /// </summary>
    public class MeshExtractor : IDisposable
    {
        #region Events

        /// <summary>
        /// 진행률 변경 이벤트 (0-100%)
        /// </summary>
        public event EventHandler<int> ProgressChanged;

        /// <summary>
        /// 상태 메시지 이벤트
        /// </summary>
        public event EventHandler<string> StatusChanged;

        #endregion

        #region Private Fields

        private bool _disposed = false;

        #endregion

        #region Phase 25: Failure Tracking

        /// <summary>
        /// 마지막 ExtractMesh() 호출의 실패 이유 (성공 시 None)
        /// </summary>
        public TessFailureReason LastFailureReason { get; private set; }

        /// <summary>
        /// 마지막 실패 시 추가 진단 정보 (fragment 수, error message 등)
        /// </summary>
        public string LastFailureDetail { get; private set; }

        #endregion

        #region Public Methods

        /// <summary>
        /// 단일 ModelItem에서 Mesh 데이터 추출
        /// </summary>
        /// <param name="item">Navisworks ModelItem</param>
        /// <param name="objectId">객체 고유 ID (파일명용)</param>
        /// <returns>MeshData (실패 시 null)</returns>
        /// <summary>
        /// Extract mesh from a ModelItem.
        /// When includeChildren=true, skips the HasGeometry check so that
        /// container nodes' aggregated child fragments are extracted via COM API.
        /// </summary>
        public MeshData ExtractMesh(ModelItem item, Guid objectId, bool includeChildren = false)
        {
            // Phase 25: Reset failure tracking
            LastFailureReason = TessFailureReason.None;
            LastFailureDetail = null;

            if (item == null || objectId == Guid.Empty)
                return null;

            try
            {
                // ──── Phase 21: 진단 로 깅 ────
                string displayName = item.DisplayName ?? "(unnamed)";
                string classDisplay = item.ClassDisplayName ?? "(no class)";
                bool isLeaf = item.Children == null || !item.Children.Any();
                bool hasGeom = item.HasGeometry;
                bool isHidden = item.IsHidden;

                Debug.WriteLine($"[TESS_ATTEMPT] ObjectId={objectId:D}, Name={displayName}, Class={classDisplay}, IsLeaf={isLeaf}");
                Debug.WriteLine($"[TESS_INFO] HasGeometry={hasGeom}, IsHidden={isHidden}, ClassDisplay={classDisplay}, includeChildren={includeChildren}");

                // Phase 25: HasGeometry=false → 가시적 geometry가 없는 객체
                // includeChildren=true이면 컨테이너 노드도 COM Fragments()로 하위 geometry 추출
                if (!hasGeom && !includeChildren)
                {
                    LastFailureReason = TessFailureReason.NoGeometry;
                    LastFailureDetail = string.Format("Name={0}, Class={1}", displayName, classDisplay);
                    Debug.WriteLine($"[TESS_SKIP] ObjectId={objectId:D} → HasGeometry=false: {displayName}");
                    return null;
                }

                // Phase 25: Hidden 객체 → Navisworks 뷰어에서 안 보이는 객체
                if (isHidden)
                {
                    LastFailureReason = TessFailureReason.Hidden;
                    LastFailureDetail = string.Format("Name={0}, Class={1}", displayName, classDisplay);
                    Debug.WriteLine($"[TESS_SKIP] ObjectId={objectId:D} → IsHidden=true: {displayName}");
                    return null;
                }

                // COM API 상태 가져오기
                var comState = ComApiBridge.State;
                if (comState == null)
                {
                    LastFailureReason = TessFailureReason.ComStateNull;
                    LastFailureDetail = displayName;
                    Debug.WriteLine($"[TESS_FALLBACK] ObjectId={objectId:D} → COM State null");
                    return null;
                }

                // ModelItem을 COM Path로 변환
                var comPath = ComApiBridge.ToInwOaPath(item);
                if (comPath == null)
                {
                    LastFailureReason = TessFailureReason.ComPathFailed;
                    LastFailureDetail = displayName;
                    Debug.WriteLine($"[TESS_FALLBACK] ObjectId={objectId:D} → COM path conversion failed for: {displayName}");
                    return null;
                }

                // Geometry 컬렉션 가져오기
                var meshData = new MeshData
                {
                    ObjectId = objectId,
                    Vertices = new List<float>(),
                    Normals = new List<float>(),
                    Indices = new List<int>()
                };

                // Fragment Callback을 통해 Mesh 추출
                var callback = new MeshCallbackSink(meshData);

                // Phase 19+21: Diagnostic counters
                int fragmentCount = 0;
                int emptyFragmentCount = 0;
                int retrySuccessCount = 0;

                const int MAX_VERTICES = 5000000; // 5M vertices safety cap

                foreach (InwOaFragment3 fragment in comPath.Fragments())
                {
                    if (fragment == null) continue;
                    fragmentCount++;

                    // Safety cap: skip remaining fragments if mesh is already very large
                    if (meshData.Vertices.Count > MAX_VERTICES * 3)
                    {
                        Debug.WriteLine($"[MeshExtractor] {displayName}: vertex cap reached ({meshData.Vertices.Count / 3:N0}), skipping remaining fragments");
                        break;
                    }

                    try
                    {
                        // Phase 19: LCS→WCS 변환 행렬 추출 (per-fragment)
                        var transform = GetFragmentTransform(fragment);

                        // 진단: 첫 fragment의 transform 행렬 출력
                        if (fragmentCount == 1 && transform != null)
                        {
                            Debug.WriteLine($"[MeshExtractor] {displayName} frag0 transform: " +
                                $"[{transform[0]:F4},{transform[1]:F4},{transform[2]:F4},{transform[3]:F4}] " +
                                $"[{transform[4]:F4},{transform[5]:F4},{transform[6]:F4},{transform[7]:F4}] " +
                                $"[{transform[8]:F4},{transform[9]:F4},{transform[10]:F4},{transform[11]:F4}] " +
                                $"[{transform[12]:F4},{transform[13]:F4},{transform[14]:F4},{transform[15]:F4}]");
                        }

                        int vertexCountBefore = meshData.Vertices.Count;
                        int indexCountBefore = meshData.Indices.Count;

                        // ──── Phase 21: Multi-strategy tessellation retry chain ────
                        // Strategy 1: eNORMAL (standard with normals)
                        fragment.GenerateSimplePrimitives(nwEVertexProperty.eNORMAL, callback);

                        // Strategy 2: No vertex properties (parametric primitives fallback)
                        if (meshData.Indices.Count == indexCountBefore)
                        {
                            fragment.GenerateSimplePrimitives((nwEVertexProperty)0, callback);
                        }

                        // Strategy 3: eCOLOR — some fragments respond to color property request
                        if (meshData.Indices.Count == indexCountBefore)
                        {
                            try
                            {
                                fragment.GenerateSimplePrimitives(nwEVertexProperty.eCOLOR, callback);
                                if (meshData.Indices.Count > indexCountBefore)
                                    retrySuccessCount++;
                            }
                            catch { /* eCOLOR not supported for this fragment type */ }
                        }

                        // Strategy 4: eTEX_COORD (0x4) — texture coordinate request triggers tessellation
                        if (meshData.Indices.Count == indexCountBefore)
                        {
                            try
                            {
                                fragment.GenerateSimplePrimitives((nwEVertexProperty)4, callback);
                                if (meshData.Indices.Count > indexCountBefore)
                                    retrySuccessCount++;
                            }
                            catch { /* eTEX_COORD not supported for this fragment type */ }
                        }

                        // Strategy 5: Combined properties (eNORMAL | eCOLOR)
                        if (meshData.Indices.Count == indexCountBefore)
                        {
                            try
                            {
                                fragment.GenerateSimplePrimitives(
                                    nwEVertexProperty.eNORMAL | nwEVertexProperty.eCOLOR, callback);
                                if (meshData.Indices.Count > indexCountBefore)
                                    retrySuccessCount++;
                            }
                            catch { /* Combined properties not supported */ }
                        }

                        // Strategy 6: eNORMAL | eTEX_COORD (0x5)
                        if (meshData.Indices.Count == indexCountBefore)
                        {
                            try
                            {
                                fragment.GenerateSimplePrimitives((nwEVertexProperty)5, callback);
                                if (meshData.Indices.Count > indexCountBefore)
                                    retrySuccessCount++;
                            }
                            catch { /* eNORMAL|eTEX_COORD not supported */ }
                        }

                        // Strategy 7: All flags combined (0x7 = eNORMAL | eCOLOR | eTEX_COORD)
                        if (meshData.Indices.Count == indexCountBefore)
                        {
                            try
                            {
                                fragment.GenerateSimplePrimitives((nwEVertexProperty)7, callback);
                                if (meshData.Indices.Count > indexCountBefore)
                                    retrySuccessCount++;
                            }
                            catch { /* All flags combined not supported */ }
                        }

                        // Diagnostic: 모든 재시도 후에도 빈 fragment 감지
                        if (meshData.Indices.Count == indexCountBefore)
                        {
                            emptyFragmentCount++;
                            Debug.WriteLine($"[TESS_EMPTY_FRAG] ObjectId={objectId:D}, Fragment#{fragmentCount}, Name={displayName}, Class={classDisplay}");
                        }

                        // Phase 19: LCS→WCS 변환 적용
                        int newVertexCount = (meshData.Vertices.Count - vertexCountBefore) / 3;
                        if (transform != null && newVertexCount > 0)
                        {
                            // 진단: 변환 전 범위 (첫 fragment만)
                            if (fragmentCount == 1)
                            {
                                float bMinZ = float.MaxValue, bMaxZ = float.MinValue;
                                for (int vi = vertexCountBefore; vi < meshData.Vertices.Count; vi += 3)
                                {
                                    float vz = meshData.Vertices[vi + 2];
                                    if (vz < bMinZ) bMinZ = vz;
                                    if (vz > bMaxZ) bMaxZ = vz;
                                }
                                Debug.WriteLine($"[MeshExtractor] {displayName} frag0 BEFORE transform Z-range: [{bMinZ:F4}..{bMaxZ:F4}]");
                            }

                            TransformVertices(meshData.Vertices, vertexCountBefore, newVertexCount, transform);

                            // 진단: 변환 후 범위
                            if (fragmentCount == 1)
                            {
                                float aMinZ = float.MaxValue, aMaxZ = float.MinValue;
                                for (int vi = vertexCountBefore; vi < meshData.Vertices.Count; vi += 3)
                                {
                                    float vz = meshData.Vertices[vi + 2];
                                    if (vz < aMinZ) aMinZ = vz;
                                    if (vz > aMaxZ) aMaxZ = vz;
                                }
                                Debug.WriteLine($"[MeshExtractor] {displayName} frag0 AFTER  transform Z-range: [{aMinZ:F4}..{aMaxZ:F4}]");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[MeshExtractor] Fragment error for {displayName}: {ex.Message}");
                    }
                }

                // ──── Phase 21: Comprehensive diagnostic summary ────
                int triangleCount = meshData.Indices.Count / 3;
                Debug.WriteLine($"[TESS_RESULT] ObjectId={objectId:D}, Name={displayName}, " +
                    $"Fragments={fragmentCount}, Empty={emptyFragmentCount}, RetrySuccess={retrySuccessCount}, " +
                    $"Vertices={meshData.Vertices.Count / 3}, Triangles={triangleCount}, Lines={callback.LineCount}");

                // ──── Phase 25: Line→Tube 변환 ────
                // Triangle이 전혀 없고 Line만 있는 경우 → Line을 tube mesh로 변환
                // Triangle이 있는 객체의 Line은 무시 (실제 mesh와 보조 wireframe이므로 중복 방지)
                if (callback.LineCount > 0 && meshData.Indices.Count == 0)
                {
                    callback.ConvertLinesToMesh(0.005f);
                    Debug.WriteLine($"[MeshExtractor] {displayName}: Line→Tube conversion: {callback.LineCount} lines → {meshData.Indices.Count / 3} triangles");
                }

                // Phase 20: Gap mesh — tessellation 실패 fragment의 누락 영역을 item BBox로 보충
                if (emptyFragmentCount > 0 && meshData.Vertices.Count >= 9)
                {
                    try
                    {
                        var itemBBox = item.BoundingBox();
                        if (itemBBox != null && !itemBBox.IsEmpty)
                        {
                            int gapTrisBefore = meshData.Indices.Count;
                            AppendGapMesh(meshData, itemBBox);
                            int gapTris = (meshData.Indices.Count - gapTrisBefore) / 3;
                            if (gapTris > 0)
                            {
                                Debug.WriteLine($"[MeshExtractor] {displayName}: Gap mesh +{gapTris} triangles for {emptyFragmentCount} empty fragments");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[MeshExtractor] Gap mesh error for {displayName}: {ex.Message}");
                    }
                }

                // 최소 삼각형이 있는지 확인
                if (meshData.Vertices.Count < 9 || meshData.Indices.Count < 3)
                {
                    // Phase 25: 실패 이유 분류
                    if (fragmentCount == 0)
                    {
                        LastFailureReason = TessFailureReason.NoFragments;
                        LastFailureDetail = string.Format("Name={0}, Class={1}", displayName, classDisplay);
                    }
                    else
                    {
                        LastFailureReason = TessFailureReason.AllStrategiesFail;
                        LastFailureDetail = string.Format("Name={0}, Class={1}, Fragments={2}, Empty={3}, Lines={4}",
                            displayName, classDisplay, fragmentCount, emptyFragmentCount, callback.LineCount);
                    }
                    Debug.WriteLine($"[TESS_FALLBACK] ObjectId={objectId:D}, Name={displayName} → No valid mesh. Fragments={fragmentCount}, Empty={emptyFragmentCount}, Lines={callback.LineCount}. Reason: {LastFailureReason}");
                    return null;
                }

                // Normal 정규화 (glTF spec: NORMAL must be unit length)
                NormalizeNormals(meshData);

                meshData.VertexCount = meshData.Vertices.Count / 3;
                meshData.TriangleCount = meshData.Indices.Count / 3;

                // ──── Phase 21+25: MeshQuality 메타데이터 설정 ────
                // Phase 25: Line-only fragment → tube mesh 변환된 경우
                if (callback.LineCount > 0 && emptyFragmentCount == fragmentCount
                    && meshData.Indices.Count > 0)
                    meshData.Quality = "line_mesh";
                else if (emptyFragmentCount > 0 && retrySuccessCount > 0)
                    meshData.Quality = "partial_retry_success";
                else if (emptyFragmentCount > 0)
                    meshData.Quality = "gap_supplemented";
                else
                    meshData.Quality = "full_mesh";

                // ──── Material color 추출 (PropertyCategories에서 Material 카테고리 읽기) ────
                try
                {
                    float r = -1, g = -1, b = -1, transparency = 0;
                    foreach (PropertyCategory category in item.PropertyCategories)
                    {
                        if (category == null) continue;
                        string catName = category.DisplayName ?? category.Name ?? "";
                        if (catName != "Material") continue;

                        DataPropertyCollection props = null;
                        try { props = category.Properties; }
                        catch { break; }
                        if (props == null) break;

                        foreach (DataProperty prop in props)
                        {
                            if (prop == null) continue;
                            string pName = prop.DisplayName ?? prop.Name ?? "";
                            try
                            {
                                if (pName == "Diffuse.Red" || pName == "Diffuse Red")
                                    r = Convert.ToSingle(prop.Value.ToDouble());
                                else if (pName == "Diffuse.Green" || pName == "Diffuse Green")
                                    g = Convert.ToSingle(prop.Value.ToDouble());
                                else if (pName == "Diffuse.Blue" || pName == "Diffuse Blue")
                                    b = Convert.ToSingle(prop.Value.ToDouble());
                                else if (pName == "Transparency")
                                    transparency = Convert.ToSingle(prop.Value.ToDouble());
                            }
                            catch { }
                        }
                        break;
                    }
                    if (r >= 0 && g >= 0 && b >= 0)
                    {
                        meshData.DiffuseColor = new float[] { r, g, b, 1.0f - transparency };
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[MeshExtractor] Color extraction failed for {displayName}: {ex.Message}");
                }

                // 진단: vertex 범위 출력 (납작한 판 문제 디버깅)
                {
                    float xMin = float.MaxValue, yMin = float.MaxValue, zMin = float.MaxValue;
                    float xMax = float.MinValue, yMax = float.MinValue, zMax = float.MinValue;
                    for (int i = 0; i < meshData.Vertices.Count; i += 3)
                    {
                        float x = meshData.Vertices[i], y = meshData.Vertices[i + 1], z = meshData.Vertices[i + 2];
                        if (x < xMin) xMin = x; if (x > xMax) xMax = x;
                        if (y < yMin) yMin = y; if (y > yMax) yMax = y;
                        if (z < zMin) zMin = z; if (z > zMax) zMax = z;
                    }
                    Debug.WriteLine($"[MeshExtractor] {displayName}: bounds X[{xMin:F4}..{xMax:F4}] Y[{yMin:F4}..{yMax:F4}] Z[{zMin:F4}..{zMax:F4}] range=({xMax - xMin:F4}, {yMax - yMin:F4}, {zMax - zMin:F4})");
                }

                return meshData;
            }
            catch (COMException comEx)
            {
                LastFailureReason = TessFailureReason.Exception;
                LastFailureDetail = string.Format("COM: {0}", comEx.Message);
                Debug.WriteLine($"[TESS_FALLBACK] ObjectId={objectId:D} → COM Error: {comEx.Message}");
                return null;
            }
            catch (Exception ex)
            {
                LastFailureReason = TessFailureReason.Exception;
                LastFailureDetail = ex.Message;
                Debug.WriteLine($"[TESS_FALLBACK] ObjectId={objectId:D} → Error: {ex.Message}");
                return null;
            }
        }


        /// <summary>
        /// Phase 19: 단일 ModelItem의 mesh 진단 정보 추출 (GLB 생성 없이 통계만)
        /// Parent 노드: fragment 수만 카운트 (tessellation 생략 — Fragments()가 전체 하위를 포함하므로 느림)
        /// Leaf 노드: 실제 tessellation 수행하여 triangle 수 확인
        /// </summary>
        public MeshDiagnosticInfo DiagnoseMesh(ModelItem item)
        {
            var info = new MeshDiagnosticInfo
            {
                DisplayName = item?.DisplayName ?? "(null)",
                IsLeaf = item?.Children == null || !item.Children.Any(),
                HasGeometry = item?.HasGeometry ?? false,
                ChildCount = 0
            };

            if (item == null) return info;

            try { info.ChildCount = item.Children?.Count() ?? 0; } catch { }
            try { info.ClassDisplayName = item.ClassDisplayName ?? ""; } catch { }

            // BBox 정보
            try
            {
                var bbox = item.BoundingBox();
                info.HasBBox = bbox != null && !bbox.IsEmpty;
                if (info.HasBBox)
                {
                    info.BBoxVolume = (bbox.Max.X - bbox.Min.X) *
                                     (bbox.Max.Y - bbox.Min.Y) *
                                     (bbox.Max.Z - bbox.Min.Z);
                }
            }
            catch { }

            // COM API Fragment 분석
            try
            {
                var comPath = ComApiBridge.ToInwOaPath(item);
                if (comPath != null)
                {
                    // Parent 노드: fragment 수만 세기 (Fragments()는 모든 하위 포함 → tessellation하면 freeze)
                    if (!info.IsLeaf)
                    {
                        foreach (InwOaFragment3 fragment in comPath.Fragments())
                        {
                            if (fragment == null) continue;
                            info.FragmentCount++;
                            // fragment 100개 이상이면 카운트만으로 충분
                            if (info.FragmentCount >= 100)
                            {
                                info.LastError = "fragment count capped at 100+ (parent node)";
                                break;
                            }
                        }
                    }
                    else
                    {
                        // Leaf 노드: 실제 tessellation 수행
                        var meshData = new MeshData
                        {
                            Vertices = new List<float>(),
                            Normals = new List<float>(),
                            Indices = new List<int>()
                        };
                        var callback = new MeshCallbackSink(meshData);

                        foreach (InwOaFragment3 fragment in comPath.Fragments())
                        {
                            if (fragment == null) continue;
                            info.FragmentCount++;

                            try
                            {
                                int idxBefore = meshData.Indices.Count;
                                fragment.GenerateSimplePrimitives(nwEVertexProperty.eNORMAL, callback);

                                if (meshData.Indices.Count == idxBefore)
                                    info.EmptyFragmentCount++;

                                // Transform 존재 여부
                                var transform = GetFragmentTransform(fragment);
                                if (transform != null)
                                    info.HasNonIdentityTransform = true;
                            }
                            catch (Exception ex)
                            {
                                info.FragmentErrors++;
                                info.LastError = ex.Message;
                            }
                        }

                        info.TriangleCount = meshData.Indices.Count / 3;
                        info.VertexCount = meshData.Vertices.Count / 3;
                    }
                }
                else
                {
                    info.ComPathFailed = true;
                }
            }
            catch (Exception ex)
            {
                info.LastError = ex.Message;
            }

            return info;
        }

        /// <summary>
        /// Phase 22: Deep Diagnostic — 5개 전략을 개별 테스트하고 Line/Point/SnapPoint도 캡처
        /// 실패 원인을 정확히 식별하기 위한 상세 진단
        /// </summary>
        public MeshDiagnosticInfo DeepDiagnoseMesh(ModelItem item)
        {
            var info = new MeshDiagnosticInfo
            {
                DisplayName = item?.DisplayName ?? "(null)",
                IsLeaf = item?.Children == null || !item.Children.Any(),
                HasGeometry = item?.HasGeometry ?? false,
                ChildCount = 0
            };

            if (item == null) return info;

            try { info.ChildCount = item.Children?.Count() ?? 0; } catch { }
            try { info.ClassDisplayName = item.ClassDisplayName ?? ""; } catch { }
            try { info.IsHidden = item.IsHidden; } catch { }

            // 소스 모델 파일 이름
            try
            {
                if (item.Model != null)
                    info.SourceFile = item.Model.FileName ?? "";
            }
            catch { }

            // 속성 카테고리 목록
            try
            {
                var cats = new List<string>();
                foreach (var cat in item.PropertyCategories)
                {
                    if (cat != null && !string.IsNullOrEmpty(cat.DisplayName))
                        cats.Add(cat.DisplayName);
                }
                info.PropertyCategories = string.Join("|", cats);
            }
            catch { }

            // BBox 정보
            try
            {
                var bbox = item.BoundingBox();
                info.HasBBox = bbox != null && !bbox.IsEmpty;
                if (info.HasBBox)
                {
                    info.BBoxVolume = (bbox.Max.X - bbox.Min.X) *
                                     (bbox.Max.Y - bbox.Min.Y) *
                                     (bbox.Max.Z - bbox.Min.Z);
                }
            }
            catch { }

            // COM API Deep Fragment 분석
            try
            {
                var comPath = ComApiBridge.ToInwOaPath(item);
                if (comPath == null)
                {
                    info.ComPathFailed = true;
                    return info;
                }

                // Parent 노드: fragment 수만 세기
                if (!info.IsLeaf)
                {
                    foreach (InwOaFragment3 fragment in comPath.Fragments())
                    {
                        if (fragment == null) continue;
                        info.FragmentCount++;
                        if (info.FragmentCount >= 100)
                        {
                            info.LastError = "fragment count capped at 100+ (parent node)";
                            break;
                        }
                    }
                    return info;
                }

                // ──── Leaf 노드: 5개 전략 개별 테스트 ────
                var diagCallback = new DiagnosticCallbackSink();
                var strategyTriangles = new int[5];
                var strategyLines = new int[5];

                // nwEVertexProperty 전략 배열
                var strategies = new[]
                {
                    nwEVertexProperty.eNORMAL,                                    // S1: eNORMAL
                    (nwEVertexProperty)0,                                          // S2: None
                    nwEVertexProperty.eCOLOR,                                      // S3: eCOLOR
                    (nwEVertexProperty)2,                                           // S4: flag 2
                    nwEVertexProperty.eNORMAL | nwEVertexProperty.eCOLOR           // S5: combined
                };

                foreach (InwOaFragment3 fragment in comPath.Fragments())
                {
                    if (fragment == null) continue;
                    info.FragmentCount++;

                    // Fragment COM 타입명 캡처 (첫 fragment만)
                    if (info.FragmentCount == 1)
                    {
                        try { info.FragmentTypeName = fragment.GetType().FullName ?? fragment.GetType().Name; }
                        catch { info.FragmentTypeName = "unknown"; }
                    }

                    // Transform 확인
                    try
                    {
                        var transform = GetFragmentTransform(fragment);
                        if (transform != null)
                            info.HasNonIdentityTransform = true;
                    }
                    catch { }

                    // 5개 전략 개별 테스트
                    bool anyTriangles = false;
                    for (int si = 0; si < strategies.Length; si++)
                    {
                        diagCallback.Reset();
                        try
                        {
                            fragment.GenerateSimplePrimitives(strategies[si], diagCallback);
                            strategyTriangles[si] += diagCallback.TriangleCount;
                            strategyLines[si] += diagCallback.LineCount;
                            info.PointCount += diagCallback.PointCount;
                            info.SnapPointCount += diagCallback.SnapPointCount;

                            if (diagCallback.TriangleCount > 0)
                                anyTriangles = true;
                        }
                        catch (Exception ex)
                        {
                            info.FragmentErrors++;
                            info.LastError = string.Format("S{0}:{1}", si + 1, ex.Message);
                        }
                    }

                    if (!anyTriangles)
                        info.EmptyFragmentCount++;
                }

                // 최대 삼각형/라인 수 집계 (가장 좋은 전략 기준)
                for (int si = 0; si < 5; si++)
                {
                    if (strategyTriangles[si] > info.TriangleCount)
                    {
                        info.TriangleCount = strategyTriangles[si];
                        info.VertexCount = strategyTriangles[si] * 3;
                    }
                    info.LineCount += strategyLines[si];
                }
                // Line은 전략별 합산 대신 최대값으로 (같은 line이 반복될 수 있음)
                int maxLines = 0;
                for (int si = 0; si < 5; si++)
                {
                    if (strategyLines[si] > maxLines) maxLines = strategyLines[si];
                }
                info.LineCount = maxLines;

                info.StrategyResults = string.Format("[{0},{1},{2},{3},{4}]",
                    strategyTriangles[0], strategyTriangles[1], strategyTriangles[2],
                    strategyTriangles[3], strategyTriangles[4]);
                info.StrategyLineResults = string.Format("[{0},{1},{2},{3},{4}]",
                    strategyLines[0], strategyLines[1], strategyLines[2],
                    strategyLines[3], strategyLines[4]);
            }
            catch (Exception ex)
            {
                info.LastError = ex.Message;
            }

            return info;
        }

        /// <summary>
        /// 여러 ModelItem에서 Mesh 데이터 일괄 추출
        /// </summary>
        /// <param name="items">ModelItem과 ObjectId 쌍</param>
        /// <param name="cancellationToken">취소 토큰</param>
        /// <returns>ObjectId → MeshData 딕셔너리</returns>
        public Dictionary<Guid, MeshData> ExtractMeshes(
            IEnumerable<(ModelItem item, Guid objectId)> items,
            CancellationToken cancellationToken = default)
        {
            var result = new Dictionary<Guid, MeshData>();
            var itemList = items.ToList();
            int total = itemList.Count;
            int processed = 0;
            int successful = 0;

            OnStatusChanged($"Mesh 추출 시작: {total:N0}개 객체");
            var sw = Stopwatch.StartNew();

            foreach (var (item, objectId) in itemList)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    OnStatusChanged("Mesh 추출 취소됨");
                    break;
                }

                var meshData = ExtractMesh(item, objectId);
                if (meshData != null)
                {
                    result[objectId] = meshData;
                    successful++;
                }

                processed++;

                // 50개마다 진행률 보고
                if (processed % 50 == 0 || processed == total)
                {
                    int percentage = (int)(100.0 * processed / total);
                    OnProgressChanged(percentage);
                    OnStatusChanged($"Mesh 추출 중: {processed:N0}/{total:N0} ({successful:N0}개 성공)");
                }
            }

            sw.Stop();
            OnProgressChanged(100);
            OnStatusChanged($"Mesh 추출 완료: {successful:N0}개 ({sw.Elapsed.TotalSeconds:F1}초)");

            return result;
        }

        /// <summary>
        /// MeshData를 GLB 파일로 저장
        /// </summary>
        /// <param name="meshData">Mesh 데이터</param>
        /// <param name="outputPath">출력 파일 경로 (.glb)</param>
        /// <returns>저장 성공 여부</returns>
        public bool SaveToGlb(MeshData meshData, string outputPath)
        {
            if (meshData == null || string.IsNullOrEmpty(outputPath))
                return false;

            try
            {
                // GLB 파일 형식으로 저장 (간단한 구현)
                // 실제로는 glTF-CSharp-Loader 또는 SharpGLTF 라이브러리 사용 권장
                using (var fs = new FileStream(outputPath, FileMode.Create))
                using (var bw = new BinaryWriter(fs))
                {
                    WriteGlbHeader(bw, meshData);
                }

                // Phase 31: GLB 최소 유효성 검증 — GLB 헤더(12 bytes) + 최소 chunk
                var fi = new FileInfo(outputPath);
                if (!fi.Exists || fi.Length < 100)
                {
                    Debug.WriteLine(string.Format("[MeshExtractor] GLB too small or missing: {0} ({1} bytes)",
                        outputPath, fi.Exists ? fi.Length : 0));
                    if (fi.Exists) fi.Delete();
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MeshExtractor] GLB save error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// MeshData를 OBJ 파일로 저장 (간단한 대안 포맷)
        /// </summary>
        /// <param name="meshData">Mesh 데이터</param>
        /// <param name="outputPath">출력 파일 경로 (.obj)</param>
        /// <returns>저장 성공 여부</returns>
        public bool SaveToObj(MeshData meshData, string outputPath)
        {
            if (meshData == null || string.IsNullOrEmpty(outputPath))
                return false;

            try
            {
                using (var sw = new StreamWriter(outputPath))
                {
                    sw.WriteLine($"# DXTnavis Mesh Export");
                    sw.WriteLine($"# ObjectId: {meshData.ObjectId}");
                    sw.WriteLine($"# Vertices: {meshData.VertexCount}");
                    sw.WriteLine($"# Triangles: {meshData.TriangleCount}");
                    sw.WriteLine();

                    // Vertices
                    for (int i = 0; i < meshData.Vertices.Count; i += 3)
                    {
                        sw.WriteLine($"v {meshData.Vertices[i]:F6} {meshData.Vertices[i + 1]:F6} {meshData.Vertices[i + 2]:F6}");
                    }

                    sw.WriteLine();

                    // Normals (only if count matches vertices — Phase 20 retry can cause mismatch)
                    bool hasValidNormals = meshData.Normals.Count > 0
                        && meshData.Normals.Count == meshData.Vertices.Count;

                    if (hasValidNormals)
                    {
                        for (int i = 0; i < meshData.Normals.Count; i += 3)
                        {
                            sw.WriteLine($"vn {meshData.Normals[i]:F6} {meshData.Normals[i + 1]:F6} {meshData.Normals[i + 2]:F6}");
                        }
                        sw.WriteLine();
                    }

                    // Faces (1-indexed)
                    for (int i = 0; i < meshData.Indices.Count; i += 3)
                    {
                        int i1 = meshData.Indices[i] + 1;
                        int i2 = meshData.Indices[i + 1] + 1;
                        int i3 = meshData.Indices[i + 2] + 1;

                        if (hasValidNormals)
                        {
                            sw.WriteLine($"f {i1}//{i1} {i2}//{i2} {i3}//{i3}");
                        }
                        else
                        {
                            sw.WriteLine($"f {i1} {i2} {i3}");
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MeshExtractor] OBJ save error: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Phase 19: LCS→WCS Transform & BBox Fallback

        /// <summary>
        /// Fragment에서 Local-to-World 변환 행렬 추출
        /// InwLTransform3f COM 객체 → 4x4 float[]
        /// Note: GetLocalToWorldMatrix()는 InwLTransform3f COM 객체를 반환.
        /// PIA typed cast 실패하므로 late-binding (InvokeMember)으로 Matrix 접근.
        /// </summary>
        private static float[] GetFragmentTransform(InwOaFragment3 fragment)
        {
            try
            {
                var transformObj = fragment.GetLocalToWorldMatrix();
                if (transformObj == null) return null;

                // Step 1: 직접 Array인지 시도 (혹시 직접 배열 반환하는 경우)
                Array matrix = transformObj as Array;

                // Step 2: COM 객체면 Matrix 속성을 late-binding으로 접근
                if (matrix == null)
                {
                    try
                    {
                        var matrixData = transformObj.GetType().InvokeMember(
                            "Matrix",
                            System.Reflection.BindingFlags.GetProperty,
                            null, transformObj, null);
                        matrix = matrixData as Array;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[MeshExtractor] Transform Matrix property failed: {ex.Message}");
                    }
                }

                // Step 3: 그래도 실패하면 다른 속성명 시도
                if (matrix == null)
                {
                    try
                    {
                        // InwLTransform3f의 다른 가능한 접근 방식
                        var matrixData = transformObj.GetType().InvokeMember(
                            "GetMatrix",
                            System.Reflection.BindingFlags.InvokeMethod,
                            null, transformObj, null);
                        matrix = matrixData as Array;
                    }
                    catch { }
                }

                if (matrix == null || matrix.Length < 16)
                {
                    Debug.WriteLine($"[MeshExtractor] Transform extraction failed: obj type={transformObj.GetType().Name}, isArray={transformObj is Array}");
                    return null;
                }

                var result = new float[16];
                int lb = matrix.GetLowerBound(0);
                for (int i = 0; i < 16; i++)
                    result[i] = Convert.ToSingle(matrix.GetValue(lb + i));

                // Identity 행렬 검사: 변환 불필요한 경우 null 반환 (최적화)
                bool isIdentity = true;
                for (int i = 0; i < 16; i++)
                {
                    float expected = (i % 5 == 0) ? 1.0f : 0.0f;
                    if (Math.Abs(result[i] - expected) > 1e-6f)
                    {
                        isIdentity = false;
                        break;
                    }
                }
                return isIdentity ? null : result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MeshExtractor] GetFragmentTransform exception: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Normal 벡터를 단위 길이로 정규화 (glTF spec 요구사항)
        /// </summary>
        private static void NormalizeNormals(MeshData meshData)
        {
            if (meshData.Normals == null || meshData.Normals.Count == 0) return;

            for (int i = 0; i < meshData.Normals.Count; i += 3)
            {
                float nx = meshData.Normals[i];
                float ny = meshData.Normals[i + 1];
                float nz = meshData.Normals[i + 2];
                float len = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
                if (len > 1e-8f)
                {
                    meshData.Normals[i] = nx / len;
                    meshData.Normals[i + 1] = ny / len;
                    meshData.Normals[i + 2] = nz / len;
                }
            }
        }

        /// <summary>
        /// LCS 정점들을 WCS로 변환 (4x4 행렬 곱)
        /// </summary>
        private static void TransformVertices(List<float> vertices, int startIdx, int count, float[] matrix)
        {
            for (int i = startIdx; i < startIdx + count * 3; i += 3)
            {
                float x = vertices[i], y = vertices[i + 1], z = vertices[i + 2];
                vertices[i] = matrix[0] * x + matrix[4] * y + matrix[8] * z + matrix[12];
                vertices[i + 1] = matrix[1] * x + matrix[5] * y + matrix[9] * z + matrix[13];
                vertices[i + 2] = matrix[2] * x + matrix[6] * y + matrix[10] * z + matrix[14];
            }
        }

        /// <summary>
        /// BoundingBox에서 12-triangle box mesh 생성 (tessellation 실패 시 fallback)
        /// 24 vertices (face당 4개, 법선 방향 다름), 36 indices (12 triangles)
        /// </summary>
        /// <param name="bbox">BBox3D 경계 상자</param>
        /// <param name="objectId">객체 고유 ID</param>
        /// <returns>MeshData (실패 시 null)</returns>
        public static MeshData GenerateBoxMesh(BBox3D bbox, Guid objectId)
        {
            if (bbox == null || !bbox.IsValid || bbox.IsEmpty) return null;

            var mesh = new MeshData
            {
                ObjectId = objectId,
                Vertices = new List<float>(72),  // 24 verts × 3
                Normals = new List<float>(72),
                Indices = new List<int>(36)       // 12 tris × 3
            };

            float x0 = (float)bbox.Min.X, y0 = (float)bbox.Min.Y, z0 = (float)bbox.Min.Z;
            float x1 = (float)bbox.Max.X, y1 = (float)bbox.Max.Y, z1 = (float)bbox.Max.Z;

            // 6 faces: each has 4 vertices and 2 triangles
            // Face layout: (v0,v1,v2,v3) → tri(0,1,2), tri(0,2,3)
            // Normal directions: +X, -X, +Y, -Y, +Z, -Z

            float[][] faceVerts = new float[][]
            {
                // +X face
                new float[]{ x1,y0,z0, x1,y1,z0, x1,y1,z1, x1,y0,z1 },
                // -X face
                new float[]{ x0,y0,z1, x0,y1,z1, x0,y1,z0, x0,y0,z0 },
                // +Y face
                new float[]{ x0,y1,z0, x0,y1,z1, x1,y1,z1, x1,y1,z0 },
                // -Y face
                new float[]{ x0,y0,z1, x0,y0,z0, x1,y0,z0, x1,y0,z1 },
                // +Z face
                new float[]{ x0,y0,z1, x1,y0,z1, x1,y1,z1, x0,y1,z1 },
                // -Z face
                new float[]{ x1,y0,z0, x0,y0,z0, x0,y1,z0, x1,y1,z0 },
            };

            float[][] faceNormals = new float[][]
            {
                new float[]{ 1,0,0 }, new float[]{-1,0,0 },
                new float[]{ 0,1,0 }, new float[]{ 0,-1,0 },
                new float[]{ 0,0,1 }, new float[]{ 0,0,-1 },
            };

            for (int face = 0; face < 6; face++)
            {
                int baseIdx = face * 4;
                var verts = faceVerts[face];
                var normal = faceNormals[face];

                // 4 vertices per face
                for (int v = 0; v < 4; v++)
                {
                    mesh.Vertices.Add(verts[v * 3]);
                    mesh.Vertices.Add(verts[v * 3 + 1]);
                    mesh.Vertices.Add(verts[v * 3 + 2]);

                    mesh.Normals.Add(normal[0]);
                    mesh.Normals.Add(normal[1]);
                    mesh.Normals.Add(normal[2]);
                }

                // 2 triangles: (0,1,2), (0,2,3)
                mesh.Indices.Add(baseIdx);
                mesh.Indices.Add(baseIdx + 1);
                mesh.Indices.Add(baseIdx + 2);

                mesh.Indices.Add(baseIdx);
                mesh.Indices.Add(baseIdx + 2);
                mesh.Indices.Add(baseIdx + 3);
            }

            mesh.VertexCount = 24;
            mesh.TriangleCount = 12;

            return mesh;
        }

        /// <summary>
        /// Phase 20: Tessellation 실패 fragment의 누락 영역을 BBox 비교로 보충
        /// 기존 vertex AABB와 item BBox를 비교하여 미커버 영역에 box slab mesh 추가
        /// (dome, dish head 등 파라메트릭 프리미티브가 실패한 영역 커버)
        /// </summary>
        private static void AppendGapMesh(MeshData meshData, BoundingBox3D itemBBox)
        {
            // 기존 vertex의 AABB 계산
            float vMinX = float.MaxValue, vMinY = float.MaxValue, vMinZ = float.MaxValue;
            float vMaxX = float.MinValue, vMaxY = float.MinValue, vMaxZ = float.MinValue;

            for (int i = 0; i < meshData.Vertices.Count; i += 3)
            {
                float x = meshData.Vertices[i], y = meshData.Vertices[i + 1], z = meshData.Vertices[i + 2];
                if (x < vMinX) vMinX = x; if (x > vMaxX) vMaxX = x;
                if (y < vMinY) vMinY = y; if (y > vMaxY) vMaxY = y;
                if (z < vMinZ) vMinZ = z; if (z > vMaxZ) vMaxZ = z;
            }

            // Item BBox (Navisworks API BoundingBox3D)
            float iMinX = (float)itemBBox.Min.X, iMinY = (float)itemBBox.Min.Y, iMinZ = (float)itemBBox.Min.Z;
            float iMaxX = (float)itemBBox.Max.X, iMaxY = (float)itemBBox.Max.Y, iMaxZ = (float)itemBBox.Max.Z;

            // 각 축별 gap threshold: 축 길이의 1% 또는 최소 0.01 단위 (미터 기준)
            float dimX = iMaxX - iMinX, dimY = iMaxY - iMinY, dimZ = iMaxZ - iMinZ;
            float threshX = Math.Max(dimX * 0.01f, 0.01f);
            float threshY = Math.Max(dimY * 0.01f, 0.01f);
            float threshZ = Math.Max(dimZ * 0.01f, 0.01f);

            // 6방향 gap slab 생성: item BBox가 vertex AABB를 넘어서는 영역마다 box mesh 추가
            // +X cap (item이 vertex보다 +X 방향으로 더 큰 경우)
            if (iMaxX - vMaxX > threshX)
                AppendBoxToMesh(meshData, vMaxX, iMinY, iMinZ, iMaxX, iMaxY, iMaxZ);
            // -X cap
            if (vMinX - iMinX > threshX)
                AppendBoxToMesh(meshData, iMinX, iMinY, iMinZ, vMinX, iMaxY, iMaxZ);
            // +Y cap
            if (iMaxY - vMaxY > threshY)
                AppendBoxToMesh(meshData, iMinX, vMaxY, iMinZ, iMaxX, iMaxY, iMaxZ);
            // -Y cap
            if (vMinY - iMinY > threshY)
                AppendBoxToMesh(meshData, iMinX, iMinY, iMinZ, iMaxX, vMinY, iMaxZ);
            // +Z cap
            if (iMaxZ - vMaxZ > threshZ)
                AppendBoxToMesh(meshData, iMinX, iMinY, vMaxZ, iMaxX, iMaxY, iMaxZ);
            // -Z cap
            if (vMinZ - iMinZ > threshZ)
                AppendBoxToMesh(meshData, iMinX, iMinY, iMinZ, iMaxX, iMaxY, vMinZ);
        }

        /// <summary>
        /// Phase 20: 기존 MeshData에 axis-aligned box mesh를 추가 (24 vertices, 12 triangles)
        /// gap slab 생성에 사용
        /// </summary>
        private static void AppendBoxToMesh(MeshData meshData,
            float x0, float y0, float z0, float x1, float y1, float z1)
        {
            // Degenerate box 방지 (너비/높이/깊이가 0에 가까우면 생성 불가)
            if (Math.Abs(x1 - x0) < 1e-6f || Math.Abs(y1 - y0) < 1e-6f || Math.Abs(z1 - z0) < 1e-6f)
                return;

            int baseVertex = meshData.Vertices.Count / 3;

            float[][] faceVerts = new float[][]
            {
                new float[]{ x1,y0,z0, x1,y1,z0, x1,y1,z1, x1,y0,z1 }, // +X
                new float[]{ x0,y0,z1, x0,y1,z1, x0,y1,z0, x0,y0,z0 }, // -X
                new float[]{ x0,y1,z0, x0,y1,z1, x1,y1,z1, x1,y1,z0 }, // +Y
                new float[]{ x0,y0,z1, x0,y0,z0, x1,y0,z0, x1,y0,z1 }, // -Y
                new float[]{ x0,y0,z1, x1,y0,z1, x1,y1,z1, x0,y1,z1 }, // +Z
                new float[]{ x1,y0,z0, x0,y0,z0, x0,y1,z0, x1,y1,z0 }, // -Z
            };

            float[][] faceNormals = new float[][]
            {
                new float[]{ 1,0,0 }, new float[]{-1,0,0 },
                new float[]{ 0,1,0 }, new float[]{ 0,-1,0 },
                new float[]{ 0,0,1 }, new float[]{ 0,0,-1 },
            };

            for (int face = 0; face < 6; face++)
            {
                int baseIdx = baseVertex + face * 4;
                var verts = faceVerts[face];
                var normal = faceNormals[face];

                for (int v = 0; v < 4; v++)
                {
                    meshData.Vertices.Add(verts[v * 3]);
                    meshData.Vertices.Add(verts[v * 3 + 1]);
                    meshData.Vertices.Add(verts[v * 3 + 2]);

                    meshData.Normals.Add(normal[0]);
                    meshData.Normals.Add(normal[1]);
                    meshData.Normals.Add(normal[2]);
                }

                meshData.Indices.Add(baseIdx);
                meshData.Indices.Add(baseIdx + 1);
                meshData.Indices.Add(baseIdx + 2);

                meshData.Indices.Add(baseIdx);
                meshData.Indices.Add(baseIdx + 2);
                meshData.Indices.Add(baseIdx + 3);
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 간단한 GLB 헤더 작성 (최소 구현)
        /// </summary>
        private void WriteGlbHeader(BinaryWriter bw, MeshData meshData)
        {
            // GLB Magic: glTF
            bw.Write(0x46546C67); // "glTF"

            // Version 2
            bw.Write((uint)2);

            // 전체 길이 (나중에 업데이트)
            long lengthPos = bw.BaseStream.Position;
            bw.Write((uint)0);

            // JSON chunk
            string json = CreateMinimalGltfJson(meshData);
            byte[] jsonBytes = System.Text.Encoding.UTF8.GetBytes(json);

            // Padding to 4-byte alignment
            int jsonPadding = (4 - (jsonBytes.Length % 4)) % 4;

            bw.Write((uint)(jsonBytes.Length + jsonPadding));
            bw.Write(0x4E4F534A); // "JSON"
            bw.Write(jsonBytes);
            for (int i = 0; i < jsonPadding; i++) bw.Write((byte)0x20);

            // Binary chunk (vertices + indices)
            var binData = CreateBinaryBuffer(meshData);
            int binPadding = (4 - (binData.Length % 4)) % 4;

            bw.Write((uint)(binData.Length + binPadding));
            bw.Write(0x004E4942); // "BIN\0"
            bw.Write(binData);
            for (int i = 0; i < binPadding; i++) bw.Write((byte)0x00);

            // Update total length
            long endPos = bw.BaseStream.Position;
            bw.BaseStream.Position = lengthPos;
            bw.Write((uint)endPos);
        }

        /// <summary>
        /// glTF JSON 생성 (Normal 포함 시 smooth shading 지원)
        /// </summary>
        private string CreateMinimalGltfJson(MeshData meshData)
        {
            int vertexByteLength = meshData.Vertices.Count * sizeof(float);
            int indexByteLength = meshData.Indices.Count * sizeof(int);
            bool hasNormals = meshData.Normals.Count > 0
                && meshData.Normals.Count == meshData.Vertices.Count;
            int normalByteLength = hasNormals ? meshData.Normals.Count * sizeof(float) : 0;

            // glTF spec: POSITION accessor MUST have min/max
            // Z-up (Navisworks) → Y-up (glTF): gX = nX, gY = nZ, gZ = -nY
            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;

            for (int i = 0; i < meshData.Vertices.Count; i += 3)
            {
                float x = meshData.Vertices[i];
                float y = meshData.Vertices[i + 2];      // NW Z → glTF Y
                float z = -meshData.Vertices[i + 1];     // -NW Y → glTF Z
                if (x < minX) minX = x; if (x > maxX) maxX = x;
                if (y < minY) minY = y; if (y > maxY) maxY = y;
                if (z < minZ) minZ = z; if (z > maxZ) maxZ = z;
            }

            // G9 format: float round-trip precision to avoid ACCESSOR_MIN_MISMATCH
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            string sMinX = minX.ToString("G9", inv);
            string sMinY = minY.ToString("G9", inv);
            string sMinZ = minZ.ToString("G9", inv);
            string sMaxX = maxX.ToString("G9", inv);
            string sMaxY = maxY.ToString("G9", inv);
            string sMaxZ = maxZ.ToString("G9", inv);

            int totalBufferLength = vertexByteLength + normalByteLength + indexByteLength;
            int indexBufferOffset = vertexByteLength + normalByteLength;

            // Material color
            string materialSection = "";
            string materialRef = "";
            if (meshData.DiffuseColor != null)
            {
                materialSection = string.Format(inv,
                    @",""materials"": [{{ ""pbrMetallicRoughness"": {{ ""baseColorFactor"": [{0}, {1}, {2}, {3}], ""metallicFactor"": 0.1, ""roughnessFactor"": 0.7 }} }}]",
                    meshData.DiffuseColor[0].ToString("F4", inv),
                    meshData.DiffuseColor[1].ToString("F4", inv),
                    meshData.DiffuseColor[2].ToString("F4", inv),
                    meshData.DiffuseColor[3].ToString("F4", inv));
                materialRef = @", ""material"": 0";
            }

            if (hasNormals)
            {
                // With normals: 3 bufferViews, 3 accessors, POSITION + NORMAL attributes
                return string.Format(inv,
@"{{
  ""asset"": {{ ""version"": ""2.0"", ""generator"": ""DXTnavis"" }},
  ""buffers"": [{{ ""byteLength"": {0} }}],
  ""bufferViews"": [
    {{ ""buffer"": 0, ""byteOffset"": 0, ""byteLength"": {1}, ""target"": 34962 }},
    {{ ""buffer"": 0, ""byteOffset"": {1}, ""byteLength"": {2}, ""target"": 34962 }},
    {{ ""buffer"": 0, ""byteOffset"": {3}, ""byteLength"": {4}, ""target"": 34963 }}
  ],
  ""accessors"": [
    {{ ""bufferView"": 0, ""componentType"": 5126, ""count"": {5}, ""type"": ""VEC3"", ""min"": [{6}, {7}, {8}], ""max"": [{9}, {10}, {11}] }},
    {{ ""bufferView"": 1, ""componentType"": 5126, ""count"": {5}, ""type"": ""VEC3"" }},
    {{ ""bufferView"": 2, ""componentType"": 5125, ""count"": {12}, ""type"": ""SCALAR"" }}
  ]{13},
  ""meshes"": [{{ ""primitives"": [{{ ""attributes"": {{ ""POSITION"": 0, ""NORMAL"": 1 }}, ""indices"": 2{14} }}] }}],
  ""nodes"": [{{ ""mesh"": 0 }}],
  ""scenes"": [{{ ""nodes"": [0] }}],
  ""scene"": 0
}}",
                    totalBufferLength,      // {0}
                    vertexByteLength,       // {1}
                    normalByteLength,       // {2}
                    indexBufferOffset,       // {3}
                    indexByteLength,         // {4}
                    meshData.VertexCount,    // {5}
                    sMinX, sMinY, sMinZ,     // {6}{7}{8}
                    sMaxX, sMaxY, sMaxZ,     // {9}{10}{11}
                    meshData.Indices.Count,  // {12}
                    materialSection,         // {13}
                    materialRef);            // {14}
            }
            else
            {
                // Without normals: 2 bufferViews, 2 accessors (fallback)
                return string.Format(inv,
@"{{
  ""asset"": {{ ""version"": ""2.0"", ""generator"": ""DXTnavis"" }},
  ""buffers"": [{{ ""byteLength"": {0} }}],
  ""bufferViews"": [
    {{ ""buffer"": 0, ""byteOffset"": 0, ""byteLength"": {1}, ""target"": 34962 }},
    {{ ""buffer"": 0, ""byteOffset"": {1}, ""byteLength"": {2}, ""target"": 34963 }}
  ],
  ""accessors"": [
    {{ ""bufferView"": 0, ""componentType"": 5126, ""count"": {3}, ""type"": ""VEC3"", ""min"": [{4}, {5}, {6}], ""max"": [{7}, {8}, {9}] }},
    {{ ""bufferView"": 1, ""componentType"": 5125, ""count"": {10}, ""type"": ""SCALAR"" }}
  ]{11},
  ""meshes"": [{{ ""primitives"": [{{ ""attributes"": {{ ""POSITION"": 0 }}, ""indices"": 1{12} }}] }}],
  ""nodes"": [{{ ""mesh"": 0 }}],
  ""scenes"": [{{ ""nodes"": [0] }}],
  ""scene"": 0
}}",
                    totalBufferLength,
                    vertexByteLength,
                    indexByteLength,
                    meshData.VertexCount,
                    sMinX, sMinY, sMinZ,
                    sMaxX, sMaxY, sMaxZ,
                    meshData.Indices.Count,
                    materialSection,         // {11}
                    materialRef);            // {12}
            }
        }

        /// <summary>
        /// 바이너리 버퍼 생성 (positions + [normals] + indices)
        /// Z-up (Navisworks) → Y-up (glTF): gX = nX, gY = nZ, gZ = -nY
        /// </summary>
        private byte[] CreateBinaryBuffer(MeshData meshData)
        {
            bool hasNormals = meshData.Normals.Count > 0
                && meshData.Normals.Count == meshData.Vertices.Count;

            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                // Positions (Z-up → Y-up)
                for (int i = 0; i < meshData.Vertices.Count; i += 3)
                {
                    bw.Write(meshData.Vertices[i]);       // X → X
                    bw.Write(meshData.Vertices[i + 2]);   // Z → Y
                    bw.Write(-meshData.Vertices[i + 1]);  // -Y → Z
                }

                // Normals (same axis swap)
                if (hasNormals)
                {
                    for (int i = 0; i < meshData.Normals.Count; i += 3)
                    {
                        bw.Write(meshData.Normals[i]);       // X → X
                        bw.Write(meshData.Normals[i + 2]);   // Z → Y
                        bw.Write(-meshData.Normals[i + 1]);  // -Y → Z
                    }
                }

                // Indices
                foreach (var i in meshData.Indices)
                    bw.Write(i);

                return ms.ToArray();
            }
        }

        #endregion

        #region Event Helpers

        private void OnProgressChanged(int percentage)
        {
            ProgressChanged?.Invoke(this, percentage);
        }

        private void OnStatusChanged(string message)
        {
            StatusChanged?.Invoke(this, message);
            Debug.WriteLine($"[MeshExtractor] {message}");
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }

        #endregion
    }

    /// <summary>
    /// Mesh 데이터 컨테이너
    /// </summary>
    public class MeshData
    {
        public Guid ObjectId { get; set; }
        public List<float> Vertices { get; set; }
        public List<float> Normals { get; set; }
        public List<int> Indices { get; set; }
        public int VertexCount { get; set; }
        public int TriangleCount { get; set; }

        /// <summary>
        /// Phase 21: Mesh 품질 상태
        /// "full_mesh", "box_placeholder", "gap_supplemented", "partial_retry_success", "skipped_container"
        /// </summary>
        public string Quality { get; set; }

        /// <summary>
        /// Material diffuse color (R, G, B, A) in 0.0–1.0 range. Null if no color available.
        /// </summary>
        public float[] DiffuseColor { get; set; }
    }

    /// <summary>
    /// Phase 25: Tessellation 실패 이유 코드
    /// </summary>
    public enum TessFailureReason
    {
        None,               // 성공 (실패 아님)
        NoGeometry,         // HasGeometry=false (geometry 데이터 없음)
        Hidden,             // IsHidden=true (Navisworks에서 숨김 상태)
        ComStateNull,       // COM State null
        ComPathFailed,      // COM Path 변환 실패
        NoFragments,        // Fragment 0개 (tessellation 대상 없음)
        AllStrategiesFail,  // 5개 전략 모두 Triangle/Line 0 (EMPTY_TESS)
        Exception           // 예외 발생
    }

    /// <summary>
    /// Phase 19: Mesh 추출 진단 정보
    /// </summary>
    public class MeshDiagnosticInfo
    {
        public string DisplayName { get; set; }
        public string ClassDisplayName { get; set; }
        public bool IsLeaf { get; set; }
        public int ChildCount { get; set; }
        public bool HasGeometry { get; set; }
        public bool HasBBox { get; set; }
        public double BBoxVolume { get; set; }
        public bool ComPathFailed { get; set; }
        public int FragmentCount { get; set; }
        public int EmptyFragmentCount { get; set; }
        public int FragmentErrors { get; set; }
        public int VertexCount { get; set; }
        public int TriangleCount { get; set; }
        public bool HasNonIdentityTransform { get; set; }
        public string LastError { get; set; }

        // ──── Phase 22: Deep Diagnostic 확장 필드 ────
        /// <summary>Line 프리미티브 콜백 횟수 (파이프/센터라인)</summary>
        public int LineCount { get; set; }
        /// <summary>Point 프리미티브 콜백 횟수</summary>
        public int PointCount { get; set; }
        /// <summary>SnapPoint 프리미티브 콜백 횟수</summary>
        public int SnapPointCount { get; set; }
        /// <summary>객체 숨김 여부</summary>
        public bool IsHidden { get; set; }
        /// <summary>소스 모델 파일명</summary>
        public string SourceFile { get; set; }
        /// <summary>속성 카테고리 목록 (쉼표 구분)</summary>
        public string PropertyCategories { get; set; }
        /// <summary>5개 전략별 삼각형 수 [S1,S2,S3,S4,S5]</summary>
        public string StrategyResults { get; set; }
        /// <summary>5개 전략별 라인 수 [S1,S2,S3,S4,S5]</summary>
        public string StrategyLineResults { get; set; }
        /// <summary>Fragment COM 타입명 (첫 fragment)</summary>
        public string FragmentTypeName { get; set; }

        /// <summary>mesh 추출 가능 여부 판정</summary>
        public string Status
        {
            get
            {
                if (ComPathFailed) return "COM_FAIL";
                if (IsHidden) return "HIDDEN";
                if (!IsLeaf)
                {
                    // Parent 노드: tessellation 안 하므로 fragment 존재 여부만 판정
                    if (FragmentCount == 0) return "PARENT_NO_FRAG";
                    return "PARENT_HAS_FRAG";
                }
                if (FragmentCount == 0) return "NO_FRAGMENTS";
                if (TriangleCount == 0 && LineCount > 0) return "LINE_ONLY";
                if (TriangleCount == 0 && EmptyFragmentCount == FragmentCount) return "EMPTY_TESS";
                if (TriangleCount == 0) return "TESS_FAIL";
                if (TriangleCount > 0) return "OK";
                return "UNKNOWN";
            }
        }

        public string ToCsvLine(int depth, string hierarchyPath)
        {
            return string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20},{21},{22}",
                depth,
                EscapeCsv(hierarchyPath),
                EscapeCsv(DisplayName),
                EscapeCsv(ClassDisplayName ?? ""),
                IsLeaf ? "LEAF" : "PARENT",
                ChildCount,
                HasGeometry,
                HasBBox,
                BBoxVolume.ToString("F4", System.Globalization.CultureInfo.InvariantCulture),
                FragmentCount,
                EmptyFragmentCount,
                FragmentErrors,
                VertexCount,
                TriangleCount,
                LineCount,
                PointCount,
                SnapPointCount,
                HasNonIdentityTransform,
                IsHidden,
                EscapeCsv(SourceFile ?? ""),
                EscapeCsv(StrategyResults ?? ""),
                EscapeCsv(StrategyLineResults ?? ""),
                Status);
        }

        public static string CsvHeader =>
            "Depth,HierarchyPath,DisplayName,ClassDisplayName,NodeType,ChildCount,HasGeometry,HasBBox,BBoxVolume," +
            "FragmentCount,EmptyFragments,FragmentErrors,VertexCount,TriangleCount," +
            "LineCount,PointCount,SnapPointCount,HasTransform,IsHidden,SourceFile," +
            "StrategyTriangles,StrategyLines,Status";

        private static string EscapeCsv(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.Contains(",") || s.Contains("\"") || s.Contains("\n"))
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        /// <summary>Public accessor for CSV escaping (used by ViewModel diagnostic export)</summary>
        public static string EscapeCsvPublic(string s) => EscapeCsv(s);
    }

    /// <summary>
    /// Phase 22: 진단용 콜백 — Triangle/Line/Point/SnapPoint 모두 카운트
    /// </summary>
    internal class DiagnosticCallbackSink : InwSimplePrimitivesCB
    {
        public int TriangleCount { get; private set; }
        public int LineCount { get; private set; }
        public int PointCount { get; private set; }
        public int SnapPointCount { get; private set; }

        public void Triangle(InwSimpleVertex v1, InwSimpleVertex v2, InwSimpleVertex v3)
        {
            TriangleCount++;
        }

        public void Line(InwSimpleVertex v1, InwSimpleVertex v2)
        {
            LineCount++;
        }

        public void Point(InwSimpleVertex v1)
        {
            PointCount++;
        }

        public void SnapPoint(InwSimpleVertex v1)
        {
            SnapPointCount++;
        }

        public void Reset()
        {
            TriangleCount = 0;
            LineCount = 0;
            PointCount = 0;
            SnapPointCount = 0;
        }
    }

    /// <summary>
    /// COM API Mesh Callback Sink
    /// GenerateSimplePrimitives() 결과를 수집
    /// </summary>
    internal class MeshCallbackSink : InwSimplePrimitivesCB
    {
        private readonly MeshData _meshData;
        private int _currentVertexIndex = 0;

        // Phase 25: Line 프리미티브 수집 (추후 tube mesh로 변환)
        private readonly List<float[]> _lineSegments = new List<float[]>();
        public int LineCount { get; private set; }

        public MeshCallbackSink(MeshData meshData)
        {
            _meshData = meshData;
        }

        public void Line(InwSimpleVertex v1, InwSimpleVertex v2)
        {
            // Phase 25: Line vertex 쌍 수집 (LCS 좌표)
            var c1 = GetComArray(v1, "coord");
            var c2 = GetComArray(v2, "coord");
            if (c1 != null && c1.Length >= 3 && c2 != null && c2.Length >= 3)
            {
                _lineSegments.Add(new float[] { c1[0], c1[1], c1[2], c2[0], c2[1], c2[2] });
                LineCount++;
            }
        }

        public void Point(InwSimpleVertex v1)
        {
            // 포인트는 무시 (mesh 변환 불가)
        }

        public void SnapPoint(InwSimpleVertex v1)
        {
            // 스냅 포인트는 무시
        }

        /// <summary>
        /// Phase 25: 수집된 Line segment를 thin tube mesh로 변환하여 MeshData에 추가
        /// Triangle이 전혀 없는 fragment(Line-only)에서만 호출
        /// </summary>
        /// <param name="tubeRadius">Tube 반지름 (기본 5mm)</param>
        public void ConvertLinesToMesh(float tubeRadius = 0.005f)
        {
            if (_lineSegments.Count == 0) return;

            foreach (var seg in _lineSegments)
            {
                AppendTubeMesh(_meshData, seg[0], seg[1], seg[2], seg[3], seg[4], seg[5],
                    tubeRadius, 6, ref _currentVertexIndex);
            }
        }

        /// <summary>
        /// Phase 25: 두 끝점 사이에 thin tube mesh 추가 (6-sided, 12 triangles per segment)
        /// </summary>
        private static void AppendTubeMesh(MeshData mesh,
            float x1, float y1, float z1,
            float x2, float y2, float z2,
            float radius, int segments, ref int vertexIndex)
        {
            // Direction vector
            float ddx = x2 - x1, ddy = y2 - y1, ddz = z2 - z1;
            float len = (float)Math.Sqrt(ddx * ddx + ddy * ddy + ddz * ddz);
            if (len < 1e-9f) return; // degenerate line

            // Normalize direction
            float dx = ddx / len, dy = ddy / len, dz = ddz / len;

            // Find perpendicular vector U via cross with least-aligned axis
            float ux, uy, uz;
            if (Math.Abs(dx) <= Math.Abs(dy) && Math.Abs(dx) <= Math.Abs(dz))
            {
                // X-axis least aligned → cross with X
                ux = 0; uy = -dz; uz = dy;
            }
            else if (Math.Abs(dy) <= Math.Abs(dz))
            {
                // Y-axis least aligned → cross with Y
                ux = dz; uy = 0; uz = -dx;
            }
            else
            {
                // Z-axis least aligned → cross with Z
                ux = -dy; uy = dx; uz = 0;
            }
            float uLen = (float)Math.Sqrt(ux * ux + uy * uy + uz * uz);
            if (uLen < 1e-9f) return;
            ux /= uLen; uy /= uLen; uz /= uLen;

            // V = D × U (cross product)
            float vx = dy * uz - dz * uy;
            float vy = dz * ux - dx * uz;
            float vz = dx * uy - dy * ux;

            // Generate ring vertices at P1 (end=0) and P2 (end=1)
            int baseIdx = vertexIndex;
            for (int end = 0; end < 2; end++)
            {
                float px = (end == 0) ? x1 : x2;
                float py = (end == 0) ? y1 : y2;
                float pz = (end == 0) ? z1 : z2;

                for (int i = 0; i < segments; i++)
                {
                    float angle = 2f * (float)Math.PI * i / segments;
                    float cosA = (float)Math.Cos(angle);
                    float sinA = (float)Math.Sin(angle);

                    float nx = cosA * ux + sinA * vx;
                    float ny = cosA * uy + sinA * vy;
                    float nz = cosA * uz + sinA * vz;

                    mesh.Vertices.Add(px + radius * nx);
                    mesh.Vertices.Add(py + radius * ny);
                    mesh.Vertices.Add(pz + radius * nz);
                    mesh.Normals.Add(nx);
                    mesh.Normals.Add(ny);
                    mesh.Normals.Add(nz);
                    vertexIndex++;
                }
            }

            // Side wall triangles (2 per quad segment)
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                int topA = baseIdx + i;
                int topB = baseIdx + next;
                int botA = baseIdx + segments + i;
                int botB = baseIdx + segments + next;

                mesh.Indices.Add(topA);
                mesh.Indices.Add(botA);
                mesh.Indices.Add(topB);

                mesh.Indices.Add(topB);
                mesh.Indices.Add(botA);
                mesh.Indices.Add(botB);
            }
        }

        public void Triangle(InwSimpleVertex v1, InwSimpleVertex v2, InwSimpleVertex v3)
        {
            // Phase 20: Transaction-safe triangle — 3 vertex 모두 성공해야 commit
            int vBefore = _meshData.Vertices.Count;
            int nBefore = _meshData.Normals.Count;

            AddVertex(v1);
            AddVertex(v2);
            AddVertex(v3);

            int verticesAdded = _meshData.Vertices.Count - vBefore;

            // 3 vertices × 3 floats = 9 floats가 추가되어야 정상
            if (verticesAdded != 9)
            {
                // Rollback: 부분적으로 추가된 vertex/normal 제거
                if (_meshData.Vertices.Count > vBefore)
                    _meshData.Vertices.RemoveRange(vBefore, _meshData.Vertices.Count - vBefore);
                if (_meshData.Normals.Count > nBefore)
                    _meshData.Normals.RemoveRange(nBefore, _meshData.Normals.Count - nBefore);
                return;
            }

            int idx1 = _currentVertexIndex++;
            int idx2 = _currentVertexIndex++;
            int idx3 = _currentVertexIndex++;

            _meshData.Indices.Add(idx1);
            _meshData.Indices.Add(idx2);
            _meshData.Indices.Add(idx3);
        }

        private void AddVertex(InwSimpleVertex v)
        {
            // Navisworks COM API: coord/normal return 1-based SAFEARRAY (Single[*])
            // Direct cast to float[] fails with InvalidCastException
            // Fix: use reflection late-binding to bypass PIA typed marshaling
            var coord = GetComArray(v, "coord");
            if (coord == null || coord.Length < 3) return;

            _meshData.Vertices.Add(coord[0]);
            _meshData.Vertices.Add(coord[1]);
            _meshData.Vertices.Add(coord[2]);

            // Normal (if available)
            var normal = GetComArray(v, "normal");
            if (normal != null && normal.Length >= 3)
            {
                _meshData.Normals.Add(normal[0]);
                _meshData.Normals.Add(normal[1]);
                _meshData.Normals.Add(normal[2]);
            }
        }

        /// <summary>
        /// COM SAFEARRAY (Single[*]) → float[] 변환
        /// PIA typed property가 Single[*]→Single[] 캐스트 실패하므로
        /// InvokeMember로 late-bound 접근하여 Array 기반으로 읽음
        /// </summary>
        private static float[] GetComArray(object comObject, string propertyName)
        {
            try
            {
                var raw = comObject.GetType().InvokeMember(
                    propertyName,
                    System.Reflection.BindingFlags.GetProperty,
                    null, comObject, null);

                if (raw == null) return null;

                var arr = raw as Array;
                if (arr == null) return null;
                int len = arr.Length;
                var result = new float[len];
                int lb = arr.GetLowerBound(0);
                for (int i = 0; i < len; i++)
                    result[i] = Convert.ToSingle(arr.GetValue(lb + i));
                return result;
            }
            catch
            {
                return null;
            }
        }
    }
}