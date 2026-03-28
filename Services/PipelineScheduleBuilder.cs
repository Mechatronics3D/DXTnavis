using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DXTnavis.Models;

namespace DXTnavis.Services
{
    /// <summary>
    /// Pipeline Schedule Builder
    /// SP3D Pipeline/PipeRun 속성을 기반으로 TimeLiner 스케줄 자동 생성
    /// AllProperties CSV에서 Pipeline/PipeRun을 추출하고 시간을 매핑하여 List&lt;ScheduleData&gt;를 생성
    /// </summary>
    public class PipelineScheduleBuilder
    {
        /// <summary>
        /// 진행률 변경 이벤트
        /// </summary>
        public event EventHandler<PipelineScheduleProgressEventArgs> ProgressChanged;

        #region Public Methods

        /// <summary>
        /// CSV에서 Pipeline Schedule 생성
        /// </summary>
        public PipelineScheduleResult BuildFromCsv(PipelineScheduleOptions options)
        {
            var result = new PipelineScheduleResult();

            try
            {
                if (string.IsNullOrEmpty(options.AllPropertiesCsvPath) ||
                    !File.Exists(options.AllPropertiesCsvPath))
                {
                    result.ErrorMessage = "AllProperties CSV 파일을 찾을 수 없습니다.";
                    return result;
                }

                ReportProgress(0, "CSV 파일에서 Pipeline/PipeRun 추출 중...");

                // Step 1: CSV에서 Pipeline/PipeRun 그룹 추출
                var groups = ExtractAndGroupFromCsv(options.AllPropertiesCsvPath);
                if (groups.Count == 0)
                {
                    result.ErrorMessage = "Pipeline 데이터를 찾을 수 없습니다. CSV에 Pipeline/PipeRun 컬럼이 있는지 확인하세요.";
                    return result;
                }

                ReportProgress(30, $"{groups.Count}개 Pipeline, {groups.Sum(g => g.PipeRuns.Count)}개 PipeRun 발견");

                // Step 2: Geometry CSV에서 CentroidX 로드 (선택)
                Dictionary<Guid, double> centroidXMap = null;
                if (!string.IsNullOrEmpty(options.GeometryCsvPath) && File.Exists(options.GeometryCsvPath))
                {
                    ReportProgress(40, "Geometry CSV에서 좌표 데이터 로드 중...");
                    centroidXMap = LoadCentroidsFromCsv(options.GeometryCsvPath);
                }

                // Step 3: 정렬
                ReportProgress(50, "Pipeline/PipeRun 정렬 중...");
                ApplyOrdering(groups, options, centroidXMap);

                // Step 4: 시간 매핑
                ReportProgress(60, "시간 매핑 중...");
                ApplyTimeMapping(groups, options);

                // Step 5: ScheduleData 변환
                ReportProgress(80, "ScheduleData 변환 중...");
                var schedules = ConvertToScheduleData(groups, options);

                // 결과 설정
                result.Success = true;
                result.Schedules = schedules;
                result.PipelineCount = groups.Count;
                result.PipeRunCount = groups.Sum(g => g.PipeRuns.Count);
                result.TotalObjectCount = groups.Sum(g => g.PipeRuns.Sum(pr => pr.Objects.Count));

                if (schedules.Count > 0)
                {
                    result.ScheduleStart = schedules.Min(s => s.PlannedStartDate);
                    result.ScheduleEnd = schedules.Max(s => s.PlannedEndDate);
                    result.TotalDurationDays = result.ScheduleEnd.HasValue && result.ScheduleStart.HasValue
                        ? (int)(result.ScheduleEnd.Value - result.ScheduleStart.Value).TotalDays
                        : 0;
                }

                ReportProgress(100, $"완료: {result.PipeRunCount}개 Task 생성, {result.TotalDurationDays}일 기간");
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Pipeline Schedule 생성 오류: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// TimeLiner CSV 내보내기
        /// Navisworks TimeLiner 필드 선택기(Field Selector)에 직접 매핑 가능한 포맷
        /// 컬럼명이 필드 선택기 필드명과 1:1 일치하여 자동 매핑됨
        /// </summary>
        public void ExportTimeLinerCsv(List<ScheduleData> schedules, string outputPath)
        {
            var sb = new StringBuilder();

            // 필드 선택기 필드명과 정확히 일치하는 헤더
            sb.AppendLine("작업 이름,동기화 ID,작업 유형,계획된 시작 날짜,계획된 끝 날짜");

            int syncCounter = 1;
            foreach (var schedule in schedules)
            {
                // 작업 이름: ParentSet\TaskName (backslash = TimeLiner 계층 구조)
                // 예: "03-BFW-2001\Recovery Stage 2-6-X-0002-1C0031"
                //   → Parent Task: "03-BFW-2001"
                //   → Child Task: "Recovery Stage 2-6-X-0002-1C0031" (= Selection Set 이름과 일치)
                var taskPath = !string.IsNullOrEmpty(schedule.ParentSet)
                    ? $"{schedule.ParentSet}\\{schedule.TaskName}"
                    : schedule.TaskName;

                // 작업 유형: 한글
                var taskTypeKr = ConvertTaskTypeToKorean(schedule.TaskType);

                // 날짜: yyyy-MM-dd (시간 없이 — Navisworks 필드 선택기 호환 형식)
                // Navisworks 네이티브는 "2026-03-23 오전 9:00:00" 형식이지만,
                // 날짜만 제공하면 Navisworks가 기본 시간으로 자동 설정
                var plannedStart = schedule.PlannedStartDate?.ToString("yyyy-MM-dd") ?? "";
                var plannedEnd = schedule.PlannedEndDate?.ToString("yyyy-MM-dd") ?? "";

                sb.AppendLine(string.Join(",",
                    EscapeCsv(taskPath),     // 작업 이름
                    syncCounter.ToString(),  // 동기화 ID (순차 번호)
                    taskTypeKr,              // 작업 유형
                    plannedStart,            // 계획된 시작 날짜
                    plannedEnd               // 계획된 끝 날짜
                ));

                syncCounter++;
            }

            // CP949 인코딩 (Navisworks 한글 호환)
            File.WriteAllText(outputPath, sb.ToString(), Encoding.GetEncoding(949));
        }

        /// <summary>
        /// TaskType을 한글로 변환
        /// </summary>
        private string ConvertTaskTypeToKorean(string taskType)
        {
            if (string.IsNullOrEmpty(taskType))
                return "구성";

            switch (taskType.ToLowerInvariant())
            {
                case "construct": return "구성";
                case "demolish": return "철거";
                case "temporary": return "임시";
                default: return "구성";
            }
        }

        /// <summary>
        /// CSV 필드 이스케이프
        /// </summary>
        private string EscapeCsv(string field)
        {
            if (string.IsNullOrEmpty(field)) return "";
            if (field.Contains(",") || field.Contains("\"") || field.Contains("\n"))
            {
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            }
            return field;
        }

        #endregion

        #region CSV Parsing

        /// <summary>
        /// AllProperties CSV에서 Pipeline/PipeRun 그룹 추출
        /// </summary>
        private List<PipelineGroup> ExtractAndGroupFromCsv(string csvPath)
        {
            var objects = new List<PipelineObject>();
            var lines = File.ReadAllLines(csvPath, DetectEncoding(csvPath));

            if (lines.Length < 2) return new List<PipelineGroup>();

            // 헤더에서 Pipeline, PipeRun, ObjectId 컬럼 인덱스 찾기
            var headers = ParseCsvLine(lines[0]);
            int pipelineIdx = FindColumnIndex(headers, "Pipeline", "pipeline", "PipeLine");
            int pipeRunIdx = FindColumnIndex(headers, "PipeRun", "piperun", "PipelineRun", "Pipe Run");
            int objectIdIdx = FindColumnIndex(headers, "ObjectId", "objectid", "Object Id", "GUID", "guid", "InstanceGuid");
            int displayNameIdx = FindColumnIndex(headers, "DisplayName", "displayname", "Display Name", "Name", "name");

            // Pipeline/PipeRun 컬럼을 찾지 못한 경우 모든 컬럼에서 검색
            if (pipelineIdx < 0 || pipeRunIdx < 0)
            {
                // 값 기반으로 Pipeline/PipeRun 컬럼 추정
                var detected = DetectPipelineColumns(lines, headers);
                if (detected.pipelineIdx >= 0) pipelineIdx = detected.pipelineIdx;
                if (detected.pipeRunIdx >= 0) pipeRunIdx = detected.pipeRunIdx;
            }

            if (pipelineIdx < 0 && pipeRunIdx < 0)
            {
                return new List<PipelineGroup>();
            }

            // 데이터 행 파싱
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;

                var cols = ParseCsvLine(lines[i]);

                string pipeline = pipelineIdx >= 0 && pipelineIdx < cols.Length
                    ? StripDisplayString(cols[pipelineIdx])
                    : "";
                string pipeRun = pipeRunIdx >= 0 && pipeRunIdx < cols.Length
                    ? StripDisplayString(cols[pipeRunIdx])
                    : "";

                // Pipeline 또는 PipeRun 값이 없는 행은 건너뛰기
                if (string.IsNullOrWhiteSpace(pipeline) && string.IsNullOrWhiteSpace(pipeRun))
                    continue;

                // Pipeline이 비어있으면 PipeRun에서 추출 시도
                if (string.IsNullOrWhiteSpace(pipeline))
                    pipeline = "Unknown Pipeline";

                if (string.IsNullOrWhiteSpace(pipeRun))
                    pipeRun = "Unknown PipeRun";

                string objectIdStr = objectIdIdx >= 0 && objectIdIdx < cols.Length
                    ? cols[objectIdIdx].Trim()
                    : "";
                string displayName = displayNameIdx >= 0 && displayNameIdx < cols.Length
                    ? StripDisplayString(cols[displayNameIdx])
                    : $"Object_{i}";

                Guid objectId;
                if (!Guid.TryParse(objectIdStr, out objectId))
                {
                    // GUID가 아닌 경우 행 번호 기반 생성
                    objectId = GenerateDeterministicGuid(csvPath, i);
                }

                objects.Add(new PipelineObject
                {
                    ObjectId = objectId,
                    DisplayName = displayName,
                    Pipeline = pipeline,
                    PipeRun = pipeRun,
                    CentroidX = 0
                });
            }

            // Pipeline → PipeRun → Objects 그룹핑
            var groups = objects
                .GroupBy(o => o.Pipeline)
                .Select(pipelineGroup => new PipelineGroup
                {
                    PipelineName = pipelineGroup.Key,
                    PipeRuns = pipelineGroup
                        .GroupBy(o => o.PipeRun)
                        .Select(pipeRunGroup => new PipeRunGroup
                        {
                            PipeRunName = pipeRunGroup.Key,
                            Objects = pipeRunGroup.ToList()
                        })
                        .ToList()
                })
                .ToList();

            return groups;
        }

        /// <summary>
        /// AllProperties CSV에서 Pipeline/PipeRun 컬럼을 값 패턴으로 자동 감지
        /// </summary>
        private (int pipelineIdx, int pipeRunIdx) DetectPipelineColumns(string[] lines, string[] headers)
        {
            int pipelineIdx = -1;
            int pipeRunIdx = -1;

            // 헤더에서 SP3D 관련 키워드 검색
            for (int i = 0; i < headers.Length; i++)
            {
                var header = headers[i].Trim().ToLowerInvariant();
                if (header.Contains("pipeline") && !header.Contains("run"))
                    pipelineIdx = i;
                else if (header.Contains("piperun") || header.Contains("pipe run") || header.Contains("pipe_run"))
                    pipeRunIdx = i;
            }

            // 헤더에서 찾지 못한 경우 값 패턴으로 추정
            if (pipelineIdx < 0 || pipeRunIdx < 0)
            {
                int sampleSize = Math.Min(50, lines.Length - 1);
                for (int col = 0; col < headers.Length; col++)
                {
                    int pipelinePatternCount = 0;
                    int pipeRunPatternCount = 0;

                    for (int row = 1; row <= sampleSize; row++)
                    {
                        var cols = ParseCsvLine(lines[row]);
                        if (col >= cols.Length) continue;

                        var val = StripDisplayString(cols[col]).Trim();
                        if (string.IsNullOrEmpty(val)) continue;

                        // Pipeline 패턴: "P-015", "P-001" 형태
                        if (System.Text.RegularExpressions.Regex.IsMatch(val, @"^[A-Z]-\d{3}$"))
                            pipelinePatternCount++;

                        // PipeRun 패턴: 더 긴 이름 패턴
                        if (val.Contains("Dist.") || val.Contains("Unit") ||
                            System.Text.RegularExpressions.Regex.IsMatch(val, @"^.*-P-\d{4}$"))
                            pipeRunPatternCount++;
                    }

                    if (pipelineIdx < 0 && pipelinePatternCount > sampleSize * 0.3)
                        pipelineIdx = col;
                    if (pipeRunIdx < 0 && pipeRunPatternCount > sampleSize * 0.3)
                        pipeRunIdx = col;
                }
            }

            return (pipelineIdx, pipeRunIdx);
        }

        /// <summary>
        /// Geometry CSV에서 CentroidX 값 로드
        /// </summary>
        private Dictionary<Guid, double> LoadCentroidsFromCsv(string geometryCsvPath)
        {
            var map = new Dictionary<Guid, double>();

            try
            {
                var lines = File.ReadAllLines(geometryCsvPath, DetectEncoding(geometryCsvPath));
                if (lines.Length < 2) return map;

                var headers = ParseCsvLine(lines[0]);
                int objectIdIdx = FindColumnIndex(headers, "ObjectId", "objectid", "GUID", "guid");
                int centroidXIdx = FindColumnIndex(headers, "CentroidX", "centroidx", "Centroid_X", "CenterX");

                if (objectIdIdx < 0 || centroidXIdx < 0) return map;

                for (int i = 1; i < lines.Length; i++)
                {
                    var cols = ParseCsvLine(lines[i]);
                    if (objectIdIdx >= cols.Length || centroidXIdx >= cols.Length) continue;

                    Guid id;
                    double cx;
                    if (Guid.TryParse(cols[objectIdIdx].Trim(), out id) &&
                        double.TryParse(cols[centroidXIdx].Trim(), out cx))
                    {
                        map[id] = cx;
                    }
                }
            }
            catch
            {
                // Geometry CSV 로드 실패 시 빈 맵 반환
            }

            return map;
        }

        #endregion

        #region Ordering

        /// <summary>
        /// Pipeline/PipeRun 정렬 적용
        /// </summary>
        private void ApplyOrdering(List<PipelineGroup> groups, PipelineScheduleOptions options,
                                    Dictionary<Guid, double> centroidXMap)
        {
            // CentroidX 값을 객체에 적용
            if (centroidXMap != null && centroidXMap.Count > 0)
            {
                foreach (var pipeline in groups)
                {
                    foreach (var pipeRun in pipeline.PipeRuns)
                    {
                        foreach (var obj in pipeRun.Objects)
                        {
                            double cx;
                            if (centroidXMap.TryGetValue(obj.ObjectId, out cx))
                                obj.CentroidX = cx;
                        }
                        pipeRun.AverageCentroidX = pipeRun.Objects.Count > 0
                            ? pipeRun.Objects.Average(o => o.CentroidX)
                            : 0;
                    }
                    pipeline.AverageCentroidX = pipeline.PipeRuns.Count > 0
                        ? pipeline.PipeRuns.Average(pr => pr.AverageCentroidX)
                        : 0;
                }
            }

            switch (options.OrderingStrategy)
            {
                case OrderingStrategy.SpatialLeftToRight:
                    if (centroidXMap != null && centroidXMap.Count > 0)
                    {
                        groups.Sort((a, b) => a.AverageCentroidX.CompareTo(b.AverageCentroidX));
                        foreach (var g in groups)
                            g.PipeRuns.Sort((a, b) => a.AverageCentroidX.CompareTo(b.AverageCentroidX));
                    }
                    else
                    {
                        // Fallback to alphabetical
                        groups.Sort((a, b) => string.Compare(a.PipelineName, b.PipelineName, StringComparison.OrdinalIgnoreCase));
                        foreach (var g in groups)
                            g.PipeRuns.Sort((a, b) => string.Compare(a.PipeRunName, b.PipeRunName, StringComparison.OrdinalIgnoreCase));
                    }
                    break;

                case OrderingStrategy.Alphabetical:
                    groups.Sort((a, b) => string.Compare(a.PipelineName, b.PipelineName, StringComparison.OrdinalIgnoreCase));
                    foreach (var g in groups)
                        g.PipeRuns.Sort((a, b) => string.Compare(a.PipeRunName, b.PipeRunName, StringComparison.OrdinalIgnoreCase));
                    break;

                case OrderingStrategy.ByObjectCount:
                    groups.Sort((a, b) => b.PipeRuns.Sum(pr => pr.Objects.Count)
                        .CompareTo(a.PipeRuns.Sum(pr => pr.Objects.Count)));
                    foreach (var g in groups)
                        g.PipeRuns.Sort((a, b) => b.Objects.Count.CompareTo(a.Objects.Count));
                    break;
            }
        }

        #endregion

        #region Time Mapping

        /// <summary>
        /// 시간 매핑 적용
        /// duration_hours = BaseDurationHours + (objectCount × HoursPerObject)
        /// </summary>
        private void ApplyTimeMapping(List<PipelineGroup> groups, PipelineScheduleOptions options)
        {
            DateTime currentDate = options.ProjectStartDate;

            foreach (var pipeline in groups)
            {
                foreach (var pipeRun in pipeline.PipeRuns)
                {
                    if (options.TaskGranularity == TaskGranularity.ByObject)
                    {
                        // ByObject: 각 객체에 개별 일정 순차 배치
                        double objDuration = options.BaseDurationHours + options.HoursPerObject;
                        double objDays = Math.Max(objDuration / options.WorkHoursPerDay, 0.5);
                        double ceilDays = Math.Ceiling(objDays);

                        pipeRun.PlannedStart = currentDate;

                        foreach (var obj in pipeRun.Objects)
                        {
                            obj.PlannedStart = currentDate;
                            obj.PlannedEnd = currentDate.AddDays(ceilDays);
                            currentDate = obj.PlannedEnd;
                        }

                        pipeRun.PlannedEnd = currentDate;
                    }
                    else
                    {
                        // ByPipeRun: PipeRun 단위 일정 (기존 로직)
                        double durationHours;

                        switch (options.TimeStrategy)
                        {
                            case TimeStrategy.FixedDuration:
                                durationHours = options.BaseDurationHours;
                                break;
                            case TimeStrategy.ObjectCountBased:
                                durationHours = pipeRun.Objects.Count * options.HoursPerObject;
                                durationHours = Math.Max(durationHours, options.WorkHoursPerDay);
                                break;
                            case TimeStrategy.Hybrid:
                            default:
                                durationHours = options.BaseDurationHours + (pipeRun.Objects.Count * options.HoursPerObject);
                                break;
                        }

                        double durationDays = durationHours / options.WorkHoursPerDay;
                        durationDays = Math.Max(durationDays, 0.5);

                        pipeRun.PlannedStart = currentDate;
                        pipeRun.PlannedEnd = currentDate.AddDays(Math.Ceiling(durationDays));
                        currentDate = pipeRun.PlannedEnd;
                    }
                }

                // Pipeline 간 간격
                currentDate = currentDate.AddDays(options.GapDaysBetweenPipelines);
            }
        }

        #endregion

        #region ScheduleData Conversion

        /// <summary>
        /// PipelineGroup을 ScheduleData 리스트로 변환
        /// TaskGranularity에 따라 PipeRun 단위 또는 객체 단위로 생성
        /// </summary>
        private List<ScheduleData> ConvertToScheduleData(List<PipelineGroup> groups, PipelineScheduleOptions options)
        {
            var schedules = new List<ScheduleData>();

            foreach (var pipeline in groups)
            {
                foreach (var pipeRun in pipeline.PipeRuns)
                {
                    if (options.TaskGranularity == TaskGranularity.ByObject)
                    {
                        // ByObject: 객체 1개 = ScheduleData 1개
                        foreach (var obj in pipeRun.Objects)
                        {
                            var schedule = new ScheduleData
                            {
                                SyncID = obj.ObjectId.ToString(),
                                TaskName = obj.DisplayName ?? obj.ObjectId.ToString().Substring(0, 8),
                                PlannedStartDate = obj.PlannedStart,
                                PlannedEndDate = obj.PlannedEnd,
                                ActualStartDate = obj.PlannedStart,
                                ActualEndDate = obj.PlannedEnd,
                                TaskType = options.TaskType ?? "Construct",
                                ParentSet = pipeline.PipelineName + "/" + pipeRun.PipeRunName,
                                Progress = 0,
                                CustomProperties = new Dictionary<string, string>
                                {
                                    ["ObjectIds"] = obj.ObjectId.ToString(),
                                    ["ObjectCount"] = "1",
                                    ["Pipeline"] = pipeline.PipelineName,
                                    ["PipeRun"] = pipeRun.PipeRunName
                                }
                            };
                            schedules.Add(schedule);
                        }
                    }
                    else
                    {
                        // ByPipeRun: PipeRun 1개 = ScheduleData 1개 (기존 로직)
                        var objectIds = string.Join(";", pipeRun.Objects.Select(o => o.ObjectId.ToString()));

                        // TaskName = "Pipeline_PipeRun" (유니크 이름)
                        // 같은 PipeRun 이름이 다른 Pipeline에 존재할 수 있으므로 접두사 필수
                        // 이 이름이 CSV leaf task name + Selection Set name으로 동시 사용됨
                        var uniqueTaskName = $"{pipeline.PipelineName}_{pipeRun.PipeRunName}";

                        var schedule = new ScheduleData
                        {
                            SyncID = pipeRun.PipeRunName,
                            TaskName = uniqueTaskName,
                            PlannedStartDate = pipeRun.PlannedStart,
                            PlannedEndDate = pipeRun.PlannedEnd,
                            ActualStartDate = pipeRun.PlannedStart,
                            ActualEndDate = pipeRun.PlannedEnd,
                            TaskType = options.TaskType ?? "Construct",
                            ParentSet = pipeline.PipelineName,
                            Progress = 0,
                            CustomProperties = new Dictionary<string, string>
                            {
                                ["ObjectIds"] = objectIds,
                                ["ObjectCount"] = pipeRun.Objects.Count.ToString(),
                                ["Pipeline"] = pipeline.PipelineName,
                                ["PipeRun"] = pipeRun.PipeRunName
                            }
                        };
                        schedules.Add(schedule);
                    }
                }
            }

            return schedules;
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// "DisplayString:P-015" → "P-015" 변환
        /// </summary>
        private string StripDisplayString(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return "";

            raw = raw.Trim();

            // "DisplayString:" 접두사 제거
            const string prefix = "DisplayString:";
            if (raw.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return raw.Substring(prefix.Length).Trim();

            // 따옴표 제거
            if (raw.StartsWith("\"") && raw.EndsWith("\"") && raw.Length >= 2)
                raw = raw.Substring(1, raw.Length - 2);

            return raw.Trim();
        }

        /// <summary>
        /// CSV 행을 컬럼 배열로 파싱 (따옴표 처리 포함)
        /// </summary>
        private string[] ParseCsvLine(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            var current = new StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            result.Add(current.ToString());
            return result.ToArray();
        }

        /// <summary>
        /// 헤더에서 컬럼 인덱스 찾기 (대소문자 무시, 여러 별칭 지원)
        /// </summary>
        private int FindColumnIndex(string[] headers, params string[] aliases)
        {
            for (int i = 0; i < headers.Length; i++)
            {
                var header = headers[i].Trim().Trim('"');
                foreach (var alias in aliases)
                {
                    if (string.Equals(header, alias, StringComparison.OrdinalIgnoreCase))
                        return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// 파일 인코딩 감지 (UTF-8 BOM, UTF-8, EUC-KR)
        /// </summary>
        private Encoding DetectEncoding(string filePath)
        {
            var bom = new byte[4];
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                fs.Read(bom, 0, 4);
            }

            // UTF-8 BOM
            if (bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
                return Encoding.UTF8;

            // UTF-16 LE BOM
            if (bom[0] == 0xFF && bom[1] == 0xFE)
                return Encoding.Unicode;

            // Default: UTF-8
            return Encoding.UTF8;
        }

        /// <summary>
        /// 결정론적 GUID 생성 (파일 경로 + 행 번호 기반)
        /// </summary>
        private Guid GenerateDeterministicGuid(string source, int index)
        {
            var bytes = Encoding.UTF8.GetBytes($"{source}:{index}");
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                var hash = md5.ComputeHash(bytes);
                return new Guid(hash);
            }
        }

        /// <summary>
        /// 진행률 보고
        /// </summary>
        private void ReportProgress(int percentage, string message)
        {
            ProgressChanged?.Invoke(this, new PipelineScheduleProgressEventArgs
            {
                Percentage = percentage,
                Message = message
            });
        }

        #endregion
    }

    /// <summary>
    /// Pipeline Schedule 진행률 이벤트 인자
    /// </summary>
    public class PipelineScheduleProgressEventArgs : EventArgs
    {
        public int Percentage { get; set; }
        public string Message { get; set; }
    }
}
