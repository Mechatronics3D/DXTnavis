using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Navisworks.Api;
using DXTnavis.Models;
using WinForms = System.Windows.Forms;

namespace DXTnavis.Services
{
    /// <summary>
    /// SyncID를 기반으로 Navisworks ModelItem을 매칭하는 서비스
    /// Phase 8: AWP 4D Automation - Object Matching
    /// </summary>
    public class ObjectMatcher
    {
        private readonly NavisworksDataExtractor _extractor;

        /// <summary>
        /// 캐시: SyncID → ModelItem 매핑
        /// </summary>
        private readonly ConcurrentDictionary<string, ModelItem> _matchCache;

        /// <summary>
        /// 캐시: 속성값 → ModelItem 목록 매핑
        /// </summary>
        private readonly ConcurrentDictionary<string, List<ModelItem>> _propertyValueCache;

        /// <summary>
        /// 매칭 진행 이벤트
        /// </summary>
        public event EventHandler<ObjectMatchProgressEventArgs> ProgressChanged;

        /// <summary>
        /// 상세 로깅 활성화
        /// </summary>
        public bool VerboseLogging { get; set; } = true;

        /// <summary>
        /// 캐시 사용 여부
        /// </summary>
        public bool UseCache { get; set; } = true;

        public ObjectMatcher()
        {
            _extractor = new NavisworksDataExtractor();
            _matchCache = new ConcurrentDictionary<string, ModelItem>(StringComparer.OrdinalIgnoreCase);
            _propertyValueCache = new ConcurrentDictionary<string, List<ModelItem>>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 캐시 초기화
        /// </summary>
        public void ClearCache()
        {
            _matchCache.Clear();
            _propertyValueCache.Clear();
        }

        /// <summary>
        /// 단일 SyncID로 ModelItem 찾기
        /// </summary>
        /// <param name="syncId">검색할 SyncID</param>
        /// <param name="options">매칭 옵션</param>
        /// <returns>매칭된 ModelItem (없으면 null)</returns>
        public ModelItem FindBySyncId(string syncId, AWP4DOptions options)
        {
            if (string.IsNullOrEmpty(syncId))
                return null;

            // 캐시 확인
            if (UseCache && _matchCache.TryGetValue(syncId, out ModelItem cached))
            {
                return cached;
            }

            var doc = Application.ActiveDocument;
            if (doc == null)
                return null;

            // 속성 기반 검색
            var search = new Search();
            search.Selection.SelectAll();

            // 검색 조건 설정 - 정확한 매칭 사용
            // Note: Navisworks Search API는 부분 매칭을 직접 지원하지 않음
            // 부분 매칭이 필요한 경우 결과에서 수동으로 필터링
            SearchCondition condition = SearchCondition.HasPropertyByDisplayName(
                options.MatchPropertyCategory,
                options.MatchPropertyName)
                .EqualValue(VariantData.FromDisplayString(syncId));

            search.SearchConditions.Add(condition);

            // 검색 실행
            ModelItemCollection results = search.FindAll(doc, false);

            if (results.Count == 0)
            {
                // 대체 검색: Element.Id 외에 다른 일반적인 ID 속성들 시도
                var alternativeResults = TryAlternativeMatching(syncId, options);
                if (alternativeResults != null && alternativeResults.Count > 0)
                {
                    results = alternativeResults;
                }
            }

            if (results.Count == 0)
            {
                if (VerboseLogging)
                    System.Diagnostics.Debug.WriteLine($"[ObjectMatcher] '{syncId}' 매칭 실패: 결과 없음");
                return null;
            }

            if (results.Count > 1 && !options.AllowMultipleMatches)
            {
                if (VerboseLogging)
                    System.Diagnostics.Debug.WriteLine($"[ObjectMatcher] '{syncId}' 다중 매칭: {results.Count}개 발견");
            }

            // 첫 번째 결과 반환
            var modelItem = results.First;

            // 캐시에 저장
            if (UseCache)
            {
                _matchCache.TryAdd(syncId, modelItem);
            }

            return modelItem;
        }

        /// <summary>
        /// 대체 매칭 시도 (다양한 ID 속성 검색)
        /// </summary>
        private ModelItemCollection TryAlternativeMatching(string syncId, AWP4DOptions options)
        {
            var doc = Application.ActiveDocument;
            if (doc == null)
                return null;

            // 시도할 속성 목록
            var alternativeProperties = new List<(string Category, string Property)>
            {
                ("Element", "UniqueId"),
                ("Element", "Id"),
                ("Item", "GUID"),
                ("Item", "Name"),
                ("Element ID", "Value"),
                ("LcRevitData", "UniqueId"),
                ("LcOaNode", "GUID"),
            };

            foreach (var (category, property) in alternativeProperties)
            {
                // 이미 시도한 조합은 건너뛰기
                if (category == options.MatchPropertyCategory && property == options.MatchPropertyName)
                    continue;

                try
                {
                    var search = new Search();
                    search.Selection.SelectAll();

                    var condition = SearchCondition.HasPropertyByDisplayName(category, property)
                        .EqualValue(VariantData.FromDisplayString(syncId));

                    search.SearchConditions.Add(condition);
                    var results = search.FindAll(doc, false);

                    if (results.Count > 0)
                    {
                        if (VerboseLogging)
                            System.Diagnostics.Debug.WriteLine(
                                $"[ObjectMatcher] '{syncId}' 대체 매칭 성공: {category}.{property}");
                        return results;
                    }
                }
                catch
                {
                    // 해당 속성이 없는 경우 무시
                    continue;
                }
            }

            return null;
        }

        /// <summary>
        /// 다수의 스케줄 데이터에 대해 일괄 매칭 수행
        /// </summary>
        /// <param name="schedules">스케줄 데이터 목록</param>
        /// <param name="options">매칭 옵션</param>
        /// <returns>매칭 결과</returns>
        public ObjectMatchResult MatchAll(List<ScheduleData> schedules, AWP4DOptions options)
        {
            var result = new ObjectMatchResult
            {
                TotalCount = schedules.Count
            };

            int index = 0;
            foreach (var schedule in schedules)
            {
                index++;

                if (string.IsNullOrEmpty(schedule.SyncID))
                {
                    schedule.MatchStatus = MatchStatus.Error;
                    schedule.MatchError = "SyncID가 비어있습니다.";
                    result.ErrorCount++;
                    continue;
                }

                try
                {
                    var modelItem = FindBySyncId(schedule.SyncID, options);

                    if (modelItem != null)
                    {
                        schedule.MatchedObjectId = modelItem.InstanceGuid;
                        schedule.MatchStatus = MatchStatus.Matched;
                        result.MatchedCount++;

                        if (VerboseLogging)
                            System.Diagnostics.Debug.WriteLine(
                                $"[ObjectMatcher] 매칭 성공: {schedule.SyncID} → {modelItem.DisplayName}");
                    }
                    else
                    {
                        schedule.MatchStatus = MatchStatus.NotFound;
                        schedule.MatchError = "해당 SyncID를 가진 객체를 찾을 수 없습니다.";
                        result.NotFoundCount++;
                        result.NotFoundSyncIds.Add(schedule.SyncID);
                    }

                    // 진행 이벤트
                    OnProgressChanged(new ObjectMatchProgressEventArgs
                    {
                        CurrentIndex = index,
                        TotalCount = result.TotalCount,
                        CurrentSyncId = schedule.SyncID,
                        Status = schedule.MatchStatus,
                        Progress = (double)index / result.TotalCount * 100
                    });
                }
                catch (Exception ex)
                {
                    schedule.MatchStatus = MatchStatus.Error;
                    schedule.MatchError = ex.Message;
                    result.ErrorCount++;

                    System.Diagnostics.Debug.WriteLine(
                        $"[ObjectMatcher] 매칭 오류: {schedule.SyncID} - {ex.Message}");
                }
            }

            if (VerboseLogging)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[ObjectMatcher] 매칭 완료: 성공 {result.MatchedCount}/{result.TotalCount} ({result.MatchRate:F1}%)");
            }

            return result;
        }

        /// <summary>
        /// 속성 캐시 사전 구축 (대량 매칭 시 성능 향상)
        /// </summary>
        /// <param name="options">매칭 옵션</param>
        public void BuildPropertyCache(AWP4DOptions options)
        {
            var doc = Application.ActiveDocument;
            if (doc == null)
                return;

            _propertyValueCache.Clear();

            int itemCount = 0;

            foreach (var model in doc.Models)
            {
                BuildCacheRecursive(model.RootItem, options, ref itemCount);
            }

            if (VerboseLogging)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[ObjectMatcher] 캐시 구축 완료: {itemCount}개 항목, {_propertyValueCache.Count}개 고유값");
            }
        }

        /// <summary>
        /// 재귀적으로 캐시 구축
        /// </summary>
        private void BuildCacheRecursive(ModelItem item, AWP4DOptions options, ref int count)
        {
            if (item == null)
                return;

            count++;

            // DoEvents every 5000 items to prevent ContextSwitchDeadlock
            if (count % 5000 == 0)
            {
                WinForms.Application.DoEvents();
            }

            // 속성값 추출
            try
            {
                var propertyCategories = item.PropertyCategories;
                foreach (PropertyCategory category in propertyCategories)
                {
                    if (category.DisplayName != options.MatchPropertyCategory)
                        continue;

                    foreach (DataProperty property in category.Properties)
                    {
                        if (property.DisplayName != options.MatchPropertyName)
                            continue;

                        string value = property.Value?.ToDisplayString();
                        if (!string.IsNullOrEmpty(value))
                        {
                            string key = options.IgnoreCaseInMatching ? value.ToLowerInvariant() : value;

                            _propertyValueCache.AddOrUpdate(
                                key,
                                new List<ModelItem> { item },
                                (k, list) =>
                                {
                                    list.Add(item);
                                    return list;
                                });
                        }
                        break;
                    }
                    break;
                }
            }
            catch
            {
                // 속성 접근 실패 시 무시
            }

            // 자식 항목 처리
            foreach (ModelItem child in item.Children)
            {
                BuildCacheRecursive(child, options, ref count);
            }
        }

        /// <summary>
        /// 캐시를 사용한 빠른 매칭
        /// </summary>
        public ModelItem FindBySyncIdCached(string syncId, AWP4DOptions options)
        {
            if (string.IsNullOrEmpty(syncId))
                return null;

            string key = options.IgnoreCaseInMatching ? syncId.ToLowerInvariant() : syncId;

            if (_propertyValueCache.TryGetValue(key, out List<ModelItem> items))
            {
                if (items.Count == 1 || options.AllowMultipleMatches)
                {
                    return items.First();
                }
                else if (items.Count > 1)
                {
                    if (VerboseLogging)
                        System.Diagnostics.Debug.WriteLine(
                            $"[ObjectMatcher] '{syncId}' 다중 매칭 (캐시): {items.Count}개");
                    return items.First();
                }
            }

            // 캐시에 없으면 일반 검색
            return FindBySyncId(syncId, options);
        }

        /// <summary>
        /// InstanceGuid 기반 ModelItem 검색
        /// Pipeline 4D에서 AllProperties CSV의 ObjectId(=InstanceGuid)로 매칭할 때 사용
        /// </summary>
        /// <param name="instanceGuid">Navisworks ModelItem의 InstanceGuid</param>
        /// <returns>매칭된 ModelItem (없으면 null)</returns>
        public ModelItem FindByInstanceGuid(Guid instanceGuid)
        {
            if (instanceGuid == Guid.Empty)
                return null;

            string key = instanceGuid.ToString();

            // 캐시 확인
            if (UseCache && _matchCache.TryGetValue(key, out ModelItem cached))
                return cached;

            // InstanceGuid 맵이 없으면 구축
            if (_instanceGuidMap == null)
                BuildInstanceGuidMap();

            ModelItem found;
            if (_instanceGuidMap.TryGetValue(instanceGuid, out found))
            {
                if (UseCache)
                    _matchCache.TryAdd(key, found);
                return found;
            }

            if (VerboseLogging)
                System.Diagnostics.Debug.WriteLine($"[ObjectMatcher] InstanceGuid '{instanceGuid}' 매칭 실패");

            return null;
        }

        /// <summary>
        /// InstanceGuid → ModelItem 맵 구축 (1회만 실행)
        /// </summary>
        private Dictionary<Guid, ModelItem> _instanceGuidMap;

        /// <summary>
        /// DoEvents 호출용 카운터 (ContextSwitchDeadlock 방지)
        /// </summary>
        private int _guidMapCounter;

        public void BuildInstanceGuidMap()
        {
            _instanceGuidMap = new Dictionary<Guid, ModelItem>();
            _guidMapCounter = 0;
            var doc = Application.ActiveDocument;
            if (doc == null) return;

            foreach (var model in doc.Models)
            {
                BuildInstanceGuidMapRecursive(model.RootItem);
            }

            if (VerboseLogging)
                System.Diagnostics.Debug.WriteLine(
                    $"[ObjectMatcher] InstanceGuid 맵 구축 완료: {_instanceGuidMap.Count}개 항목");
        }

        private void BuildInstanceGuidMapRecursive(ModelItem item)
        {
            if (item == null) return;

            if (item.InstanceGuid != Guid.Empty)
            {
                _instanceGuidMap[item.InstanceGuid] = item;
            }

            _guidMapCounter++;
            if (_guidMapCounter % 5000 == 0)
            {
                WinForms.Application.DoEvents();
            }

            foreach (ModelItem child in item.Children)
            {
                BuildInstanceGuidMapRecursive(child);
            }
        }

        /// <summary>
        /// 매칭 통계 반환
        /// </summary>
        public string GetCacheStatistics()
        {
            return $"캐시 크기: {_matchCache.Count}개 직접 매칭, {_propertyValueCache.Count}개 속성값, InstanceGuid 맵: {_instanceGuidMap?.Count ?? 0}개";
        }

        protected virtual void OnProgressChanged(ObjectMatchProgressEventArgs e)
        {
            ProgressChanged?.Invoke(this, e);
        }
    }

    /// <summary>
    /// Object Matching 진행 이벤트 인자
    /// </summary>
    public class ObjectMatchProgressEventArgs : EventArgs
    {
        public int CurrentIndex { get; set; }
        public int TotalCount { get; set; }
        public string CurrentSyncId { get; set; }
        public MatchStatus Status { get; set; }
        public double Progress { get; set; }
    }
}
