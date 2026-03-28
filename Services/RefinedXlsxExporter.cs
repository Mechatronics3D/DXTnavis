using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using Autodesk.Navisworks.Api;
using DXTnavis.Models;
using WinForms = System.Windows.Forms;

namespace DXTnavis.Services
{
    /// <summary>
    /// Refining_ObjectID_Latest.xlsx 스타일의 서식이 적용된 XLSX 워크북을 생성합니다.
    /// 5개 시트: Refining_ObjectID_Pivot, Pipeline_Summary, Class_Distribution, Equipment_Summary, PipeRun_Detail
    /// ClosedXML 기반 구현
    /// </summary>
    public class RefinedXlsxExporter
    {
        #region Constants

        // 헤더 스타일: Dark blue (#2F5496), white text
        private const string HeaderFillHex = "#2F5496";
        private const string HeaderFontColorHex = "#FFFFFF";

        // Class별 행 배경색
        private static readonly Dictionary<string, string> ClassColorMap =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Piping", "#E2EFDA" },
                { "Structure", "#FFF2CC" },
                { "Equipment", "#D6DCE4" }
            };

        /// <summary>
        /// Refining_ObjectID_Pivot 시트의 66개 컬럼 정의
        /// (Category|PropertyName) 매핑은 MatchPropertyKey로 수행
        /// </summary>
        private static readonly string[] PivotColumns = new string[]
        {
            "Class",
            "ObjectId(GUID)",
            "DisplayName",
            "System Path",
            "Level",
            "Status",
            "Permission Group ID",
            "Pipeline",
            "PipeRun",
            "RunName",
            "Spec Name",
            "Specification",
            "NPD",
            "Size",
            "Description",
            "Construction Type",
            "Location",
            "Material",
            "Material Grade",
            "Material Name",
            "Material Type",
            "Equipment Name",
            "BOM description",
            "Cardinal Point",
            "Commodity Code",
            "Cut Length",
            "Depth",
            "Height",
            "Length",
            "Width",
            "Design Max Pressure",
            "Design Max Temperature",
            "Dry Weight",
            "Wet Weight",
            "Weight",
            "End Prep",
            "End Standard",
            "Eqp Type 0",
            "Eqp Type 1",
            "Eqp Type 2",
            "Eqp Type 3",
            "Fire Rating",
            "Fireproofing Label",
            "Flow Direction",
            "Insulation Material",
            "Insulation Purpose",
            "Insulation Thickness",
            "Rating",
            "Reference",
            "Reporting Type",
            "Section Name",
            "Shape",
            "ShortCode",
            "Specification Description",
            "Support Assembly",
            "Support Dry Weight",
            "Support Location",
            "Type",
            "Item GUID",
            "Item Type",
            "Item Icon",
            "Item Unit",
            "Source File",
            "Triangles",
            "Primitives",
            "Lines"
        };

        /// <summary>
        /// PivotColumn 이름 → 속성 키(Category|PropertyName) 매칭용 후보 키워드
        /// 대소문자 무시, PropertyName 부분 일치 기반 매핑
        /// </summary>
        private static readonly Dictionary<string, string[]> ColumnMatchHints =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "Class", new[] { "ClassDisplayName", "Class", "Item|ClassDisplayName" } },
                { "ObjectId(GUID)", new[] { "__ObjectId" } },
                { "DisplayName", new[] { "__DisplayName" } },
                { "System Path", new[] { "__SysPath", "SysPath" } },
                { "Level", new[] { "__Level" } },
                { "Status", new[] { "Status" } },
                { "Permission Group ID", new[] { "Permission Group ID", "PermissionGroupID" } },
                { "Pipeline", new[] { "Pipeline" } },
                { "PipeRun", new[] { "PipeRun" } },
                { "RunName", new[] { "RunName", "Run Name" } },
                { "Spec Name", new[] { "Spec Name", "SpecName" } },
                { "Specification", new[] { "Specification" } },
                { "NPD", new[] { "NPD", "Nominal Pipe Diameter" } },
                { "Size", new[] { "Size" } },
                { "Description", new[] { "Description" } },
                { "Construction Type", new[] { "Construction Type", "ConstructionType" } },
                { "Location", new[] { "Location" } },
                { "Material", new[] { "Material" } },
                { "Material Grade", new[] { "Material Grade", "MaterialGrade" } },
                { "Material Name", new[] { "Material Name", "MaterialName" } },
                { "Material Type", new[] { "Material Type", "MaterialType" } },
                { "Equipment Name", new[] { "Equipment Name", "EquipmentName" } },
                { "BOM description", new[] { "BOM description", "BOM Description", "BOMDescription" } },
                { "Cardinal Point", new[] { "Cardinal Point", "CardinalPoint" } },
                { "Commodity Code", new[] { "Commodity Code", "CommodityCode" } },
                { "Cut Length", new[] { "Cut Length", "CutLength" } },
                { "Depth", new[] { "Depth" } },
                { "Height", new[] { "Height" } },
                { "Length", new[] { "Length" } },
                { "Width", new[] { "Width" } },
                { "Design Max Pressure", new[] { "Design Max Pressure", "DesignMaxPressure" } },
                { "Design Max Temperature", new[] { "Design Max Temperature", "DesignMaxTemperature" } },
                { "Dry Weight", new[] { "Dry Weight", "DryWeight" } },
                { "Wet Weight", new[] { "Wet Weight", "WetWeight" } },
                { "Weight", new[] { "Weight" } },
                { "End Prep", new[] { "End Prep", "EndPrep" } },
                { "End Standard", new[] { "End Standard", "EndStandard" } },
                { "Eqp Type 0", new[] { "Eqp Type 0", "EqpType0" } },
                { "Eqp Type 1", new[] { "Eqp Type 1", "EqpType1" } },
                { "Eqp Type 2", new[] { "Eqp Type 2", "EqpType2" } },
                { "Eqp Type 3", new[] { "Eqp Type 3", "EqpType3" } },
                { "Fire Rating", new[] { "Fire Rating", "FireRating" } },
                { "Fireproofing Label", new[] { "Fireproofing Label", "FireproofingLabel" } },
                { "Flow Direction", new[] { "Flow Direction", "FlowDirection" } },
                { "Insulation Material", new[] { "Insulation Material", "InsulationMaterial" } },
                { "Insulation Purpose", new[] { "Insulation Purpose", "InsulationPurpose" } },
                { "Insulation Thickness", new[] { "Insulation Thickness", "InsulationThickness" } },
                { "Rating", new[] { "Rating" } },
                { "Reference", new[] { "Reference" } },
                { "Reporting Type", new[] { "Reporting Type", "ReportingType" } },
                { "Section Name", new[] { "Section Name", "SectionName" } },
                { "Shape", new[] { "Shape" } },
                { "ShortCode", new[] { "ShortCode", "Short Code" } },
                { "Specification Description", new[] { "Specification Description", "SpecificationDescription" } },
                { "Support Assembly", new[] { "Support Assembly", "SupportAssembly" } },
                { "Support Dry Weight", new[] { "Support Dry Weight", "SupportDryWeight" } },
                { "Support Location", new[] { "Support Location", "SupportLocation" } },
                { "Type", new[] { "Type" } },
                { "Item GUID", new[] { "Item|GUID", "GUID" } },
                { "Item Type", new[] { "Item|Type", "Type" } },
                { "Item Icon", new[] { "Item|Icon", "Icon" } },
                { "Item Unit", new[] { "Item|Unit", "Unit" } },
                { "Source File", new[] { "Item|Source File", "Source File", "SourceFile" } },
                { "Triangles", new[] { "Geometry|Triangles", "Triangles" } },
                { "Primitives", new[] { "Geometry|Primitives", "Primitives" } },
                { "Lines", new[] { "Geometry|Lines", "Lines" } }
            };

        #endregion

        #region Public API

        /// <summary>
        /// Navisworks 활성 문서에서 데이터를 추출하여 Refined XLSX 워크북을 생성합니다.
        /// </summary>
        /// <param name="outputPath">출력 .xlsx 파일 경로</param>
        /// <param name="progress">진행률 보고</param>
        public void Export(string outputPath, IProgress<(int percentage, string message)> progress = null)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("출력 파일 경로가 유효하지 않습니다.", nameof(outputPath));

            var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("활성 Navisworks 문서가 없습니다.");

            progress?.Report((0, "모델 데이터 추출 중..."));

            // 1) 계층 구조 데이터 추출 (기존 NavisworksDataExtractor 재사용)
            var extractor = new NavisworksDataExtractor();
            var hierarchicalData = new List<HierarchicalPropertyRecord>();

            foreach (var model in doc.Models)
            {
                extractor.TraverseAndExtractProperties(model.RootItem, Guid.Empty, 0, hierarchicalData);
                WinForms.Application.DoEvents();
            }
            WinForms.Application.DoEvents();

            if (hierarchicalData.Count == 0)
                throw new InvalidOperationException("내보낼 데이터가 없습니다.");

            progress?.Report((10, $"{hierarchicalData.Count:N0}개 속성 레코드 추출 완료. 피벗 변환 중..."));
            WinForms.Application.DoEvents();

            // 2) 피벗 변환: ObjectId별로 속성을 딕셔너리로 그룹화
            var objectDataMap = BuildObjectDataMap(hierarchicalData);
            WinForms.Application.DoEvents();

            progress?.Report((25, $"{objectDataMap.Count:N0}개 객체 피벗 완료. 컬럼 매핑 중..."));
            WinForms.Application.DoEvents();

            // 3) 사용 가능한 전체 속성 키 수집 및 PivotColumn → 실제 키 매핑 구축
            var allPropertyKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var objData in objectDataMap.Values)
            {
                foreach (var key in objData.Keys)
                {
                    allPropertyKeys.Add(key);
                }
            }

            var columnKeyMap = BuildColumnKeyMap(allPropertyKeys);

            progress?.Report((30, "XLSX 워크북 생성 중..."));

            // 4) 행 데이터 구축 (Class 판별 포함)
            var rows = BuildPivotRows(objectDataMap, columnKeyMap);
            WinForms.Application.DoEvents();

            progress?.Report((45, $"{rows.Count:N0}개 행 준비 완료. 워크북 작성 중..."));
            WinForms.Application.DoEvents();

            // 5) ClosedXML 워크북 생성
            using (var workbook = new XLWorkbook())
            {
                // Sheet 1: Refining_ObjectID_Pivot
                progress?.Report((50, "Sheet 1/5: Refining_ObjectID_Pivot 작성 중..."));
                WinForms.Application.DoEvents();
                WritePivotSheet(workbook, rows);
                WinForms.Application.DoEvents();

                // Sheet 2: Pipeline_Summary
                progress?.Report((65, "Sheet 2/5: Pipeline_Summary 작성 중..."));
                WinForms.Application.DoEvents();
                WritePipelineSummarySheet(workbook, rows);
                WinForms.Application.DoEvents();

                // Sheet 3: Class_Distribution
                progress?.Report((75, "Sheet 3/5: Class_Distribution 작성 중..."));
                WriteClassDistributionSheet(workbook, rows);

                // Sheet 4: Equipment_Summary
                progress?.Report((80, "Sheet 4/5: Equipment_Summary 작성 중..."));
                WriteEquipmentSummarySheet(workbook, rows);

                // Sheet 5: PipeRun_Detail
                progress?.Report((85, "Sheet 5/5: PipeRun_Detail 작성 중..."));
                WritePipeRunDetailSheet(workbook, rows);
                WinForms.Application.DoEvents();

                // 저장
                progress?.Report((95, "파일 저장 중..."));
                WinForms.Application.DoEvents();
                workbook.SaveAs(outputPath);
            }

            progress?.Report((100, $"완료! {rows.Count:N0}개 객체, 5개 시트 저장됨"));
        }

        #endregion

        #region Data Transformation

        /// <summary>
        /// HierarchicalPropertyRecord 목록을 ObjectId별 딕셔너리로 피벗합니다.
        /// FullModelExporterService.ExportAllPropertiesToCsv 와 동일한 피벗 로직
        /// </summary>
        private Dictionary<Guid, Dictionary<string, string>> BuildObjectDataMap(
            List<HierarchicalPropertyRecord> records)
        {
            var objectDataMap = new Dictionary<Guid, Dictionary<string, string>>();

            int count = 0;
            foreach (var record in records)
            {
                string propertyKey = $"{record.Category}|{record.PropertyName}";

                if (!objectDataMap.ContainsKey(record.ObjectId))
                {
                    objectDataMap[record.ObjectId] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["__ObjectId"] = record.ObjectId.ToString(),
                        ["__ParentId"] = record.ParentId.ToString(),
                        ["__Level"] = record.Level.ToString(),
                        ["__DisplayName"] = record.DisplayName ?? string.Empty,
                        ["__SysPath"] = record.SysPath ?? string.Empty
                    };
                }

                // SysPath 가 비어있으면 첫 비어있지 않은 값으로 갱신
                if (!string.IsNullOrEmpty(record.SysPath))
                {
                    var dict = objectDataMap[record.ObjectId];
                    if (!dict.ContainsKey("__SysPath") || string.IsNullOrEmpty(dict["__SysPath"]))
                        dict["__SysPath"] = record.SysPath;
                }

                objectDataMap[record.ObjectId][propertyKey] = record.PropertyValue ?? string.Empty;

                // ContextSwitchDeadlock 방지
                count++;
                if (count % 5000 == 0)
                    WinForms.Application.DoEvents();
            }

            return objectDataMap;
        }

        /// <summary>
        /// PivotColumn 이름 → 실제 속성 키 매핑을 구축합니다.
        /// </summary>
        private Dictionary<string, string> BuildColumnKeyMap(HashSet<string> allPropertyKeys)
        {
            var columnKeyMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var pivotCol in PivotColumns)
            {
                if (!ColumnMatchHints.ContainsKey(pivotCol))
                    continue;

                var hints = ColumnMatchHints[pivotCol];
                string matchedKey = null;

                foreach (var hint in hints)
                {
                    // 1. 정확 매칭 (__ObjectId, Category|PropertyName)
                    if (allPropertyKeys.Contains(hint) || hint.StartsWith("__"))
                    {
                        matchedKey = hint;
                        break;
                    }

                    // 2. Category|PropertyName 형식 - PropertyName 부분에 hint 포함
                    foreach (var propKey in allPropertyKeys)
                    {
                        // "Category|PropertyName" 형식
                        int barIdx = propKey.IndexOf('|');
                        if (barIdx >= 0)
                        {
                            string propNamePart = propKey.Substring(barIdx + 1);

                            // hint에 | 가 있으면 Category|PropertyName 전체 매칭
                            if (hint.IndexOf('|') >= 0)
                            {
                                if (string.Equals(propKey, hint, StringComparison.OrdinalIgnoreCase))
                                {
                                    matchedKey = propKey;
                                    break;
                                }
                            }
                            else
                            {
                                // PropertyName 부분 정확 매칭
                                if (string.Equals(propNamePart, hint, StringComparison.OrdinalIgnoreCase))
                                {
                                    matchedKey = propKey;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            // "|" 없는 키 (예: __ObjectId)
                            if (string.Equals(propKey, hint, StringComparison.OrdinalIgnoreCase))
                            {
                                matchedKey = propKey;
                                break;
                            }
                        }
                    }

                    if (matchedKey != null)
                        break;
                }

                if (matchedKey != null)
                    columnKeyMap[pivotCol] = matchedKey;
            }

            return columnKeyMap;
        }

        /// <summary>
        /// 각 객체에 대해 PivotColumn 값 배열을 구성하고, Class를 판별합니다.
        /// </summary>
        private List<PivotRow> BuildPivotRows(
            Dictionary<Guid, Dictionary<string, string>> objectDataMap,
            Dictionary<string, string> columnKeyMap)
        {
            var rows = new List<PivotRow>();

            int count = 0;
            foreach (var kvp in objectDataMap)
            {
                var objData = kvp.Value;
                var values = new string[PivotColumns.Length];

                for (int i = 0; i < PivotColumns.Length; i++)
                {
                    var colName = PivotColumns[i];
                    string value = string.Empty;

                    if (columnKeyMap.ContainsKey(colName))
                    {
                        string actualKey = columnKeyMap[colName];
                        if (objData.ContainsKey(actualKey))
                        {
                            value = CleanDisplayString(objData[actualKey]);
                        }
                    }

                    values[i] = value;
                }

                // Class 판별: values[0] (Class 컬럼)
                // Class가 빈 문자열이면 SysPath 또는 DisplayName 기반으로 추론
                string classValue = values[0];
                if (string.IsNullOrWhiteSpace(classValue))
                {
                    classValue = InferClass(objData);
                    values[0] = classValue;
                }

                rows.Add(new PivotRow
                {
                    Values = values,
                    ClassName = classValue
                });

                // ContextSwitchDeadlock 방지
                count++;
                if (count % 1000 == 0)
                    WinForms.Application.DoEvents();
            }

            return rows;
        }

        /// <summary>
        /// DisplayString: 접두사를 제거합니다.
        /// </summary>
        private string CleanDisplayString(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            // "DisplayString:" 접두사 제거
            if (value.StartsWith("DisplayString:", StringComparison.OrdinalIgnoreCase))
                return value.Substring("DisplayString:".Length).Trim();

            return value;
        }

        /// <summary>
        /// SysPath 또는 카테고리 기반으로 Class를 추론합니다.
        /// </summary>
        private string InferClass(Dictionary<string, string> objData)
        {
            // SysPath 기반 추론
            string sysPath = string.Empty;
            if (objData.ContainsKey("__SysPath"))
                sysPath = objData["__SysPath"];

            string displayName = string.Empty;
            if (objData.ContainsKey("__DisplayName"))
                displayName = objData["__DisplayName"];

            string combined = (sysPath + " " + displayName).ToLower();

            // 카테고리 키에서 힌트 수집
            foreach (var key in objData.Keys)
            {
                if (key.StartsWith("__")) continue;
                combined += " " + key.ToLower();
            }

            // Pipeline 또는 PipeRun 속성 존재 시 Piping
            bool hasPipeline = false;
            bool hasEquipment = false;
            foreach (var key in objData.Keys)
            {
                string lk = key.ToLower();
                if (lk.Contains("pipeline") || lk.Contains("piperun") || lk.Contains("piping"))
                    hasPipeline = true;
                if (lk.Contains("equipment") || lk.Contains("eqp type"))
                    hasEquipment = true;
            }

            if (hasPipeline)
                return "Piping";

            if (hasEquipment)
                return "Equipment";

            if (combined.Contains("pipe") || combined.Contains("valve") ||
                combined.Contains("flange") || combined.Contains("elbow") ||
                combined.Contains("tee") || combined.Contains("reducer") ||
                combined.Contains("nozzle") || combined.Contains("coupling"))
                return "Piping";

            if (combined.Contains("equipment") || combined.Contains("vessel") ||
                combined.Contains("pump") || combined.Contains("tank") ||
                combined.Contains("compressor") || combined.Contains("exchanger") ||
                combined.Contains("heater") || combined.Contains("reactor"))
                return "Equipment";

            if (combined.Contains("struct") || combined.Contains("steel") ||
                combined.Contains("beam") || combined.Contains("column") ||
                combined.Contains("brace") || combined.Contains("foundation") ||
                combined.Contains("slab") || combined.Contains("plate") ||
                combined.Contains("grating") || combined.Contains("handrail") ||
                combined.Contains("ladder") || combined.Contains("stair"))
                return "Structure";

            if (combined.Contains("electrical") || combined.Contains("cable") ||
                combined.Contains("conduit") || combined.Contains("tray"))
                return "Electrical";

            if (combined.Contains("hvac") || combined.Contains("duct") ||
                combined.Contains("ventilat"))
                return "HVAC";

            if (combined.Contains("instrument"))
                return "Instrumentation";

            return "Other";
        }

        #endregion

        #region Sheet Writers

        /// <summary>
        /// Sheet 1: Refining_ObjectID_Pivot - 메인 피벗 데이터
        /// </summary>
        private void WritePivotSheet(XLWorkbook workbook, List<PivotRow> rows)
        {
            var ws = workbook.Worksheets.Add("Refining_ObjectID_Pivot");

            // 헤더 작성
            for (int col = 0; col < PivotColumns.Length; col++)
            {
                var cell = ws.Cell(1, col + 1);
                cell.Value = PivotColumns[col];
            }
            ApplyHeaderStyle(ws.Range(1, 1, 1, PivotColumns.Length));

            // 데이터 행 작성
            for (int r = 0; r < rows.Count; r++)
            {
                var row = rows[r];
                for (int col = 0; col < row.Values.Length; col++)
                {
                    ws.Cell(r + 2, col + 1).Value = row.Values[col] ?? string.Empty;
                }

                // Class 기반 행 배경색 적용
                if (!string.IsNullOrEmpty(row.ClassName) && ClassColorMap.ContainsKey(row.ClassName))
                {
                    string colorHex = ClassColorMap[row.ClassName];
                    var dataRange = ws.Range(r + 2, 1, r + 2, PivotColumns.Length);
                    dataRange.Style.Fill.BackgroundColor = XLColor.FromHtml(colorHex);
                }

                // UI 응답성 유지
                if (r % 500 == 0)
                    WinForms.Application.DoEvents();
            }

            // 열 너비 자동 조정 (최대 40)
            AdjustColumnWidths(ws, PivotColumns.Length, rows.Count + 1);
        }

        /// <summary>
        /// Sheet 2: Pipeline_Summary
        /// Columns: Pipeline, Objects, PipeRuns, Primary NPD, All NPDs, Specs
        /// </summary>
        private void WritePipelineSummarySheet(XLWorkbook workbook, List<PivotRow> rows)
        {
            var ws = workbook.Worksheets.Add("Pipeline_Summary");

            var headers = new[] { "Pipeline", "Objects", "PipeRuns", "Primary NPD", "All NPDs", "Specs" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(1, i + 1).Value = headers[i];
            }
            ApplyHeaderStyle(ws.Range(1, 1, 1, headers.Length));

            // Pipeline 인덱스
            int pipelineIdx = Array.IndexOf(PivotColumns, "Pipeline");
            int pipeRunIdx = Array.IndexOf(PivotColumns, "PipeRun");
            int npdIdx = Array.IndexOf(PivotColumns, "NPD");
            int specIdx = Array.IndexOf(PivotColumns, "Specification");

            if (pipelineIdx < 0) pipelineIdx = Array.IndexOf(PivotColumns, "Pipeline");

            // Pipeline별 그룹화
            var pipelineGroups = rows
                .Where(r => !string.IsNullOrWhiteSpace(GetValue(r, pipelineIdx)))
                .GroupBy(r => GetValue(r, pipelineIdx))
                .OrderBy(g => g.Key)
                .ToList();

            int dataRow = 2;
            foreach (var group in pipelineGroups)
            {
                ws.Cell(dataRow, 1).Value = group.Key;
                ws.Cell(dataRow, 2).Value = group.Count();

                var pipeRuns = group
                    .Select(r => GetValue(r, pipeRunIdx))
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Distinct()
                    .ToList();
                ws.Cell(dataRow, 3).Value = pipeRuns.Count;

                var npds = group
                    .Select(r => GetValue(r, npdIdx))
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .ToList();
                string primaryNpd = npds.GroupBy(n => n)
                    .OrderByDescending(g => g.Count())
                    .Select(g => g.Key)
                    .FirstOrDefault() ?? string.Empty;
                ws.Cell(dataRow, 4).Value = primaryNpd;
                ws.Cell(dataRow, 5).Value = string.Join(", ", npds.Distinct().OrderBy(n => n));

                var specs = group
                    .Select(r => GetValue(r, specIdx))
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Distinct()
                    .OrderBy(s => s)
                    .ToList();
                ws.Cell(dataRow, 6).Value = string.Join(", ", specs);

                dataRow++;
            }

            AdjustColumnWidths(ws, headers.Length, dataRow);
        }

        /// <summary>
        /// Sheet 3: Class_Distribution
        /// Columns: Class, Count, Percentage
        /// </summary>
        private void WriteClassDistributionSheet(XLWorkbook workbook, List<PivotRow> rows)
        {
            var ws = workbook.Worksheets.Add("Class_Distribution");

            var headers = new[] { "Class", "Count", "Percentage" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(1, i + 1).Value = headers[i];
            }
            ApplyHeaderStyle(ws.Range(1, 1, 1, headers.Length));

            var classGroups = rows
                .GroupBy(r => string.IsNullOrWhiteSpace(r.ClassName) ? "Other" : r.ClassName)
                .OrderByDescending(g => g.Count())
                .ToList();

            int total = rows.Count;
            int dataRow = 2;

            foreach (var group in classGroups)
            {
                ws.Cell(dataRow, 1).Value = group.Key;
                ws.Cell(dataRow, 2).Value = group.Count();

                double pct = total > 0 ? (double)group.Count() / total * 100.0 : 0;
                var pctCell = ws.Cell(dataRow, 3);
                pctCell.Value = pct / 100.0;
                pctCell.Style.NumberFormat.Format = "0.0%";

                // Class별 배경색 적용
                if (ClassColorMap.ContainsKey(group.Key))
                {
                    ws.Range(dataRow, 1, dataRow, headers.Length)
                        .Style.Fill.BackgroundColor = XLColor.FromHtml(ClassColorMap[group.Key]);
                }

                dataRow++;
            }

            // 합계 행
            ws.Cell(dataRow, 1).Value = "Total";
            ws.Cell(dataRow, 1).Style.Font.Bold = true;
            ws.Cell(dataRow, 2).Value = total;
            ws.Cell(dataRow, 2).Style.Font.Bold = true;
            var totalPctCell = ws.Cell(dataRow, 3);
            totalPctCell.Value = 1.0;
            totalPctCell.Style.NumberFormat.Format = "0.0%";
            totalPctCell.Style.Font.Bold = true;

            AdjustColumnWidths(ws, headers.Length, dataRow);
        }

        /// <summary>
        /// Sheet 4: Equipment_Summary
        /// Columns: Equipment Name, Objects, Types
        /// </summary>
        private void WriteEquipmentSummarySheet(XLWorkbook workbook, List<PivotRow> rows)
        {
            var ws = workbook.Worksheets.Add("Equipment_Summary");

            var headers = new[] { "Equipment Name", "Objects", "Types" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(1, i + 1).Value = headers[i];
            }
            ApplyHeaderStyle(ws.Range(1, 1, 1, headers.Length));

            int equipNameIdx = Array.IndexOf(PivotColumns, "Equipment Name");
            int typeIdx = Array.IndexOf(PivotColumns, "Type");

            var equipGroups = rows
                .Where(r => !string.IsNullOrWhiteSpace(GetValue(r, equipNameIdx)))
                .GroupBy(r => GetValue(r, equipNameIdx))
                .OrderBy(g => g.Key)
                .ToList();

            int dataRow = 2;
            foreach (var group in equipGroups)
            {
                ws.Cell(dataRow, 1).Value = group.Key;
                ws.Cell(dataRow, 2).Value = group.Count();

                var types = group
                    .Select(r => GetValue(r, typeIdx))
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Distinct()
                    .OrderBy(t => t)
                    .ToList();
                ws.Cell(dataRow, 3).Value = string.Join(", ", types);

                dataRow++;
            }

            AdjustColumnWidths(ws, headers.Length, dataRow);
        }

        /// <summary>
        /// Sheet 5: PipeRun_Detail
        /// Columns: PipeRun, Pipeline, Objects, Primary NPD, Spec, Component Types
        /// </summary>
        private void WritePipeRunDetailSheet(XLWorkbook workbook, List<PivotRow> rows)
        {
            var ws = workbook.Worksheets.Add("PipeRun_Detail");

            var headers = new[] { "PipeRun", "Pipeline", "Objects", "Primary NPD", "Spec", "Component Types" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(1, i + 1).Value = headers[i];
            }
            ApplyHeaderStyle(ws.Range(1, 1, 1, headers.Length));

            int pipeRunIdx = Array.IndexOf(PivotColumns, "PipeRun");
            int pipelineIdx = Array.IndexOf(PivotColumns, "Pipeline");
            int npdIdx = Array.IndexOf(PivotColumns, "NPD");
            int specIdx = Array.IndexOf(PivotColumns, "Specification");
            int typeIdx = Array.IndexOf(PivotColumns, "Type");
            int displayNameIdx = Array.IndexOf(PivotColumns, "DisplayName");

            var pipeRunGroups = rows
                .Where(r => !string.IsNullOrWhiteSpace(GetValue(r, pipeRunIdx)))
                .GroupBy(r => GetValue(r, pipeRunIdx))
                .OrderBy(g => g.Key)
                .ToList();

            int dataRow = 2;
            foreach (var group in pipeRunGroups)
            {
                ws.Cell(dataRow, 1).Value = group.Key;

                // Pipeline (take first non-empty)
                string pipeline = group
                    .Select(r => GetValue(r, pipelineIdx))
                    .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
                ws.Cell(dataRow, 2).Value = pipeline;

                ws.Cell(dataRow, 3).Value = group.Count();

                // Primary NPD
                var npds = group
                    .Select(r => GetValue(r, npdIdx))
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .ToList();
                string primaryNpd = npds.GroupBy(n => n)
                    .OrderByDescending(g => g.Count())
                    .Select(g => g.Key)
                    .FirstOrDefault() ?? string.Empty;
                ws.Cell(dataRow, 4).Value = primaryNpd;

                // Spec
                string spec = group
                    .Select(r => GetValue(r, specIdx))
                    .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
                ws.Cell(dataRow, 5).Value = spec;

                // Component Types (from Type or DisplayName)
                var componentTypes = group
                    .Select(r =>
                    {
                        string t = GetValue(r, typeIdx);
                        if (string.IsNullOrWhiteSpace(t))
                            t = GetValue(r, displayNameIdx);
                        return t;
                    })
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Distinct()
                    .OrderBy(t => t)
                    .ToList();
                ws.Cell(dataRow, 6).Value = string.Join(", ", componentTypes);

                dataRow++;
            }

            AdjustColumnWidths(ws, headers.Length, dataRow);
        }

        #endregion

        #region Style Helpers

        /// <summary>
        /// 헤더 행 스타일 적용: Dark blue 배경, 흰색 글자, Bold
        /// </summary>
        private void ApplyHeaderStyle(IXLRange headerRange)
        {
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Font.FontColor = XLColor.FromHtml(HeaderFontColorHex);
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml(HeaderFillHex);
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            headerRange.Style.Border.BottomBorderColor = XLColor.Black;
        }

        /// <summary>
        /// 열 너비를 데이터에 맞게 조정합니다 (최대 40자).
        /// </summary>
        private void AdjustColumnWidths(IXLWorksheet ws, int columnCount, int rowCount)
        {
            for (int col = 1; col <= columnCount; col++)
            {
                try
                {
                    ws.Column(col).AdjustToContents(1, Math.Min(rowCount, 100));
                    if (ws.Column(col).Width > 40)
                        ws.Column(col).Width = 40;
                    if (ws.Column(col).Width < 8)
                        ws.Column(col).Width = 8;
                }
                catch
                {
                    ws.Column(col).Width = 15;
                }
            }
        }

        /// <summary>
        /// PivotRow에서 지정 인덱스의 값을 안전하게 가져옵니다.
        /// </summary>
        private string GetValue(PivotRow row, int index)
        {
            if (index < 0 || index >= row.Values.Length)
                return string.Empty;
            return row.Values[index] ?? string.Empty;
        }

        #endregion

        #region Inner Types

        /// <summary>
        /// 피벗 시트의 한 행을 나타냅니다.
        /// </summary>
        private class PivotRow
        {
            public string[] Values { get; set; }
            public string ClassName { get; set; }
        }

        #endregion
    }
}
