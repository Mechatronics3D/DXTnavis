using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.Navisworks.Api;
using DXTnavis.Models.Geometry;
using DXTnavis.Models.Spatial;
using DXTnavis.Models.Validation;
using DXTnavis.Services.Geometry;

namespace DXTnavis.Services.Validation
{
    /// <summary>
    /// Phase 29: 객체별 검증 서비스
    /// 모든 geometry 객체에 대해 LcOpGeometryProperty, tessellation, adjacency 결과를 검증하고 CSV 출력
    /// </summary>
    public class ObjectValidationService
    {
        #region Events

        public event EventHandler<int> ProgressChanged;
        public event EventHandler<string> StatusChanged;

        #endregion

        #region Public Methods

        /// <summary>
        /// 전체 검증 실행: geometry 객체 목록 + ModelItem 맵 → validation records 생성
        /// </summary>
        /// <param name="geometries">geometry 레코드 딕셔너리</param>
        /// <param name="modelItemMap">ObjectId → ModelItem 매핑</param>
        /// <param name="containerIds">container로 판정된 ObjectId 집합</param>
        /// <param name="partialContainerIds">partial container로 판정된 ObjectId 집합</param>
        /// <param name="adjacencies">인접 관계 목록</param>
        /// <param name="groups">연결 그룹 목록</param>
        /// <param name="parentChildMap">부모-자식 관계 맵</param>
        /// <returns>검증 레코드 목록</returns>
        public List<ObjectValidationRecord> Validate(
            Dictionary<Guid, GeometryRecord> geometries,
            Dictionary<Guid, ModelItem> modelItemMap,
            HashSet<Guid> containerIds,
            HashSet<Guid> partialContainerIds,
            List<AdjacencyRecord> adjacencies,
            List<ConnectedGroup> groups,
            Dictionary<Guid, List<Guid>> parentChildMap,
            string meshDir = null)
        {
            var records = new List<ObjectValidationRecord>();
            if (geometries == null || modelItemMap == null) return records;

            OnStatusChanged(string.Format("[Validation] 검증 시작: {0:N0}개 객체", geometries.Count));

            // adjacency count 사전 집계
            var adjacencyCountMap = BuildAdjacencyCountMap(adjacencies);

            // group membership 사전 집계
            var objectGroupMap = BuildObjectGroupMap(groups);

            int processed = 0;
            int total = geometries.Count;

            foreach (var kvp in geometries)
            {
                var objectId = kvp.Key;
                var geoRecord = kvp.Value;

                var record = new ObjectValidationRecord
                {
                    ObjectId = objectId,
                    DisplayName = geoRecord.DisplayName ?? "",
                    MeshQuality = geoRecord.MeshQuality ?? "",
                    VertexCount = geoRecord.VertexCount,
                    TriangleCount = geoRecord.TriangleCount,
                    HasBBox = geoRecord.BBox != null && geoRecord.BBox.IsValid,
                    BBoxVolume = geoRecord.GetVolume()
                };

                // ModelItem에서 Navisworks API 정보 추출
                ModelItem item;
                if (modelItemMap.TryGetValue(objectId, out item) && item != null)
                {
                    FillFromModelItem(record, item);
                    ReadGeometryProperty(record, item);
                }

                // Container 판정
                if (containerIds != null && containerIds.Contains(objectId))
                    record.ContainerStatus = "skipped_container";
                else if (partialContainerIds != null && partialContainerIds.Contains(objectId))
                    record.ContainerStatus = "partial_container";
                else if (!record.HasGeometry && record.ChildCount > 0)
                    record.ContainerStatus = "no_geometry_group";
                else
                    record.ContainerStatus = "none";

                // Tessellation 결과 판정
                ClassifyTessResult(record, geoRecord);

                // Adjacency 정보
                int adjCount;
                record.AdjacencyCount = adjacencyCountMap.TryGetValue(objectId, out adjCount) ? adjCount : 0;

                string groupId;
                record.GroupId = objectGroupMap.TryGetValue(objectId, out groupId) ? groupId : "";

                // Phase 30: GLB 파일 존재/크기 검증
                if (!string.IsNullOrEmpty(meshDir) && !string.IsNullOrEmpty(geoRecord.MeshUri))
                {
                    var glbPath = Path.Combine(meshDir, Path.GetFileName(geoRecord.MeshUri));
                    var fi = new FileInfo(glbPath);
                    record.GlbExists = fi.Exists;
                    record.GlbSizeBytes = fi.Exists ? fi.Length : 0L;
                }

                // HasMesh: GLB 파일 존재 여부 (= GlbExists, box_placeholder 포함)
                record.HasMesh = record.GlbExists;

                // HasRealMesh: 실제 geometry 기반 메시만 true (full_mesh, fbx_supplemented, line_mesh)
                string mq = record.MeshQuality ?? "";
                record.HasRealMesh = record.GlbExists &&
                    (mq == "full_mesh" || mq == "gap_supplemented" || mq == "partial_retry_success" ||
                     mq == "fbx_supplemented" || mq == "line_mesh");

                // 최종 판정
                record.ComputeVerdict();

                records.Add(record);

                processed++;
                if (processed % 100 == 0)
                {
                    int pct = (int)(100.0 * processed / total);
                    OnProgressChanged(pct);
                    System.Windows.Forms.Application.DoEvents();
                }
            }

            OnProgressChanged(100);
            OnStatusChanged(string.Format("[Validation] 검증 완료: {0:N0}개 레코드", records.Count));

            // 요약 로그
            LogSummary(records);

            return records;
        }

        /// <summary>
        /// validation.csv 파일 출력
        /// </summary>
        public string WriteCsv(List<ObjectValidationRecord> records, string outputDir)
        {
            if (records == null || records.Count == 0) return null;

            var filePath = Path.Combine(outputDir, "validation.csv");
            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            using (var sw = new StreamWriter(filePath, false, new UTF8Encoding(true)))
            {
                sw.WriteLine(ObjectValidationRecord.CsvHeader);
                foreach (var record in records)
                {
                    sw.WriteLine(record.ToCsvRow());
                }
            }

            OnStatusChanged(string.Format("validation.csv 저장: {0:N0}개 레코드 → {1}", records.Count, filePath));
            return filePath;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// ModelItem에서 기본 정보 추출
        /// </summary>
        private void FillFromModelItem(ObjectValidationRecord record, ModelItem item)
        {
            try { record.ClassDisplayName = item.ClassDisplayName ?? ""; } catch { }
            try { record.HasGeometry = item.HasGeometry; } catch { }
            try { record.IsHidden = item.IsHidden; } catch { }

            try
            {
                bool hasChildren = item.Children != null && item.Children.Any();
                record.IsLeaf = !hasChildren;
                record.ChildCount = hasChildren ? item.Children.Count() : 0;
            }
            catch { }

            // Level과 ParentId — AncestorsAndSelf에서 추출
            try
            {
                var ancestors = item.AncestorsAndSelf;
                if (ancestors != null)
                    record.Level = ancestors.Count() - 1;
            }
            catch { }

            try
            {
                if (item.Parent != null)
                {
                    // Parent의 InstanceGuid 추출 시도
                    var parentGuid = item.Parent.InstanceGuid;
                    record.ParentId = parentGuid;
                }
            }
            catch { }
        }

        /// <summary>
        /// LcOpGeometryProperty 카테고리에서 형상 속성 읽기
        /// Navisworks 특성 탭 → "형상 (LcOpGeometryProperty)"
        /// </summary>
        private void ReadGeometryProperty(ObjectValidationRecord record, ModelItem item)
        {
            try
            {
                foreach (var category in item.PropertyCategories)
                {
                    // 내부명: "LcOpGeometryProperty" / 표시명: "형상"
                    if (category.Name != "LcOpGeometryProperty")
                        continue;

                    record.HasGeometryProperty = true;

                    foreach (var prop in category.Properties)
                    {
                        try
                        {
                            switch (prop.Name)
                            {
                                case "LcOpGeometryPropertyPrimitives":
                                    record.GeoP_Primitives = SafeToInt(prop.Value);
                                    break;
                                case "LcOpGeometryPropertyHasTriangles":
                                    record.GeoP_HasTriangles = SafeToBool(prop.Value);
                                    break;
                                case "LcOpGeometryPropertyHasLines":
                                    record.GeoP_HasLines = SafeToBool(prop.Value);
                                    break;
                                case "LcOpGeometryPropertySolid":
                                    record.GeoP_Solid = SafeToBool(prop.Value);
                                    break;
                                case "LcOpGeometryPropertyHasText":
                                    record.GeoP_HasText = SafeToBool(prop.Value);
                                    break;
                                case "LcOpGeometryPropertyHasSnapPoints":
                                    record.GeoP_HasSnapPoints = SafeToBool(prop.Value);
                                    break;
                                case "LcOpGeometryPropertyHasPoints":
                                    record.GeoP_HasPoints = SafeToBool(prop.Value);
                                    break;
                                case "LcOpGeometryPropertyFragments":
                                    record.GeoP_Fragments = SafeToInt(prop.Value);
                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(string.Format("[Validation] Property read error: {0}.{1} → {2}",
                                category.Name, prop.Name, ex.Message));
                        }
                    }

                    break; // LcOpGeometryProperty 카테고리 찾으면 종료
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("[Validation] ReadGeometryProperty error for {0}: {1}",
                    record.DisplayName, ex.Message));
            }
        }

        /// <summary>
        /// VariantData를 안전하게 int로 변환
        /// </summary>
        private static int SafeToInt(VariantData data)
        {
            if (data == null || data.IsNone) return 0;

            try
            {
                if (data.DataType == VariantDataType.Int32) return data.ToInt32();
                if (data.DataType == VariantDataType.Double) return (int)data.ToDouble();
                if (data.DataType == VariantDataType.DisplayString)
                {
                    int result;
                    if (int.TryParse(data.ToDisplayString(), out result)) return result;
                }
                return data.ToInt32();
            }
            catch { return 0; }
        }

        /// <summary>
        /// VariantData를 안전하게 bool로 변환
        /// Navisworks에서 "예"/"아니오" 또는 true/false
        /// </summary>
        private static bool SafeToBool(VariantData data)
        {
            if (data == null || data.IsNone) return false;

            try
            {
                if (data.DataType == VariantDataType.Boolean) return data.ToBoolean();
                if (data.DataType == VariantDataType.DisplayString)
                {
                    string str = data.ToDisplayString();
                    return str == "예" || str == "Yes" || str == "True" || str == "true" || str == "1";
                }
                if (data.DataType == VariantDataType.Int32) return data.ToInt32() != 0;
                return data.ToBoolean();
            }
            catch { return false; }
        }

        /// <summary>
        /// GeometryRecord의 MeshQuality로부터 tessellation 결과 분류
        /// </summary>
        private void ClassifyTessResult(ObjectValidationRecord record, GeometryRecord geoRecord)
        {
            string quality = geoRecord.MeshQuality ?? "";

            switch (quality)
            {
                case "full_mesh":
                case "partial_retry_success":
                case "gap_supplemented":
                    record.TessResult = "success";
                    record.TessFailureReason = "None";
                    break;

                case "line_mesh":
                    record.TessResult = "success";
                    record.TessFailureReason = "None";
                    break;

                case "fbx_supplemented":
                    record.TessResult = "success";
                    record.TessFailureReason = "None";
                    break;

                case "box_placeholder":
                    record.TessResult = "failure";
                    // 세부 이유: HasGeometry 기반 추정
                    if (!record.HasGeometry)
                        record.TessFailureReason = "NoGeometry";
                    else if (record.IsHidden)
                        record.TessFailureReason = "Hidden";
                    else if (record.GeoP_Fragments == 0 && record.HasGeometryProperty)
                        record.TessFailureReason = "NoFragments";
                    else
                        record.TessFailureReason = "AllStrategiesFail";
                    break;

                case "skipped_container":
                    record.TessResult = "skipped";
                    record.TessFailureReason = "Container";
                    break;

                case "hidden":
                    record.TessResult = "skipped";
                    record.TessFailureReason = "Hidden";
                    break;

                case "no_geometry":
                    record.TessResult = "skipped";
                    record.TessFailureReason = "NoGeometry";
                    break;

                default:
                    if (geoRecord.HasMesh && geoRecord.VertexCount > 0)
                    {
                        record.TessResult = "success";
                        record.TessFailureReason = "None";
                    }
                    else
                    {
                        record.TessResult = "failure";
                        record.TessFailureReason = "Unknown";
                    }
                    break;
            }
        }

        /// <summary>
        /// adjacency 목록에서 ObjectId별 인접 관계 수 집계
        /// </summary>
        private Dictionary<Guid, int> BuildAdjacencyCountMap(List<AdjacencyRecord> adjacencies)
        {
            var map = new Dictionary<Guid, int>();
            if (adjacencies == null) return map;

            foreach (var adj in adjacencies)
            {
                if (!map.ContainsKey(adj.SourceObjectId))
                    map[adj.SourceObjectId] = 0;
                map[adj.SourceObjectId]++;

                if (!map.ContainsKey(adj.TargetObjectId))
                    map[adj.TargetObjectId] = 0;
                map[adj.TargetObjectId]++;
            }

            return map;
        }

        /// <summary>
        /// 연결 그룹에서 ObjectId → GroupId 매핑 구축
        /// </summary>
        private Dictionary<Guid, string> BuildObjectGroupMap(List<ConnectedGroup> groups)
        {
            var map = new Dictionary<Guid, string>();
            if (groups == null) return map;

            foreach (var group in groups)
            {
                string groupId = string.Format("G{0:D3}", group.GroupId);
                foreach (var elementId in group.ElementIds)
                {
                    map[elementId] = groupId;
                }
            }

            return map;
        }

        /// <summary>
        /// 검증 요약 로그 출력
        /// </summary>
        private void LogSummary(List<ObjectValidationRecord> records)
        {
            var verdictCounts = new Dictionary<string, int>();
            int hasTrianglesNoMesh = 0;

            foreach (var r in records)
            {
                string v = r.Verdict ?? "UNKNOWN";
                if (!verdictCounts.ContainsKey(v))
                    verdictCounts[v] = 0;
                verdictCounts[v]++;

                if (r.GeoP_HasTriangles && r.TessResult == "failure")
                    hasTrianglesNoMesh++;
            }

            var sb = new StringBuilder();
            sb.AppendLine("[Validation] === Phase 29 검증 요약 ===");
            foreach (var kv in verdictCounts)
            {
                sb.AppendFormat("  {0}: {1:N0}", kv.Key, kv.Value);
                sb.AppendLine();
            }
            sb.AppendFormat("  HasTriangles=true & TessResult=failure: {0:N0} ← 조사 대상", hasTrianglesNoMesh);
            Debug.WriteLine(sb.ToString());

            // Phase 31: GLB 무결성 verdict도 로그에 포함
            int glbMissing = verdictCounts.ContainsKey("FAIL_GLB_MISSING") ? verdictCounts["FAIL_GLB_MISSING"] : 0;
            int glbEmpty = verdictCounts.ContainsKey("FAIL_GLB_EMPTY") ? verdictCounts["FAIL_GLB_EMPTY"] : 0;
            int fbxBatchOnly = verdictCounts.ContainsKey("WARN_FBX_BATCH_ONLY") ? verdictCounts["WARN_FBX_BATCH_ONLY"] : 0;

            OnStatusChanged(string.Format(
                "[Validation] 판정 완료 — FAIL_NO_EXTRACT: {0}개, WARN_BOX: {1}개, FAIL_GLB_MISSING: {2}개, FAIL_GLB_EMPTY: {3}개, WARN_FBX_BATCH_ONLY: {4}개",
                verdictCounts.ContainsKey("FAIL_NO_EXTRACT") ? verdictCounts["FAIL_NO_EXTRACT"] : 0,
                verdictCounts.ContainsKey("WARN_BOX") ? verdictCounts["WARN_BOX"] : 0,
                glbMissing, glbEmpty, fbxBatchOnly));
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
            Debug.WriteLine(message);
        }

        #endregion
    }
}
