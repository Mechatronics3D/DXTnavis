using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using DXTnavis.Helpers;
using DXTnavis.Models;
using DXTnavis.Services;
using Microsoft.Win32;

namespace DXTnavis.ViewModels
{
    /// <summary>
    /// Pipeline Schedule ViewModel
    /// SP3D Pipeline/PipeRun 기반 4D 시뮬레이션 자동 생성 UI 바인딩
    /// </summary>
    public class PipelineScheduleViewModel : INotifyPropertyChanged
    {
        #region Fields

        private readonly PipelineScheduleBuilder _builder;
        private readonly ObjectMatcher _objectMatcher;
        private readonly SelectionSetService _selectionSetService;
        private readonly TimeLinerService _timeLinerService;

        private string _csvPath;
        private string _geometryCsvPath;
        private DateTime _startDate = DateTime.Today.AddDays(1);
        private double _baseDuration = 8;
        private double _perObjectHours = 0.5;
        private int _gapDays = 1;
        private string _selectedTimeStrategy = "Hybrid";
        private string _selectedOrderingStrategy = "알파벳순";
        private bool _isDryRun;
        private bool _isExecuting;
        private int _progress;
        private string _statusMessage = "AllProperties CSV 파일을 선택하세요.";

        private ObservableCollection<PipelinePreviewItem> _previewItems;
        private PipelineScheduleResult _lastResult;

        #endregion

        #region Constructor

        public PipelineScheduleViewModel()
        {
            _builder = new PipelineScheduleBuilder();
            _objectMatcher = new ObjectMatcher();
            _selectionSetService = new SelectionSetService();
            _timeLinerService = new TimeLinerService(_selectionSetService);

            _previewItems = new ObservableCollection<PipelinePreviewItem>();

            // Progress event
            _builder.ProgressChanged += (s, e) =>
            {
                Progress = e.Percentage;
                StatusMessage = e.Message;
            };

            // Commands
            BrowseCsvCommand = new RelayCommand(BrowseCsv);
            BrowseGeometryCsvCommand = new RelayCommand(BrowseGeometryCsv);
            PreviewCommand = new RelayCommand(ExecutePreview, () => !string.IsNullOrEmpty(CsvPath));
            ExecuteCommand = new RelayCommand(ExecuteTimeLiner, () => CanExecute);
            ExportCsvCommand = new RelayCommand(ExportCsv, () => _lastResult?.Schedules?.Count > 0);

            // Strategy options
            TimeStrategies = new List<string> { "Hybrid", "FixedDuration", "ObjectCountBased" };
            OrderingStrategies = new List<string> { "알파벳순", "공간정렬(좌→우)", "객체수 기준" };
        }

        #endregion

        #region Properties

        /// <summary>
        /// AllProperties CSV 파일 경로
        /// </summary>
        public string CsvPath
        {
            get => _csvPath;
            set
            {
                _csvPath = value;
                OnPropertyChanged(nameof(CsvPath));
                ((RelayCommand)PreviewCommand).RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// Geometry CSV 파일 경로 (선택)
        /// </summary>
        public string GeometryCsvPath
        {
            get => _geometryCsvPath;
            set
            {
                _geometryCsvPath = value;
                OnPropertyChanged(nameof(GeometryCsvPath));
            }
        }

        /// <summary>
        /// 프로젝트 시작일
        /// </summary>
        public DateTime StartDate
        {
            get => _startDate;
            set
            {
                _startDate = value;
                OnPropertyChanged(nameof(StartDate));
            }
        }

        /// <summary>
        /// PipeRun당 기본 시간(h)
        /// </summary>
        public double BaseDuration
        {
            get => _baseDuration;
            set
            {
                _baseDuration = Math.Max(0.5, value);
                OnPropertyChanged(nameof(BaseDuration));
            }
        }

        /// <summary>
        /// 객체당 추가 시간(h)
        /// </summary>
        public double PerObjectHours
        {
            get => _perObjectHours;
            set
            {
                _perObjectHours = Math.Max(0, value);
                OnPropertyChanged(nameof(PerObjectHours));
            }
        }

        /// <summary>
        /// Pipeline 간 간격(일)
        /// </summary>
        public int GapDays
        {
            get => _gapDays;
            set
            {
                _gapDays = Math.Max(0, value);
                OnPropertyChanged(nameof(GapDays));
            }
        }

        /// <summary>
        /// 시간 매핑 전략 목록
        /// </summary>
        public List<string> TimeStrategies { get; }

        /// <summary>
        /// 선택된 시간 매핑 전략
        /// </summary>
        public string SelectedTimeStrategy
        {
            get => _selectedTimeStrategy;
            set
            {
                _selectedTimeStrategy = value;
                OnPropertyChanged(nameof(SelectedTimeStrategy));
            }
        }

        /// <summary>
        /// 정렬 전략 목록
        /// </summary>
        public List<string> OrderingStrategies { get; }

        /// <summary>
        /// 선택된 정렬 전략
        /// </summary>
        public string SelectedOrderingStrategy
        {
            get => _selectedOrderingStrategy;
            set
            {
                _selectedOrderingStrategy = value;
                OnPropertyChanged(nameof(SelectedOrderingStrategy));
            }
        }

        /// <summary>
        /// DryRun 모드
        /// </summary>
        public bool IsDryRun
        {
            get => _isDryRun;
            set
            {
                _isDryRun = value;
                OnPropertyChanged(nameof(IsDryRun));
            }
        }

        /// <summary>
        /// 실행 중 여부
        /// </summary>
        public bool IsExecuting
        {
            get => _isExecuting;
            set
            {
                _isExecuting = value;
                OnPropertyChanged(nameof(IsExecuting));
                OnPropertyChanged(nameof(CanExecute));
                ((RelayCommand)ExecuteCommand)?.RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// 진행률 (0-100)
        /// </summary>
        public int Progress
        {
            get => _progress;
            set
            {
                _progress = value;
                OnPropertyChanged(nameof(Progress));
            }
        }

        /// <summary>
        /// 상태 메시지
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged(nameof(StatusMessage));
            }
        }

        /// <summary>
        /// 미리보기 아이템 목록
        /// </summary>
        public ObservableCollection<PipelinePreviewItem> PreviewItems
        {
            get => _previewItems;
            set
            {
                _previewItems = value;
                OnPropertyChanged(nameof(PreviewItems));
            }
        }

        /// <summary>
        /// 실행 가능 여부
        /// </summary>
        public bool CanExecute => _lastResult?.Schedules?.Count > 0 && !IsExecuting;

        #endregion

        #region Commands

        public ICommand BrowseCsvCommand { get; }
        public ICommand BrowseGeometryCsvCommand { get; }
        public ICommand PreviewCommand { get; }
        public ICommand ExecuteCommand { get; }
        public ICommand ExportCsvCommand { get; }

        #endregion

        #region Command Methods

        /// <summary>
        /// AllProperties CSV 파일 선택
        /// </summary>
        private void BrowseCsv()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                Title = "AllProperties CSV 파일 선택"
            };

            if (dialog.ShowDialog() == true)
            {
                CsvPath = dialog.FileName;
                StatusMessage = $"CSV 파일 선택됨: {Path.GetFileName(CsvPath)}";
            }
        }

        /// <summary>
        /// Geometry CSV 파일 선택
        /// </summary>
        private void BrowseGeometryCsv()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                Title = "Geometry CSV 파일 선택 (선택사항)"
            };

            if (dialog.ShowDialog() == true)
            {
                GeometryCsvPath = dialog.FileName;
            }
        }

        /// <summary>
        /// 미리보기 실행: CSV 파싱 → 그룹핑 → Time Mapping → Preview 표시
        /// </summary>
        private void ExecutePreview()
        {
            if (string.IsNullOrEmpty(CsvPath) || !File.Exists(CsvPath))
            {
                StatusMessage = "CSV 파일을 먼저 선택하세요.";
                return;
            }

            try
            {
                var options = BuildOptions();
                _lastResult = _builder.BuildFromCsv(options);

                if (!_lastResult.Success)
                {
                    StatusMessage = _lastResult.ErrorMessage;
                    return;
                }

                // Preview 표시
                PreviewItems.Clear();
                foreach (var schedule in _lastResult.Schedules)
                {
                    PreviewItems.Add(new PipelinePreviewItem
                    {
                        Pipeline = schedule.ParentSet,
                        PipeRun = schedule.TaskName,
                        ObjectCount = int.Parse(schedule.CustomProperties.ContainsKey("ObjectCount")
                            ? schedule.CustomProperties["ObjectCount"] : "0"),
                        Start = schedule.PlannedStartDate ?? DateTime.MinValue,
                        End = schedule.PlannedEndDate ?? DateTime.MinValue,
                        DurationDays = schedule.Duration
                    });
                }

                StatusMessage = $"미리보기 완료: {_lastResult.PipelineCount}개 Pipeline, " +
                                $"{_lastResult.PipeRunCount}개 PipeRun, " +
                                $"{_lastResult.TotalObjectCount}개 객체, " +
                                $"총 {_lastResult.TotalDurationDays}일";

                ((RelayCommand)ExecuteCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ExportCsvCommand).RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                StatusMessage = $"미리보기 오류: {ex.Message}";
            }
        }

        /// <summary>
        /// TimeLiner 실행: 4-Step Pipeline
        /// 1. BuildFromCsv → List&lt;ScheduleData&gt;
        /// 2. ObjectMatcher.FindBySyncId → ModelItem 매칭
        /// 3. SelectionSetService.CreateHierarchicalSets → Selection Set 생성
        /// 4. TimeLinerService.CreateTasks → TimeLiner Task 생성 + 연결
        /// </summary>
        private void ExecuteTimeLiner()
        {
            if (_lastResult == null || _lastResult.Schedules.Count == 0)
            {
                ExecutePreview();
                if (_lastResult == null || _lastResult.Schedules.Count == 0)
                {
                    MessageBox.Show("미리보기를 먼저 실행하세요.", "Pipeline 4D", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            // DryRun 모드
            if (IsDryRun)
            {
                ShowDryRunReport();
                return;
            }

            IsExecuting = true;
            Progress = 0;

            try
            {
                var scheduleDataList = _lastResult.Schedules;

                // AWP4DOptions 설정
                var awpOptions = new AWP4DOptions
                {
                    SelectionSetRootFolder = BuildOptions().SelectionSetRootFolder,
                    TimeLinerRootFolder = BuildOptions().TimeLinerRootFolder,
                    CreateHierarchicalTasks = true,
                    GroupingStrategy = GroupingStrategy.ByParentSet,
                    TaskSelectionMode = TaskSelectionMode.Explicit,
                    EnablePropertyWrite = false,
                    VerboseLogging = true
                };

                // Step 2: Object 매칭
                StatusMessage = "2/4: 객체 매칭 중...";
                Progress = 20;

                int matchedCount = 0;
                int failedCount = 0;

                // PipeRun 단위 매칭: ObjectIds에서 개별 GUID 추출하여 매칭
                foreach (var schedule in scheduleDataList)
                {
                    try
                    {
                        string objectIdsStr;
                        if (schedule.CustomProperties != null &&
                            schedule.CustomProperties.TryGetValue("ObjectIds", out objectIdsStr) &&
                            !string.IsNullOrEmpty(objectIdsStr))
                        {
                            var guids = objectIdsStr.Split(';');
                            bool anyMatched = false;

                            foreach (var guidStr in guids)
                            {
                                Guid objGuid;
                                if (Guid.TryParse(guidStr.Trim(), out objGuid))
                                {
                                    // 첫 번째 매칭된 객체의 GUID를 SyncID로 사용
                                    var modelItem = _objectMatcher.FindBySyncId(guidStr.Trim(), awpOptions);
                                    if (modelItem != null)
                                    {
                                        if (!anyMatched)
                                        {
                                            schedule.MatchedObjectId = modelItem.InstanceGuid;
                                            schedule.MatchStatus = MatchStatus.Matched;
                                            anyMatched = true;
                                        }
                                    }
                                }
                            }

                            if (anyMatched)
                                matchedCount++;
                            else
                            {
                                schedule.MatchStatus = MatchStatus.NotFound;
                                failedCount++;
                            }
                        }
                        else
                        {
                            // ObjectIds가 없으면 SyncID(PipeRun명)로 매칭 시도
                            var modelItem = _objectMatcher.FindBySyncId(schedule.SyncID, awpOptions);
                            if (modelItem != null)
                            {
                                schedule.MatchedObjectId = modelItem.InstanceGuid;
                                schedule.MatchStatus = MatchStatus.Matched;
                                matchedCount++;
                            }
                            else
                            {
                                schedule.MatchStatus = MatchStatus.NotFound;
                                failedCount++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        schedule.MatchStatus = MatchStatus.Error;
                        schedule.MatchError = ex.Message;
                        failedCount++;
                    }

                    var total = matchedCount + failedCount;
                    Progress = 20 + (int)(30.0 * total / scheduleDataList.Count);
                    StatusMessage = $"2/4: 객체 매칭 중... ({total}/{scheduleDataList.Count})";
                }

                if (matchedCount == 0)
                {
                    throw new InvalidOperationException(
                        $"매칭된 객체가 없습니다. (전체: {scheduleDataList.Count}, 실패: {failedCount})\n" +
                        "CSV의 ObjectId와 Navisworks 모델의 Element ID가 일치하는지 확인하세요.");
                }

                // Step 3: Selection Set 생성
                StatusMessage = "3/4: Selection Set 생성 중...";
                Progress = 50;

                var matchedData = scheduleDataList.Where(s => s.MatchStatus == MatchStatus.Matched).ToList();
                var setResults = _selectionSetService.CreateHierarchicalSets(matchedData, awpOptions);
                Progress = 70;

                // SyncID → SetName 매핑
                var syncIdToSetName = new Dictionary<string, string>();
                foreach (var schedule in matchedData)
                {
                    if (!string.IsNullOrEmpty(schedule.ParentSet))
                    {
                        syncIdToSetName[schedule.SyncID] = schedule.TaskName;
                    }
                }

                // Step 4: TimeLiner Task 생성
                StatusMessage = "4/4: TimeLiner Task 생성 중...";
                Progress = 80;

                var taskResults = _timeLinerService.CreateTasks(matchedData, syncIdToSetName, awpOptions);
                Progress = 100;

                // 결과 보고
                var report = new StringBuilder();
                report.AppendLine("=== Pipeline 4D TimeLiner 실행 완료 ===\n");
                report.AppendLine($"Pipeline: {_lastResult.PipelineCount}개");
                report.AppendLine($"PipeRun: {_lastResult.PipeRunCount}개");
                report.AppendLine($"총 객체: {_lastResult.TotalObjectCount}개");
                report.AppendLine($"\n객체 매칭: {matchedCount}개 성공, {failedCount}개 실패");
                report.AppendLine($"Selection Set: {setResults?.SetCount ?? 0}개 생성");
                report.AppendLine($"TimeLiner Task: {taskResults?.TaskCount ?? 0}개 생성");
                report.AppendLine($"  - 연결됨: {taskResults?.LinkedCount ?? 0}개");
                report.AppendLine($"  - 미연결: {taskResults?.UnlinkedCount ?? 0}개");
                report.AppendLine($"\n기간: {_lastResult.ScheduleStart:yyyy-MM-dd} ~ {_lastResult.ScheduleEnd:yyyy-MM-dd} ({_lastResult.TotalDurationDays}일)");

                StatusMessage = $"완료: {taskResults?.TaskCount ?? 0}개 Task 생성됨";
                MessageBox.Show(report.ToString(), "Pipeline 4D 완료", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = $"실행 오류: {ex.Message}";
                Progress = 0;
                MessageBox.Show($"Pipeline 4D 실행 중 오류:\n\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsExecuting = false;
            }
        }

        /// <summary>
        /// TimeLiner CSV 내보내기
        /// </summary>
        private void ExportCsv()
        {
            if (_lastResult == null || _lastResult.Schedules.Count == 0)
            {
                MessageBox.Show("미리보기를 먼저 실행하세요.", "Pipeline 4D", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv",
                DefaultExt = "csv",
                FileName = $"pipeline_schedule_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                Title = "TimeLiner CSV 내보내기"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    _builder.ExportTimeLinerCsv(_lastResult.Schedules, dialog.FileName);
                    StatusMessage = $"CSV 저장 완료: {dialog.FileName}";
                    MessageBox.Show($"TimeLiner CSV가 저장되었습니다.\n\n{dialog.FileName}\n\n" +
                                    "Navisworks TimeLiner에서 Import하여 사용할 수 있습니다.",
                        "CSV 내보내기", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    StatusMessage = $"CSV 저장 오류: {ex.Message}";
                    MessageBox.Show($"CSV 저장 오류:\n\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// DryRun 결과 보고서 표시
        /// </summary>
        private void ShowDryRunReport()
        {
            var report = new StringBuilder();
            report.AppendLine("=== DryRun 결과 (실제 실행 없음) ===\n");
            report.AppendLine($"Pipeline: {_lastResult.PipelineCount}개");
            report.AppendLine($"PipeRun (Task): {_lastResult.PipeRunCount}개");
            report.AppendLine($"총 객체: {_lastResult.TotalObjectCount}개");
            report.AppendLine($"기간: {_lastResult.ScheduleStart:yyyy-MM-dd} ~ {_lastResult.ScheduleEnd:yyyy-MM-dd}");
            report.AppendLine($"총 {_lastResult.TotalDurationDays}일\n");

            report.AppendLine("--- Pipeline별 PipeRun 수 ---");
            var grouped = _lastResult.Schedules.GroupBy(s => s.ParentSet);
            foreach (var group in grouped.Take(20))
            {
                report.AppendLine($"  {group.Key}: {group.Count()}개 PipeRun");
            }
            if (grouped.Count() > 20)
                report.AppendLine($"  ... 외 {grouped.Count() - 20}개 Pipeline");

            report.AppendLine("\n--- 상위 5개 Task 미리보기 ---");
            foreach (var task in _lastResult.Schedules.Take(5))
            {
                report.AppendLine($"  [{task.ParentSet}] {task.TaskName}");
                report.AppendLine($"    기간: {task.PlannedStartDate:yyyy-MM-dd} ~ {task.PlannedEndDate:yyyy-MM-dd}");
                var objCount = task.CustomProperties.ContainsKey("ObjectCount")
                    ? task.CustomProperties["ObjectCount"] : "?";
                report.AppendLine($"    객체 수: {objCount}개");
            }

            StatusMessage = "DryRun 완료";
            MessageBox.Show(report.ToString(), "DryRun 결과", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// UI 설정에서 PipelineScheduleOptions 빌드
        /// </summary>
        private PipelineScheduleOptions BuildOptions()
        {
            TimeStrategy timeStrategy;
            switch (SelectedTimeStrategy)
            {
                case "FixedDuration": timeStrategy = TimeStrategy.FixedDuration; break;
                case "ObjectCountBased": timeStrategy = TimeStrategy.ObjectCountBased; break;
                default: timeStrategy = TimeStrategy.Hybrid; break;
            }

            OrderingStrategy orderingStrategy;
            switch (SelectedOrderingStrategy)
            {
                case "공간정렬(좌→우)": orderingStrategy = OrderingStrategy.SpatialLeftToRight; break;
                case "객체수 기준": orderingStrategy = OrderingStrategy.ByObjectCount; break;
                default: orderingStrategy = OrderingStrategy.Alphabetical; break;
            }

            return new PipelineScheduleOptions
            {
                AllPropertiesCsvPath = CsvPath,
                GeometryCsvPath = GeometryCsvPath,
                ProjectStartDate = StartDate,
                BaseDurationHours = BaseDuration,
                HoursPerObject = PerObjectHours,
                WorkHoursPerDay = 8.0,
                GapDaysBetweenPipelines = GapDays,
                TimeStrategy = timeStrategy,
                OrderingStrategy = orderingStrategy,
                TaskType = "Construct",
                TimeLinerRootFolder = "Pipeline Schedule",
                SelectionSetRootFolder = "Pipeline Sets"
            };
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    /// <summary>
    /// Pipeline 미리보기 아이템
    /// </summary>
    public class PipelinePreviewItem
    {
        public string Pipeline { get; set; }
        public string PipeRun { get; set; }
        public int ObjectCount { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public int DurationDays { get; set; }
    }
}
