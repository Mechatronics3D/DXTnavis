using System;
using System.Globalization;
using System.Text;

namespace DXTnavis.Models.Validation
{
    /// <summary>
    /// Phase 29: 객체별 검증 결과 레코드
    /// Full Pipeline Export 시 모든 객체에 대해 geometry property, tessellation, adjacency 검증
    /// </summary>
    public class ObjectValidationRecord
    {
        #region A. 기본 정보

        public Guid ObjectId { get; set; }
        public string DisplayName { get; set; }
        public string ClassDisplayName { get; set; }
        public int Level { get; set; }
        public Guid ParentId { get; set; }
        public bool IsLeaf { get; set; }
        public int ChildCount { get; set; }

        #endregion

        #region B. Navisworks API 상태

        public bool HasGeometry { get; set; }
        public bool IsHidden { get; set; }

        #endregion

        #region C. LcOpGeometryProperty (형상 탭)

        /// <summary>기본체 수 (LcOpGeometryPropertyPrimitives)</summary>
        public int GeoP_Primitives { get; set; }

        /// <summary>삼각형 존재 (LcOpGeometryPropertyHasTriangles) — mesh 추출 가능 핵심 지표</summary>
        public bool GeoP_HasTriangles { get; set; }

        /// <summary>선분 존재 (LcOpGeometryPropertyHasLines)</summary>
        public bool GeoP_HasLines { get; set; }

        /// <summary>솔리드 여부 (LcOpGeometryPropertySolid)</summary>
        public bool GeoP_Solid { get; set; }

        /// <summary>문자 존재 (LcOpGeometryPropertyHasText)</summary>
        public bool GeoP_HasText { get; set; }

        /// <summary>스냅점 존재 (LcOpGeometryPropertyHasSnapPoints)</summary>
        public bool GeoP_HasSnapPoints { get; set; }

        /// <summary>점 존재 (LcOpGeometryPropertyHasPoints)</summary>
        public bool GeoP_HasPoints { get; set; }

        /// <summary>조각 수 (LcOpGeometryPropertyFragments)</summary>
        public int GeoP_Fragments { get; set; }

        /// <summary>LcOpGeometryProperty 카테고리 존재 여부</summary>
        public bool HasGeometryProperty { get; set; }

        #endregion

        #region D. BBox

        public bool HasBBox { get; set; }
        public double BBoxVolume { get; set; }

        #endregion

        #region E. Container 판정

        /// <summary>none / skipped_container / partial_container / no_geometry_group</summary>
        public string ContainerStatus { get; set; }

        #endregion

        #region F. Tessellation 결과

        /// <summary>success / failure / skipped</summary>
        public string TessResult { get; set; }

        /// <summary>None / NoGeometry / Hidden / NoFragments / AllStrategiesFail / Exception</summary>
        public string TessFailureReason { get; set; }

        /// <summary>full_mesh / line_mesh / gap_supplemented / fbx_supplemented / box_placeholder / skipped_container</summary>
        public string MeshQuality { get; set; }

        public int VertexCount { get; set; }
        public int TriangleCount { get; set; }

        #endregion

        #region G. Spatial

        public int AdjacencyCount { get; set; }
        public string GroupId { get; set; }

        #endregion

        #region G2. Phase 30: GLB 파일 검증

        /// <summary>GLB 파일 존재 여부</summary>
        public bool GlbExists { get; set; }

        /// <summary>GLB 파일 크기 (bytes, 0 if not exists)</summary>
        public long GlbSizeBytes { get; set; }

        /// <summary>GLB 파일 존재 여부 (= GlbExists, box_placeholder 포함)</summary>
        public bool HasMesh { get; set; }

        /// <summary>실제 geometry 기반 메시 존재 여부 (full_mesh, fbx_supplemented, line_mesh만 true)</summary>
        public bool HasRealMesh { get; set; }

        #endregion

        #region H. 최종 판정

        /// <summary>
        /// OK_MESH / OK_LINE_MESH / OK_FBX / WARN_BOX /
        /// SKIP_CONTAINER / SKIP_NO_GEOMETRY / SKIP_HIDDEN /
        /// FAIL_NO_EXTRACT / FAIL_GLB_MISSING / FAIL_GLB_EMPTY /
        /// WARN_FBX_BATCH_ONLY
        /// </summary>
        public string Verdict { get; set; }

        #endregion

        #region CSV 출력

        public static string CsvHeader =>
            "ObjectId,DisplayName,ClassDisplayName,Level,ParentId,IsLeaf,ChildCount," +
            "HasGeometry,IsHidden," +
            "HasGeometryProperty,GeoP_Primitives,GeoP_HasTriangles,GeoP_HasLines,GeoP_Solid,GeoP_HasText,GeoP_HasSnapPoints,GeoP_HasPoints,GeoP_Fragments," +
            "HasBBox,BBoxVolume," +
            "ContainerStatus," +
            "TessResult,TessFailureReason,MeshQuality,VertexCount,TriangleCount," +
            "AdjacencyCount,GroupId," +
            "GlbExists,GlbSizeBytes,HasMesh,HasRealMesh," +
            "Verdict";

        public string ToCsvRow()
        {
            return string.Format(CultureInfo.InvariantCulture,
                "{0},{1},{2},{3},{4},{5},{6}," +
                "{7},{8}," +
                "{9},{10},{11},{12},{13},{14},{15},{16},{17}," +
                "{18},{19:F6}," +
                "{20}," +
                "{21},{22},{23},{24},{25}," +
                "{26},{27}," +
                "{28},{29},{30},{31}," +
                "{32}",
                ObjectId.ToString("D"),
                EscapeCsv(DisplayName),
                EscapeCsv(ClassDisplayName),
                Level,
                ParentId == Guid.Empty ? "" : ParentId.ToString("D"),
                IsLeaf,
                ChildCount,
                HasGeometry,
                IsHidden,
                HasGeometryProperty,
                GeoP_Primitives,
                GeoP_HasTriangles,
                GeoP_HasLines,
                GeoP_Solid,
                GeoP_HasText,
                GeoP_HasSnapPoints,
                GeoP_HasPoints,
                GeoP_Fragments,
                HasBBox,
                BBoxVolume,
                EscapeCsv(ContainerStatus ?? "none"),
                EscapeCsv(TessResult ?? ""),
                EscapeCsv(TessFailureReason ?? ""),
                EscapeCsv(MeshQuality ?? ""),
                VertexCount,
                TriangleCount,
                AdjacencyCount,
                EscapeCsv(GroupId ?? ""),
                GlbExists,
                GlbSizeBytes,
                HasMesh,
                HasRealMesh,
                EscapeCsv(Verdict ?? ""));
        }

        private static string EscapeCsv(string field)
        {
            if (string.IsNullOrEmpty(field)) return "";
            if (field.Contains(",") || field.Contains("\"") || field.Contains("\n"))
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            return field;
        }

        #endregion

        #region Verdict 자동 판정

        /// <summary>
        /// 모든 필드가 채워진 후 Verdict를 자동 계산
        /// </summary>
        public void ComputeVerdict()
        {
            // Container skip
            if (ContainerStatus == "skipped_container")
            {
                Verdict = "SKIP_CONTAINER";
                return;
            }

            // Hidden
            if (IsHidden)
            {
                Verdict = "SKIP_HIDDEN";
                return;
            }

            // NoGeometry (API에서 geometry 없는 논리 노드)
            if (!HasGeometry && !HasGeometryProperty)
            {
                Verdict = "SKIP_NO_GEOMETRY";
                return;
            }

            // ── Phase 31: MeshQuality 기반 1차 판정 ──
            string preliminaryVerdict = null;

            if (MeshQuality == "full_mesh" || MeshQuality == "gap_supplemented" || MeshQuality == "partial_retry_success")
                preliminaryVerdict = "OK_MESH";
            else if (MeshQuality == "line_mesh")
                preliminaryVerdict = "OK_LINE_MESH";
            else if (MeshQuality == "fbx_supplemented")
                preliminaryVerdict = "OK_FBX";

            // ── Phase 31: GLB 물리 파일 무결성 검증 (OK 판정에만 적용) ──
            if (preliminaryVerdict != null)
            {
                // GLB 검증이 수행된 경우에만 (meshDir가 전달된 경우)
                if (GlbSizeBytes >= 0 && !string.IsNullOrEmpty(MeshQuality))
                {
                    if (!GlbExists)
                    {
                        Verdict = "FAIL_GLB_MISSING";
                        return;
                    }
                    if (GlbSizeBytes == 0)
                    {
                        Verdict = "FAIL_GLB_EMPTY";
                        return;
                    }
                }

                // fbx_supplemented 추가 검증: 개별 GLB에 실제 mesh가 있는지
                if (preliminaryVerdict == "OK_FBX" && GlbExists && VertexCount == 0 && TriangleCount == 0)
                {
                    Verdict = "WARN_FBX_BATCH_ONLY";
                    return;
                }

                Verdict = preliminaryVerdict;
                return;
            }

            // Box placeholder — HasTriangles인데 실패했으면 FAIL
            if (MeshQuality == "box_placeholder")
            {
                if (GeoP_HasTriangles)
                    Verdict = "FAIL_NO_EXTRACT";
                else if (HasGeometry)
                    Verdict = "WARN_BOX";
                else
                    Verdict = "SKIP_NO_GEOMETRY";
                return;
            }

            // NoGeometry이지만 HasGeometryProperty는 있는 경우
            if (!HasGeometry && HasGeometryProperty)
            {
                Verdict = "WARN_BOX";
                return;
            }

            // 기타 실패
            if (TessResult == "failure")
            {
                Verdict = GeoP_HasTriangles ? "FAIL_NO_EXTRACT" : "WARN_BOX";
                return;
            }

            Verdict = "UNKNOWN";
        }

        #endregion
    }
}
