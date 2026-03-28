using System;
using System.Collections.Generic;

namespace DXTnavis.Models
{
    /// <summary>
    /// Pipeline Schedule 자동 생성 옵션
    /// SP3D Pipeline/PipeRun 기반 TimeLiner 스케줄 자동화 설정
    /// </summary>
    public class PipelineScheduleOptions
    {
        #region Data Source

        /// <summary>
        /// AllProperties CSV 파일 경로 (Pipeline/PipeRun 속성 포함)
        /// </summary>
        public string AllPropertiesCsvPath { get; set; }

        /// <summary>
        /// Geometry CSV 파일 경로 (CentroidX 기반 공간 정렬용, 선택)
        /// </summary>
        public string GeometryCsvPath { get; set; }

        #endregion

        #region Time Mapping

        /// <summary>
        /// 시간 매핑 전략
        /// </summary>
        public TimeStrategy TimeStrategy { get; set; } = TimeStrategy.Hybrid;

        /// <summary>
        /// 프로젝트 시작일
        /// </summary>
        public DateTime ProjectStartDate { get; set; } = DateTime.Today.AddDays(1);

        /// <summary>
        /// PipeRun당 기본 시간 (시간 단위)
        /// </summary>
        public double BaseDurationHours { get; set; } = 8.0;

        /// <summary>
        /// 객체당 추가 시간 (시간 단위)
        /// </summary>
        public double HoursPerObject { get; set; } = 0.5;

        /// <summary>
        /// 하루 작업 시간
        /// </summary>
        public double WorkHoursPerDay { get; set; } = 8.0;

        /// <summary>
        /// Pipeline 간 간격 (일)
        /// </summary>
        public int GapDaysBetweenPipelines { get; set; } = 1;

        #endregion

        #region Task Granularity

        /// <summary>
        /// Task 생성 단위: PipeRun별 또는 객체별
        /// </summary>
        public TaskGranularity TaskGranularity { get; set; } = TaskGranularity.ByPipeRun;

        #endregion

        #region Ordering

        /// <summary>
        /// 정렬 전략
        /// </summary>
        public OrderingStrategy OrderingStrategy { get; set; } = OrderingStrategy.SpatialLeftToRight;

        #endregion

        #region Output

        /// <summary>
        /// 기본 Task 유형
        /// </summary>
        public string TaskType { get; set; } = "Construct";

        /// <summary>
        /// TimeLiner 루트 폴더명
        /// </summary>
        public string TimeLinerRootFolder { get; set; } = "Pipeline Schedule";

        /// <summary>
        /// Selection Set 루트 폴더명
        /// </summary>
        public string SelectionSetRootFolder { get; set; } = "Pipeline Sets";

        #endregion
    }

    /// <summary>
    /// 시간 매핑 전략
    /// </summary>
    public enum TimeStrategy
    {
        /// <summary>모든 PipeRun에 고정 시간</summary>
        FixedDuration,
        /// <summary>객체 수 기반 시간 계산</summary>
        ObjectCountBased,
        /// <summary>기본 시간 + 객체당 추가 시간 (권장)</summary>
        Hybrid
    }

    /// <summary>
    /// Task 생성 단위
    /// </summary>
    public enum TaskGranularity
    {
        /// <summary>PipeRun 1개 = Task 1개 (그룹 단위)</summary>
        ByPipeRun,
        /// <summary>객체 1개 = Task 1개 (개별 순서 부여)</summary>
        ByObject
    }

    /// <summary>
    /// 정렬 전략
    /// </summary>
    public enum OrderingStrategy
    {
        /// <summary>알파벳순 정렬</summary>
        Alphabetical,
        /// <summary>CentroidX 기반 왼쪽→오른쪽 정렬</summary>
        SpatialLeftToRight,
        /// <summary>객체 수 기준 정렬</summary>
        ByObjectCount
    }

    #region Internal Group Models

    /// <summary>
    /// Pipeline 그룹 (최상위 계층)
    /// </summary>
    public class PipelineGroup
    {
        public string PipelineName { get; set; }
        public List<PipeRunGroup> PipeRuns { get; set; } = new List<PipeRunGroup>();
        public double AverageCentroidX { get; set; }
    }

    /// <summary>
    /// PipeRun 그룹 (Pipeline 하위)
    /// </summary>
    public class PipeRunGroup
    {
        public string PipeRunName { get; set; }
        public List<PipelineObject> Objects { get; set; } = new List<PipelineObject>();
        public DateTime PlannedStart { get; set; }
        public DateTime PlannedEnd { get; set; }
        public double AverageCentroidX { get; set; }
    }

    /// <summary>
    /// Pipeline 내 개별 객체
    /// </summary>
    public class PipelineObject
    {
        public Guid ObjectId { get; set; }
        public string DisplayName { get; set; }
        public string Pipeline { get; set; }
        public string PipeRun { get; set; }
        public double CentroidX { get; set; }
        public DateTime PlannedStart { get; set; }
        public DateTime PlannedEnd { get; set; }
    }

    /// <summary>
    /// Pipeline Schedule 빌드 결과
    /// </summary>
    public class PipelineScheduleResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public List<ScheduleData> Schedules { get; set; } = new List<ScheduleData>();
        public int PipelineCount { get; set; }
        public int PipeRunCount { get; set; }
        public int TotalObjectCount { get; set; }
        public int TotalDurationDays { get; set; }
        public DateTime? ScheduleStart { get; set; }
        public DateTime? ScheduleEnd { get; set; }
    }

    #endregion
}
