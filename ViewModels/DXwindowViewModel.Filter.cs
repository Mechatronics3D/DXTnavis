using System;
using System.Linq;
using DXTnavis.Helpers;

namespace DXTnavis.ViewModels
{
    /// <summary>
    /// DXwindowViewModel - Filter 관련 메서드
    /// v0.5.0: Partial Class 분리
    /// </summary>
    public partial class DXwindowViewModel
    {
        #region Filter Methods

        /// <summary>
        /// 필터 적용
        /// Phase 12: 그룹 기반 필터링 통합
        /// </summary>
        private void ApplyFilter()
        {
            // Phase 12: 그룹 기반 필터링
            if (AllObjectGroups.Count > 0)
            {
                SyncFilteredGroups();

                // 호환성: HierarchicalPropertyRecord도 동기화
                FilteredHierarchicalProperties.Clear();
                foreach (var group in FilteredObjectGroups)
                {
                    foreach (var record in group.ToHierarchicalRecords())
                    {
                        FilteredHierarchicalProperties.Add(record);
                    }
                }
            }
            else
            {
                // 기존 방식 (Fallback)
                FilteredHierarchicalProperties.Clear();

                foreach (var prop in AllHierarchicalProperties)
                {
                    bool matchLevel = string.IsNullOrEmpty(SelectedLevelFilter) ||
                                     SelectedLevelFilter == "(All)" ||
                                     $"L{prop.Level}" == SelectedLevelFilter;

                    bool matchSysPath = string.IsNullOrEmpty(SysPathFilter) ||
                                       (prop.SysPath?.IndexOf(SysPathFilter, StringComparison.OrdinalIgnoreCase) >= 0);

                    bool matchCategory = string.IsNullOrEmpty(SelectedCategoryFilter) ||
                                        SelectedCategoryFilter == "(All)" ||
                                        prop.Category == SelectedCategoryFilter;

                    bool matchPropertyName = string.IsNullOrEmpty(PropertyNameFilter) ||
                                            (prop.PropertyName?.IndexOf(PropertyNameFilter, StringComparison.OrdinalIgnoreCase) >= 0);

                    bool matchPropertyValue = string.IsNullOrEmpty(PropertyValueFilter) ||
                                             (prop.PropertyValue?.IndexOf(PropertyValueFilter, StringComparison.OrdinalIgnoreCase) >= 0);

                    if (matchLevel && matchSysPath && matchCategory && matchPropertyName && matchPropertyValue)
                    {
                        FilteredHierarchicalProperties.Add(prop);
                    }
                }
            }

            StatusMessage = $"Filtered: {FilteredGroupCount:N0} groups ({FilteredPropertyCount:N0} properties)";
            OnPropertyChanged(nameof(SelectedPropertiesCount));
            ((RelayCommand)CreateSearchSetCommand).RaiseCanExecuteChanged();
            RefreshSelectionCommands();

            // v0.9.0: Select All 가용성 체크
            OnPropertyChanged(nameof(IsSelectAllAvailable));
            OnPropertyChanged(nameof(SelectAllTooltip));

            // Phase 12: 그룹 선택 상태 갱신
            UpdateGroupSelectAllState();
            OnPropertyChanged(nameof(SelectedGroupCount));
            OnPropertyChanged(nameof(SelectionSummary));

            // Phase 11: 그룹화 뷰 가용성 체크 및 갱신
            UpdateGroupedViewAvailability();
        }

        /// <summary>
        /// v0.4.3: 필터 디바운스 트리거
        /// 필터 값 변경 시 호출되어 200ms 후 자동 필터 적용
        /// </summary>
        private void TriggerFilterDebounce()
        {
            // 데이터가 없으면 필터 적용 안함
            if (AllHierarchicalProperties.Count == 0 && AllObjectGroups.Count == 0)
                return;

            _filterDebounceTimer?.Stop();
            _filterDebounceTimer?.Start();
        }

        /// <summary>
        /// 필터 초기화
        /// Phase 12: 체크박스 필터 초기화 추가
        /// </summary>
        private void ClearFilter()
        {
            SelectedLevelFilter = null;
            SysPathFilter = string.Empty;
            SelectedCategoryFilter = null;
            PropertyNameFilter = string.Empty;
            PropertyValueFilter = string.Empty;

            // Phase 12: 체크박스 필터 모두 선택
            foreach (var opt in LevelFilterOptions)
            {
                opt.IsChecked = true;
            }
            foreach (var opt in CategoryFilterOptions)
            {
                opt.IsChecked = true;
            }

            // Phase 12: 그룹 필터 초기화
            if (AllObjectGroups.Count > 0)
            {
                foreach (var group in AllObjectGroups)
                {
                    group.ResetFilter();
                }

                FilteredObjectGroups.Clear();
                foreach (var group in AllObjectGroups)
                {
                    FilteredObjectGroups.Add(group);
                }

                // 호환성: HierarchicalPropertyRecord도 동기화
                FilteredHierarchicalProperties.Clear();
                foreach (var group in AllObjectGroups)
                {
                    foreach (var record in group.ToHierarchicalRecords())
                    {
                        FilteredHierarchicalProperties.Add(record);
                    }
                }

                StatusMessage = $"Total: {AllObjectGroups.Count:N0} groups ({FilteredPropertyCount:N0} properties)";
            }
            else
            {
                FilteredHierarchicalProperties.Clear();
                foreach (var prop in AllHierarchicalProperties)
                {
                    FilteredHierarchicalProperties.Add(prop);
                }

                StatusMessage = $"Total: {AllHierarchicalProperties.Count} items";
            }

            OnPropertyChanged(nameof(FilteredGroupCount));
            OnPropertyChanged(nameof(FilteredPropertyCount));
            OnPropertyChanged(nameof(SelectedGroupCount));
            OnPropertyChanged(nameof(SelectionSummary));
            OnPropertyChanged(nameof(SelectedPropertiesCount));
            ((RelayCommand)CreateSearchSetCommand).RaiseCanExecuteChanged();
            RefreshSelectionCommands();
        }

        /// <summary>
        /// AllHierarchicalProperties가 업데이트될 때 AvailableCategories, AvailableLevels, FilteredHierarchicalProperties 동기화
        /// </summary>
        private void SyncFilteredProperties()
        {
            // Level 목록 업데이트
            AvailableLevels.Clear();
            AvailableLevels.Add("(All)");
            var levels = AllHierarchicalProperties
                .Select(p => p.Level)
                .Distinct()
                .OrderBy(l => l)
                .Select(l => $"L{l}");

            foreach (var level in levels)
            {
                AvailableLevels.Add(level);
            }

            // 카테고리 목록 업데이트
            AvailableCategories.Clear();
            AvailableCategories.Add("(All)");
            var categories = AllHierarchicalProperties
                .Select(p => p.Category)
                .Where(c => !string.IsNullOrEmpty(c))
                .Distinct()
                .OrderBy(c => c);

            foreach (var category in categories)
            {
                AvailableCategories.Add(category);
            }

            // 필터링된 목록 초기화 (전체 표시)
            FilteredHierarchicalProperties.Clear();
            foreach (var prop in AllHierarchicalProperties)
            {
                FilteredHierarchicalProperties.Add(prop);
            }

            // 필터 초기화
            SelectedLevelFilter = null;
            SysPathFilter = string.Empty;
            SelectedCategoryFilter = null;
            PropertyNameFilter = string.Empty;
            PropertyValueFilter = string.Empty;

            StatusMessage = $"Loaded: {AllHierarchicalProperties.Count} items";
            RefreshSelectionCommands();

            // v0.9.0: Select All 가용성 체크
            OnPropertyChanged(nameof(IsSelectAllAvailable));
            OnPropertyChanged(nameof(SelectAllTooltip));

            // Phase 11: 그룹화 뷰 가용성 체크
            UpdateGroupedViewAvailability();
        }

        #endregion

        #region Grouped View Methods (Phase 11)

        /// <summary>
        /// 그룹화 뷰 가용성 업데이트
        /// 필터링된 아이템이 10,000개 미만일 때만 그룹화 뷰 사용 가능
        /// </summary>
        private void UpdateGroupedViewAvailability()
        {
            const int MaxItemsForGrouping = 10000;
            IsGroupedViewAvailable = FilteredHierarchicalProperties.Count < MaxItemsForGrouping;

            OnPropertyChanged(nameof(GroupedViewTooltip));

            // 그룹화 뷰가 활성화되어 있으면 갱신
            if (_isGroupedViewEnabled && IsGroupedViewAvailable)
            {
                RefreshGroupedProperties();
            }
        }

        /// <summary>
        /// 그룹화된 속성 목록 갱신
        /// </summary>
        private void RefreshGroupedProperties()
        {
            if (!_isGroupedViewEnabled || !IsGroupedViewAvailable)
            {
                GroupedProperties.Clear();
                OnPropertyChanged(nameof(GroupCount));
                return;
            }

            // 기존 그룹 이벤트 해제
            foreach (var group in GroupedProperties)
            {
                group.PropertyChanged -= OnGroupPropertyChanged;
            }
            GroupedProperties.Clear();

            // 새 그룹 생성
            var newGroups = ObjectGroupViewModel.CreateGroups(FilteredHierarchicalProperties);
            foreach (var group in newGroups)
            {
                group.PropertyChanged += OnGroupPropertyChanged;
                GroupedProperties.Add(group);
            }

            OnPropertyChanged(nameof(GroupCount));
            StatusMessage = $"Grouped: {GroupedProperties.Count} objects ({FilteredHierarchicalProperties.Count} properties)";
        }

        /// <summary>
        /// 그룹 속성 변경 이벤트 핸들러
        /// </summary>
        private void OnGroupPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ObjectGroupViewModel.IsSelected) ||
                e.PropertyName == nameof(ObjectGroupViewModel.SelectedPropertyCount))
            {
                OnPropertyChanged(nameof(SelectedPropertiesCount));
                OnPropertyChanged(nameof(SelectedPropertyInfo));
                ((RelayCommand)CreateSearchSetCommand).RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// 모든 그룹 확장
        /// </summary>
        public void ExpandAllGroups()
        {
            foreach (var group in GroupedProperties)
            {
                group.IsExpanded = true;
            }
        }

        /// <summary>
        /// 모든 그룹 축소
        /// </summary>
        public void CollapseAllGroups()
        {
            foreach (var group in GroupedProperties)
            {
                group.IsExpanded = false;
            }
        }

        #endregion
    }
}
