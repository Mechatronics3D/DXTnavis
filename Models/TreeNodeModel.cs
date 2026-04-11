using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace DXTnavis.Models
{
    /// <summary>
    /// TreeView에 표시될 계층 구조 노드 모델
    /// Lazy loading 지원: 자식 노드는 확장 시 on-demand 로드
    /// </summary>
    public class TreeNodeModel : INotifyPropertyChanged
    {
        private bool _isSelected;
        private bool _isExpanded;
        private bool _isLazyLoaded;

        /// <summary>
        /// Lazy loading 더미 자식 노드 (TreeView에서 확장 화살표 표시용)
        /// </summary>
        internal static readonly TreeNodeModel DummyChild = new TreeNodeModel
        {
            DisplayName = "Loading...",
            Level = -1
        };

        // Level별 색상 팔레트
        private static readonly string[] LevelColors = new[]
        {
            "#0078D4", // L0 - Blue
            "#28A745", // L1 - Green
            "#FFC107", // L2 - Yellow/Orange
            "#DC3545", // L3 - Red
            "#6F42C1", // L4 - Purple
            "#20C997", // L5 - Teal
            "#FD7E14", // L6 - Orange
            "#E83E8C", // L7 - Pink
            "#17A2B8", // L8 - Cyan
            "#6C757D"  // L9+ - Gray
        };

        /// <summary>
        /// Navisworks ModelItem의 GUID
        /// </summary>
        public Guid ObjectId { get; set; }

        /// <summary>
        /// 표시 이름
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// 계층 레벨 (0부터 시작)
        /// </summary>
        public int Level { get; set; }

        /// <summary>
        /// Level 기반 접두사 (예: "L0", "L1", "L2")
        /// TreeView에서 계층 레벨을 시각적으로 표시
        /// </summary>
        public string LevelPrefix => $"L{Level}";

        /// <summary>
        /// Level 기반 배경 색상
        /// </summary>
        public string LevelColor => LevelColors[Math.Min(Math.Max(Level, 0), LevelColors.Length - 1)];

        /// <summary>
        /// 노드 아이콘 (자식 유무에 따라)
        /// </summary>
        public string NodeIcon => Children.Count > 0 && !HasDummyChild ? "📁" : (HasGeometry ? "🔷" : "📄");

        /// <summary>
        /// 자식 개수 텍스트 (자식이 있을 때만 표시)
        /// </summary>
        public string ChildCountText => Children.Count > 0 && !HasDummyChild ? $"({Children.Count})" : "";

        /// <summary>
        /// 형상 존재 여부
        /// </summary>
        public bool HasGeometry { get; set; }

        /// <summary>
        /// Navisworks ModelItem 참조 (lazy loading용, object로 저장하여 API 의존성 분리)
        /// </summary>
        public object SourceItem { get; set; }

        /// <summary>
        /// 자식이 아직 로드되지 않았는지 여부
        /// </summary>
        public bool HasDummyChild => Children.Count == 1 && Children[0] == DummyChild;

        /// <summary>
        /// Lazy loading 완료 여부
        /// </summary>
        public bool IsLazyLoaded
        {
            get => _isLazyLoaded;
            set => _isLazyLoaded = value;
        }

        /// <summary>
        /// 자식 노드 컬렉션
        /// </summary>
        public ObservableCollection<TreeNodeModel> Children { get; set; }

        /// <summary>
        /// 자식 노드 lazy load 요청 이벤트
        /// ViewModel에서 구독하여 Navisworks API로 자식 로드
        /// </summary>
        public event EventHandler LazyLoadRequested;

        /// <summary>
        /// 선택 상태 — 자식 노드에 연쇄 적용 (무통지 일괄 처리)
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    // 자식을 통지 없이 일괄 설정한 뒤 한 번만 통지
                    SetSelectionSilent(value);
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        /// <summary>
        /// 자신과 모든 자손의 _isSelected를 PropertyChanged 없이 설정
        /// </summary>
        private void SetSelectionSilent(bool value)
        {
            _isSelected = value;
            if (!HasDummyChild)
            {
                foreach (var child in Children)
                {
                    child.SetSelectionSilent(value);
                }
            }
        }

        /// <summary>
        /// 확장 상태 — 확장 시 lazy load 트리거
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                _isExpanded = value;

                // Lazy loading: 처음 확장할 때 자식 로드 요청
                if (_isExpanded && HasDummyChild && !_isLazyLoaded)
                {
                    LazyLoadRequested?.Invoke(this, EventArgs.Empty);
                }

                OnPropertyChanged(nameof(IsExpanded));
            }
        }

        public TreeNodeModel()
        {
            Children = new ObservableCollection<TreeNodeModel>();
        }

        /// <summary>
        /// 아직 로드되지 않은 자식이 있음을 표시하는 더미 자식 추가
        /// </summary>
        public void AddDummyChild()
        {
            Children.Add(DummyChild);
        }

        /// <summary>
        /// 지정된 레벨까지 확장 (lazy-loaded 노드는 확장 시 자동 로드)
        /// </summary>
        /// <param name="targetLevel">확장할 최대 레벨</param>
        public void ExpandToLevel(int targetLevel)
        {
            if (Level < targetLevel)
            {
                IsExpanded = true;
                if (!HasDummyChild)
                {
                    foreach (var child in Children)
                    {
                        child.ExpandToLevel(targetLevel);
                    }
                }
            }
            else
            {
                IsExpanded = false;
            }
        }

        /// <summary>
        /// 모든 노드 축소
        /// </summary>
        public void CollapseAll()
        {
            IsExpanded = false;
            if (!HasDummyChild)
            {
                foreach (var child in Children)
                {
                    child.CollapseAll();
                }
            }
        }

        /// <summary>
        /// 모든 노드 확장
        /// </summary>
        public void ExpandAll()
        {
            IsExpanded = true;
            if (!HasDummyChild)
            {
                foreach (var child in Children)
                {
                    child.ExpandAll();
                }
            }
        }

        /// <summary>
        /// 특정 레벨의 노드만 확장/축소 (부모 레벨은 자동으로 확장)
        /// </summary>
        /// <param name="targetLevel">대상 레벨</param>
        /// <param name="expand">true=확장, false=축소</param>
        public void SetLevelExpansion(int targetLevel, bool expand)
        {
            if (HasDummyChild) return;

            if (Level < targetLevel)
            {
                // 부모 레벨: 대상 레벨에 도달하기 위해 확장
                IsExpanded = true;
                foreach (var child in Children)
                {
                    child.SetLevelExpansion(targetLevel, expand);
                }
            }
            else if (Level == targetLevel)
            {
                // 대상 레벨: 확장/축소 설정
                IsExpanded = expand;
                if (!expand)
                {
                    // 축소 시 하위 레벨도 모두 축소
                    foreach (var child in Children)
                    {
                        child.CollapseAll();
                    }
                }
            }
        }

        /// <summary>
        /// 특정 레벨까지만 확장 (그 이후 레벨은 축소)
        /// </summary>
        /// <param name="maxLevel">최대 확장 레벨</param>
        public void ExpandExactlyToLevel(int maxLevel)
        {
            if (HasDummyChild) return;

            if (Level < maxLevel)
            {
                IsExpanded = true;
                foreach (var child in Children)
                {
                    child.ExpandExactlyToLevel(maxLevel);
                }
            }
            else if (Level == maxLevel)
            {
                IsExpanded = true;
                // 자식은 축소
                foreach (var child in Children)
                {
                    child.CollapseAll();
                }
            }
            else
            {
                IsExpanded = false;
            }
        }

        /// <summary>
        /// 특정 레벨의 노드 선택/해제 (연쇄 없이 해당 레벨만)
        /// </summary>
        public void SetSelectionByLevel(int targetLevel, bool selected)
        {
            if (Level == targetLevel)
            {
                _isSelected = selected;
                OnPropertyChanged(nameof(IsSelected));
            }
            if (!HasDummyChild)
            {
                foreach (var child in Children)
                {
                    child.SetSelectionByLevel(targetLevel, selected);
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
