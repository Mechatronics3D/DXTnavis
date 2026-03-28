using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.DocumentParts;
using DXTnavis.Models;
using WinForms = System.Windows.Forms;

namespace DXTnavis.Services
{
    /// <summary>
    /// Selection Set 자동 생성 서비스
    /// Phase 8: AWP 4D Automation - Selection Set Creation
    /// ADR-002 기반 구현
    /// </summary>
    public class SelectionSetService
    {
        private readonly NavisworksDataExtractor _extractor;

        /// <summary>
        /// 생성된 Selection Set 목록 (SetName → SelectionSet)
        /// </summary>
        private readonly Dictionary<string, SelectionSet> _createdSets;

        /// <summary>
        /// 생성된 폴더 목록 (FolderPath → FolderItem)
        /// </summary>
        private readonly Dictionary<string, FolderItem> _createdFolders;

        /// <summary>
        /// 진행 이벤트
        /// </summary>
        public event EventHandler<SelectionSetProgressEventArgs> ProgressChanged;

        /// <summary>
        /// 상세 로깅
        /// </summary>
        public bool VerboseLogging { get; set; } = true;

        public SelectionSetService()
        {
            _extractor = new NavisworksDataExtractor();
            _createdSets = new Dictionary<string, SelectionSet>(StringComparer.OrdinalIgnoreCase);
            _createdFolders = new Dictionary<string, FolderItem>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 캐시 초기화
        /// </summary>
        public void ClearCache()
        {
            _createdSets.Clear();
            _createdFolders.Clear();
        }

        /// <summary>
        /// 스케줄 데이터 기반 계층적 Selection Set 생성
        /// </summary>
        /// <param name="schedules">매칭된 스케줄 데이터 목록</param>
        /// <param name="options">옵션</param>
        /// <returns>생성 결과</returns>
        public SelectionSetResult CreateHierarchicalSets(List<ScheduleData> schedules, AWP4DOptions options)
        {
            var result = new SelectionSetResult();
            var doc = Application.ActiveDocument;

            if (doc == null)
                throw new InvalidOperationException("활성화된 Navisworks 문서가 없습니다.");

            // 매칭된 스케줄만 필터링
            var matchedSchedules = schedules.Where(s =>
                s.MatchStatus == MatchStatus.Matched && s.MatchedObjectId.HasValue).ToList();

            if (matchedSchedules.Count == 0)
            {
                result.FailedSets.Add("매칭된 스케줄 데이터가 없습니다.");
                return result;
            }

            // 루트 폴더 생성/찾기
            var rootFolder = GetOrCreateRootFolder(doc, options.SelectionSetRootFolder);
            result.FolderCount++;

            // 그룹화 전략에 따라 스케줄 그룹화
            var groups = GroupSchedules(matchedSchedules, options.GroupingStrategy);

            int index = 0;
            foreach (var group in groups)
            {
                index++;

                try
                {
                    // 폴더 경로 생성
                    var targetFolder = CreateFolderPath(doc, rootFolder, group.Key, options);

                    // ModelItem 수집
                    var modelItems = new ModelItemCollection();
                    foreach (var schedule in group.Value)
                    {
                        var item = _extractor.FindModelItemById(schedule.MatchedObjectId.Value);
                        if (item != null)
                        {
                            modelItems.Add(item);
                            result.TotalItemCount++;
                        }
                    }

                    // 빈 세트 건너뛰기
                    if (modelItems.Count == 0 && options.SkipEmptySelectionSets)
                    {
                        if (VerboseLogging)
                            System.Diagnostics.Debug.WriteLine($"[SelectionSetService] '{group.Key}' 건너뛰기 (비어있음)");
                        continue;
                    }

                    // Selection Set 생성
                    string setName = GenerateSetName(group.Key, group.Value);
                    var selectionSet = CreateSelectionSet(doc, targetFolder, setName, modelItems);

                    if (selectionSet != null)
                    {
                        _createdSets[setName] = selectionSet;
                        result.SetCount++;
                        result.CreatedSets.Add(setName);

                        if (VerboseLogging)
                            System.Diagnostics.Debug.WriteLine(
                                $"[SelectionSetService] 생성: '{setName}' ({modelItems.Count}개 객체)");
                    }

                    OnProgressChanged(new SelectionSetProgressEventArgs
                    {
                        CurrentIndex = index,
                        TotalCount = groups.Count,
                        SetName = setName,
                        ItemCount = modelItems.Count,
                        Success = true
                    });
                }
                catch (Exception ex)
                {
                    result.FailedSets.Add($"{group.Key}: {ex.Message}");

                    OnProgressChanged(new SelectionSetProgressEventArgs
                    {
                        CurrentIndex = index,
                        TotalCount = groups.Count,
                        SetName = group.Key,
                        Success = false,
                        ErrorMessage = ex.Message
                    });
                }

                // Pump Windows messages to prevent COM ContextSwitchDeadlock
                if (index % 10 == 0)
                {
                    WinForms.Application.DoEvents();
                }
            }

            result.FolderCount = _createdFolders.Count + 1; // 루트 폴더 포함

            if (VerboseLogging)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[SelectionSetService] 완료: {result.SetCount}개 세트, {result.FolderCount}개 폴더");
            }

            return result;
        }

        /// <summary>
        /// Pipeline 4D 전용: PipeRun별 개별 Selection Set 생성
        /// 각 ScheduleData = 1개 PipeRun = 1개 SelectionSet (다중 객체 포함)
        ///
        /// 구조: {RootFolder}/{ParentSet(Pipeline)}/{TaskName(PipeRun)} ← SelectionSet
        /// CSV leaf task name과 Selection Set name이 일치하여 TimeLiner 규칙 자동 매칭 가능
        ///
        /// 모델 트리를 1회 순회하여 필요한 모든 ModelItem을 fresh하게 수집
        /// (ObjectMatcher 캐시 사용 시 WeakRef GC → ObjectDisposedException 방지)
        /// </summary>
        public SelectionSetResult CreatePipelineSets(
            List<ScheduleData> schedules,
            ObjectMatcher objectMatcher,
            AWP4DOptions options)
        {
            var result = new SelectionSetResult();
            var doc = Application.ActiveDocument;

            if (doc == null)
            {
                result.FailedSets.Add("활성 문서가 없습니다.");
                return result;
            }

            // ── Phase A: 필요한 GUID 파싱 + scheduleIndex 매핑 ──
            // GUID → 해당 schedule의 인덱스 목록 (하나의 GUID가 여러 schedule에 속할 수 있음)
            var guidToScheduleIndices = new Dictionary<Guid, List<int>>();
            for (int i = 0; i < schedules.Count; i++)
            {
                string objectIdsStr;
                if (schedules[i].CustomProperties != null &&
                    schedules[i].CustomProperties.TryGetValue("ObjectIds", out objectIdsStr) &&
                    !string.IsNullOrEmpty(objectIdsStr))
                {
                    foreach (var guidStr in objectIdsStr.Split(';'))
                    {
                        Guid g;
                        if (Guid.TryParse(guidStr.Trim(), out g))
                        {
                            List<int> indices;
                            if (!guidToScheduleIndices.TryGetValue(g, out indices))
                            {
                                indices = new List<int>();
                                guidToScheduleIndices[g] = indices;
                            }
                            indices.Add(i);
                        }
                    }
                }
            }

            if (VerboseLogging)
                System.Diagnostics.Debug.WriteLine(
                    $"[SelectionSetService] Pipeline Sets: {schedules.Count}개 PipeRun, {guidToScheduleIndices.Count}개 GUID 수집");

            // ── Phase B: 모델 트리 1회 순회 → fresh ModelItem 직접 수집 ──
            var itemCollections = new ModelItemCollection[schedules.Count];
            for (int i = 0; i < schedules.Count; i++)
                itemCollections[i] = new ModelItemCollection();

            int traversedCount = 0;
            int collectedCount = 0;
            foreach (var model in doc.Models)
            {
                CollectItemsForPipelineSets(
                    model.RootItem, guidToScheduleIndices, itemCollections,
                    ref traversedCount, ref collectedCount);
            }

            WinForms.Application.DoEvents();

            if (VerboseLogging)
                System.Diagnostics.Debug.WriteLine(
                    $"[SelectionSetService] 트리 순회 완료: {traversedCount:N0}개 노드, {collectedCount}개 ModelItem 수집");

            // ── Phase C: Selection Set 즉시 생성 (ModelItem이 fresh한 상태) ──
            var rootFolder = GetOrCreateRootFolder(doc, options.SelectionSetRootFolder);
            result.FolderCount++;

            for (int i = 0; i < schedules.Count; i++)
            {
                var schedule = schedules[i];
                var modelItems = itemCollections[i];

                try
                {
                    // 빈 세트 건너뛰기
                    if (modelItems.Count == 0 && options.SkipEmptySelectionSets)
                    {
                        if (VerboseLogging)
                            System.Diagnostics.Debug.WriteLine(
                                $"[SelectionSetService] Pipeline Set 건너뛰기: '{schedule.TaskName}' (빈 세트)");
                        continue;
                    }

                    // 폴더 경로 = ParentSet (Pipeline name)
                    var targetFolder = CreateFolderPath(doc, rootFolder, schedule.ParentSet, options);

                    // Set 이름 = TaskName (PipeRun name) → CSV leaf task name과 일치
                    string setName = schedule.TaskName;

                    var selectionSet = CreateSelectionSet(doc, targetFolder, setName, modelItems);

                    if (selectionSet != null)
                    {
                        _createdSets[setName] = selectionSet;
                        result.SetCount++;
                        result.TotalItemCount += modelItems.Count;
                        result.CreatedSets.Add(setName);

                        if (VerboseLogging)
                            System.Diagnostics.Debug.WriteLine(
                                $"[SelectionSetService] Pipeline Set: '{schedule.ParentSet}/{setName}' ({modelItems.Count}개 객체)");
                    }

                    OnProgressChanged(new SelectionSetProgressEventArgs
                    {
                        CurrentIndex = i + 1,
                        TotalCount = schedules.Count,
                        SetName = setName,
                        ItemCount = modelItems.Count,
                        Success = true
                    });
                }
                catch (Exception ex)
                {
                    result.FailedSets.Add($"{schedule.TaskName}: {ex.Message}");
                    if (VerboseLogging)
                        System.Diagnostics.Debug.WriteLine(
                            $"[SelectionSetService] Pipeline Set 오류: '{schedule.TaskName}' - {ex.Message}");
                }

                // DoEvents every 5 sets
                if ((i + 1) % 5 == 0)
                    WinForms.Application.DoEvents();
            }

            result.FolderCount = _createdFolders.Count + 1;

            if (VerboseLogging)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[SelectionSetService] Pipeline Sets 완료: {result.SetCount}개 세트, " +
                    $"{result.TotalItemCount}개 객체, {result.FolderCount}개 폴더");
            }

            return result;
        }

        /// <summary>
        /// 모델 트리 재귀 순회: 필요한 GUID의 ModelItem을 해당 schedule의 컬렉션에 직접 추가
        /// 캐시 없이 fresh ModelItem 참조를 사용하여 WeakRef GC 문제 방지
        /// </summary>
        private void CollectItemsForPipelineSets(
            ModelItem item,
            Dictionary<Guid, List<int>> guidToScheduleIndices,
            ModelItemCollection[] itemCollections,
            ref int traversedCount,
            ref int collectedCount)
        {
            if (item == null) return;

            // 이 ModelItem의 GUID가 필요한지 확인
            if (item.InstanceGuid != Guid.Empty)
            {
                List<int> scheduleIndices;
                if (guidToScheduleIndices.TryGetValue(item.InstanceGuid, out scheduleIndices))
                {
                    foreach (int idx in scheduleIndices)
                    {
                        itemCollections[idx].Add(item);
                    }
                    collectedCount++;
                }
            }

            traversedCount++;

            // DoEvents: 10,000 노드마다
            if (traversedCount % 10000 == 0)
                WinForms.Application.DoEvents();

            // 자식 순회
            foreach (ModelItem child in item.Children)
            {
                CollectItemsForPipelineSets(child, guidToScheduleIndices, itemCollections,
                    ref traversedCount, ref collectedCount);
            }
        }

        /// <summary>
        /// 루트 폴더 생성/찾기
        /// </summary>
        private FolderItem GetOrCreateRootFolder(Document doc, string folderName)
        {
            var docSets = doc.SelectionSets;

            // 기존 폴더 찾기
            foreach (SavedItem item in docSets.Value)
            {
                if (item is FolderItem folder && folder.DisplayName == folderName)
                {
                    return folder;
                }
            }

            // 새 폴더 생성 (AddCopy 패턴)
            var newFolder = new FolderItem();
            newFolder.DisplayName = folderName;
            docSets.AddCopy(newFolder);

            // 추가된 폴더 찾아 반환
            foreach (SavedItem item in docSets.Value)
            {
                if (item is FolderItem folder && folder.DisplayName == folderName)
                {
                    return folder;
                }
            }

            throw new InvalidOperationException($"폴더 '{folderName}' 생성 실패");
        }

        /// <summary>
        /// 폴더 경로 생성 (예: "Zone-A/Level-1")
        /// </summary>
        private FolderItem CreateFolderPath(Document doc, FolderItem rootFolder, string path, AWP4DOptions options)
        {
            if (string.IsNullOrEmpty(path))
                return rootFolder;

            var parts = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            FolderItem currentFolder = rootFolder;

            foreach (var part in parts)
            {
                string folderPath = currentFolder == rootFolder
                    ? part
                    : $"{GetFolderPath(currentFolder)}/{part}";

                // 캐시 확인
                if (_createdFolders.TryGetValue(folderPath, out FolderItem cached))
                {
                    currentFolder = cached;
                    continue;
                }

                // 기존 하위 폴더 찾기
                FolderItem childFolder = null;
                foreach (SavedItem child in currentFolder.Children)
                {
                    if (child is FolderItem folder && folder.DisplayName == part)
                    {
                        childFolder = folder;
                        break;
                    }
                }

                if (childFolder == null)
                {
                    // 새 폴더 생성
                    childFolder = new FolderItem();
                    childFolder.DisplayName = part;

                    // InsertCopy로 추가
                    doc.SelectionSets.InsertCopy(currentFolder, currentFolder.Children.Count, childFolder);

                    // 추가된 폴더 찾기
                    foreach (SavedItem child in currentFolder.Children)
                    {
                        if (child is FolderItem folder && folder.DisplayName == part)
                        {
                            childFolder = folder;
                            break;
                        }
                    }
                }

                _createdFolders[folderPath] = childFolder;
                currentFolder = childFolder;
            }

            return currentFolder;
        }

        /// <summary>
        /// 폴더 경로 문자열 생성
        /// </summary>
        private string GetFolderPath(FolderItem folder)
        {
            // 간단히 DisplayName 반환 (실제로는 부모 추적 필요)
            return folder.DisplayName;
        }

        /// <summary>
        /// 스케줄 그룹화
        /// </summary>
        private Dictionary<string, List<ScheduleData>> GroupSchedules(List<ScheduleData> schedules, GroupingStrategy strategy)
        {
            switch (strategy)
            {
                case GroupingStrategy.ByParentSet:
                    return schedules
                        .GroupBy(s => s.ParentSet ?? "Ungrouped")
                        .ToDictionary(g => g.Key, g => g.ToList());

                case GroupingStrategy.ByZone:
                    return schedules
                        .GroupBy(s => ExtractZone(s.ParentSet) ?? "Ungrouped")
                        .ToDictionary(g => g.Key, g => g.ToList());

                case GroupingStrategy.ByZoneAndLevel:
                    return schedules
                        .GroupBy(s => ExtractZoneAndLevel(s.ParentSet) ?? "Ungrouped")
                        .ToDictionary(g => g.Key, g => g.ToList());

                case GroupingStrategy.ByTaskName:
                    return schedules
                        .GroupBy(s => s.TaskName ?? "Unnamed")
                        .ToDictionary(g => g.Key, g => g.ToList());

                case GroupingStrategy.ByStartWeek:
                    return schedules
                        .Where(s => s.PlannedStartDate.HasValue)
                        .GroupBy(s => GetWeekKey(s.PlannedStartDate.Value))
                        .ToDictionary(g => g.Key, g => g.ToList());

                case GroupingStrategy.ByTaskType:
                    return schedules
                        .GroupBy(s => s.TaskType ?? "Construct")
                        .ToDictionary(g => g.Key, g => g.ToList());

                case GroupingStrategy.None:
                default:
                    // 개별 세트
                    return schedules.ToDictionary(
                        s => s.SyncID,
                        s => new List<ScheduleData> { s });
            }
        }

        /// <summary>
        /// Zone 추출 (예: "Zone-A/Level-1" → "Zone-A")
        /// </summary>
        private string ExtractZone(string parentSet)
        {
            if (string.IsNullOrEmpty(parentSet))
                return null;

            var parts = parentSet.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0 ? parts[0] : null;
        }

        /// <summary>
        /// Zone+Level 추출 (예: "Zone-A/Level-1/Structural" → "Zone-A/Level-1")
        /// </summary>
        private string ExtractZoneAndLevel(string parentSet)
        {
            if (string.IsNullOrEmpty(parentSet))
                return null;

            var parts = parentSet.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return $"{parts[0]}/{parts[1]}";
            return parts.Length > 0 ? parts[0] : null;
        }

        /// <summary>
        /// 주 단위 키 생성
        /// </summary>
        private string GetWeekKey(DateTime date)
        {
            var cal = System.Globalization.CultureInfo.CurrentCulture.Calendar;
            int week = cal.GetWeekOfYear(date, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
            return $"{date.Year}-W{week:D2}";
        }

        /// <summary>
        /// Selection Set 이름 생성
        /// </summary>
        private string GenerateSetName(string groupKey, List<ScheduleData> schedules)
        {
            // 마지막 경로 부분을 이름으로 사용
            var parts = groupKey.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            string baseName = parts.Length > 0 ? parts[parts.Length - 1] : groupKey;

            // 중복 방지
            if (_createdSets.ContainsKey(baseName))
            {
                baseName = $"{baseName}_{schedules.Count}items";
            }

            return baseName;
        }

        /// <summary>
        /// Selection Set 생성
        /// </summary>
        private SelectionSet CreateSelectionSet(Document doc, FolderItem targetFolder, string setName, ModelItemCollection items)
        {
            // SelectionSet 생성
            var selectionSet = new SelectionSet(items);
            selectionSet.DisplayName = setName;

            // 폴더에 추가 (InsertCopy 패턴)
            doc.SelectionSets.InsertCopy(targetFolder, targetFolder.Children.Count, selectionSet);

            // 추가된 SelectionSet 찾아 반환
            foreach (SavedItem child in targetFolder.Children)
            {
                if (child is SelectionSet set && set.DisplayName == setName)
                {
                    return set;
                }
            }

            return null;
        }

        /// <summary>
        /// 이름으로 Selection Set 찾기
        /// </summary>
        public SelectionSet FindSetByName(string setName)
        {
            if (_createdSets.TryGetValue(setName, out SelectionSet cached))
            {
                return cached;
            }

            var doc = Application.ActiveDocument;
            if (doc == null)
                return null;

            return FindSetInCollection(doc.SelectionSets.Value, setName);
        }

        /// <summary>
        /// 재귀적으로 Selection Set 찾기
        /// </summary>
        private SelectionSet FindSetInCollection(SavedItemCollection items, string name)
        {
            foreach (SavedItem item in items)
            {
                if (item is SelectionSet set && set.DisplayName == name)
                    return set;

                if (item is FolderItem folder)
                {
                    var found = FindSetInCollection(folder.Children, name);
                    if (found != null)
                        return found;
                }
            }
            return null;
        }

        /// <summary>
        /// 생성된 모든 Selection Set 반환
        /// </summary>
        public IReadOnlyDictionary<string, SelectionSet> GetCreatedSets()
        {
            return _createdSets;
        }

        /// <summary>
        /// 특정 폴더의 모든 Selection Set 삭제
        /// </summary>
        public int ClearSelectionSets(string rootFolderName)
        {
            var doc = Application.ActiveDocument;
            if (doc == null)
                return 0;

            var docSets = doc.SelectionSets;
            int deletedCount = 0;

            // 루트 폴더 찾기
            FolderItem rootFolder = null;
            int rootIndex = -1;

            for (int i = 0; i < docSets.Value.Count; i++)
            {
                if (docSets.Value[i] is FolderItem folder && folder.DisplayName == rootFolderName)
                {
                    rootFolder = folder;
                    rootIndex = i;
                    break;
                }
            }

            if (rootFolder != null && rootIndex >= 0)
            {
                // 폴더 내 항목 수 카운트
                deletedCount = CountItemsInFolder(rootFolder);

                // 폴더 삭제
                docSets.Value.RemoveAt(rootIndex);

                _createdSets.Clear();
                _createdFolders.Clear();
            }

            return deletedCount;
        }

        /// <summary>
        /// 폴더 내 항목 수 카운트
        /// </summary>
        private int CountItemsInFolder(FolderItem folder)
        {
            int count = 0;
            foreach (SavedItem child in folder.Children)
            {
                if (child is FolderItem subFolder)
                    count += CountItemsInFolder(subFolder);
                else
                    count++;
            }
            return count;
        }

        protected virtual void OnProgressChanged(SelectionSetProgressEventArgs e)
        {
            ProgressChanged?.Invoke(this, e);
        }
    }

    /// <summary>
    /// Selection Set 생성 진행 이벤트 인자
    /// </summary>
    public class SelectionSetProgressEventArgs : EventArgs
    {
        public int CurrentIndex { get; set; }
        public int TotalCount { get; set; }
        public string SetName { get; set; }
        public int ItemCount { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public double Progress => TotalCount > 0 ? (double)CurrentIndex / TotalCount * 100 : 0;
    }
}
