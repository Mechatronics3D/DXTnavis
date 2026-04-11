using System;
using System.Collections.Generic;
using Autodesk.Navisworks.Api;
using DXTnavis.Helpers;
using DXTnavis.Models;

namespace DXTnavis.ViewModels
{
    /// <summary>
    /// DXwindowViewModel - Tree Expand/Collapse 관련 메서드
    /// v0.5.0: Partial Class 분리
    /// </summary>
    public partial class DXwindowViewModel
    {
        #region Tree Expand/Collapse Methods

        /// <summary>
        /// 지정된 레벨까지 트리 확장
        /// </summary>
        private void ExpandTreeToLevel(int targetLevel)
        {
            try
            {
                foreach (var node in ObjectHierarchyRoot)
                {
                    node.ExpandToLevel(targetLevel);
                }
                StatusMessage = $"Tree expanded to Level {targetLevel}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// 모든 트리 노드 축소
        /// </summary>
        private void CollapseAllTreeNodes()
        {
            try
            {
                foreach (var node in ObjectHierarchyRoot)
                {
                    node.CollapseAll();
                }
                StatusMessage = "Tree collapsed";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// 모든 트리 노드 확장
        /// </summary>
        private void ExpandAllTreeNodes()
        {
            try
            {
                foreach (var node in ObjectHierarchyRoot)
                {
                    node.ExpandAll();
                }
                StatusMessage = "Tree fully expanded";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// 트리 확장/축소 명령의 CanExecute 갱신
        /// </summary>
        private void RefreshTreeCommands()
        {
            ((RelayCommand)ExpandToLevelCommand)?.RaiseCanExecuteChanged();
            ((RelayCommand)CollapseAllCommand)?.RaiseCanExecuteChanged();
            ((RelayCommand)ExpandAllCommand)?.RaiseCanExecuteChanged();
            ((RelayCommand)ExpandLevelCommand)?.RaiseCanExecuteChanged();
        }

        /// <summary>
        /// 특정 레벨까지 정확히 확장 (P1 Feature)
        /// 클릭한 레벨까지 확장하고, 그 이후 레벨은 축소
        /// </summary>
        /// <param name="targetLevel">확장할 레벨 (0부터 시작)</param>
        private void ExpandToSpecificLevel(int targetLevel)
        {
            try
            {
                foreach (var node in ObjectHierarchyRoot)
                {
                    node.ExpandExactlyToLevel(targetLevel);
                }
                StatusMessage = $"Expanded to L{targetLevel} (children collapsed)";
                SelectedExpandLevel = targetLevel; // ComboBox도 동기화
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// Lazy loading 기반 TreeNodeModel 트리 구축
        /// maxDepth까지만 자식을 즉시 로드하고, 그 이후는 더미 자식으로 지연 로드
        /// </summary>
        /// <param name="item">Navisworks ModelItem</param>
        /// <param name="level">현재 계층 레벨</param>
        /// <param name="allNodes">모든 생성된 노드 목록 (이벤트 구독용)</param>
        /// <param name="maxDepth">즉시 로드할 최대 깊이 (이 깊이 이후는 lazy load)</param>
        private TreeNodeModel BuildTreeFromModelItem(ModelItem item, int level, List<TreeNodeModel> allNodes, int maxDepth = 2)
        {
            if (item == null || item.IsHidden)
                return null;

            // 현재 노드 생성
            var node = new TreeNodeModel
            {
                ObjectId = item.InstanceGuid,
                DisplayName = GetDisplayNameFromModelItem(item),
                Level = level,
                HasGeometry = item.HasGeometry,
                SourceItem = item  // Lazy loading용 ModelItem 참조 저장
            };

            allNodes.Add(node);

            // maxDepth 이내: 자식을 즉시 로드
            if (level < maxDepth)
            {
                foreach (ModelItem child in item.Children)
                {
                    var childNode = BuildTreeFromModelItem(child, level + 1, allNodes, maxDepth);
                    if (childNode != null)
                    {
                        node.Children.Add(childNode);
                    }
                }
                node.IsLazyLoaded = true;
            }
            else
            {
                // maxDepth 이후: 자식이 있으면 더미 자식 추가 (확장 화살표 표시)
                bool hasVisibleChildren = false;
                foreach (ModelItem child in item.Children)
                {
                    if (child != null && !child.IsHidden)
                    {
                        hasVisibleChildren = true;
                        break;
                    }
                }

                if (hasVisibleChildren)
                {
                    node.AddDummyChild();
                }
                else
                {
                    node.IsLazyLoaded = true;
                }
            }

            return node;
        }

        /// <summary>
        /// Lazy loading 콜백: 노드 확장 시 자식 노드를 실제 로드
        /// </summary>
        private void OnLazyLoadRequested(object sender, EventArgs e)
        {
            var node = sender as TreeNodeModel;
            if (node == null || node.IsLazyLoaded || !node.HasDummyChild)
                return;

            var modelItem = node.SourceItem as ModelItem;
            if (modelItem == null)
                return;

            // 더미 자식 제거
            node.Children.Clear();

            var newNodes = new List<TreeNodeModel>();

            // 자식 노드를 1레벨만 로드 (각 자식은 다시 lazy)
            foreach (ModelItem child in modelItem.Children)
            {
                var childNode = BuildTreeFromModelItem(child, node.Level + 1, newNodes, node.Level + 1);
                if (childNode != null)
                {
                    node.Children.Add(childNode);
                }
            }

            // 새 노드에 이벤트 구독
            foreach (var newNode in newNodes)
            {
                newNode.PropertyChanged += OnTreeNodeSelectionChanged;
                newNode.LazyLoadRequested += OnLazyLoadRequested;
            }

            node.IsLazyLoaded = true;
        }

        /// <summary>
        /// ModelItem에서 표시 이름 추출
        /// </summary>
        private string GetDisplayNameFromModelItem(ModelItem item)
        {
            try
            {
                // 먼저 DisplayName 속성 시도
                if (!string.IsNullOrWhiteSpace(item.DisplayName))
                    return item.DisplayName;

                // "Item" 카테고리의 "Name" 속성 찾기
                foreach (var category in item.PropertyCategories)
                {
                    if (category?.DisplayName == "Item")
                    {
                        try
                        {
                            var properties = category.Properties;
                            foreach (DataProperty property in properties)
                            {
                                if (property?.DisplayName == "Name")
                                {
                                    return property.Value?.ToString() ?? item.InstanceGuid.ToString();
                                }
                            }
                        }
                        catch { continue; }
                    }
                }

                // 이름을 찾지 못하면 GUID 사용
                return item.InstanceGuid.ToString();
            }
            catch
            {
                return "Unknown";
            }
        }

        #endregion
    }
}
