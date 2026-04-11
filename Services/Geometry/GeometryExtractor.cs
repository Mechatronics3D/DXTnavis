using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Autodesk.Navisworks.Api;
using DXTnavis.Models.Geometry;
// Type alias to avoid ambiguity with Autodesk.Navisworks.Api.Point3D
using DXTPoint3D = DXTnavis.Models.Geometry.Point3D;

namespace DXTnavis.Services.Geometry
{
    /// <summary>
    /// Navisworks ModelItem에서 기하학 정보(BoundingBox, Centroid)를 추출하는 서비스
    /// Phase 15: Geometry Export System
    /// </summary>
    public class GeometryExtractor
    {
        #region Events

        /// <summary>
        /// 진행률 변경 이벤트 (0-100%)
        /// </summary>
        public event EventHandler<int> ProgressChanged;

        /// <summary>
        /// 상태 메시지 이벤트
        /// </summary>
        public event EventHandler<string> StatusChanged;

        #endregion

        #region Properties

        /// <summary>
        /// 마지막 추출 시 생성된 ObjectId → ModelItem 매핑
        /// Mesh 추출 등에서 원본 ModelItem 접근이 필요할 때 사용
        /// </summary>
        public Dictionary<Guid, ModelItem> LastModelItemMap { get; private set; }
            = new Dictionary<Guid, ModelItem>();

        /// <summary>
        /// Phase 24: 마지막 추출 시 기록된 ParentObjectId → List of ChildObjectId 매핑
        /// Container 감지에 사용 — parent.Children wrapper 재계산 없이 안정적으로 판별
        /// </summary>
        public Dictionary<Guid, List<Guid>> LastParentChildMap { get; private set; }
            = new Dictionary<Guid, List<Guid>>();

        #endregion

        #region Public Methods

        /// <summary>
        /// 단일 ModelItem에서 BoundingBox 추출
        /// </summary>
        /// <param name="item">Navisworks ModelItem</param>
        /// <param name="hierarchyPath">계층 경로 (Synthetic ID 생성용)</param>
        /// <returns>GeometryRecord (실패 시 null)</returns>
        public GeometryRecord ExtractBoundingBox(ModelItem item, string hierarchyPath = "")
        {
            if (item == null) return null;

            try
            {
                // Navisworks API: BoundingBox()는 월드 좌표계 반환
                var bbox = item.BoundingBox();

                // BoundingBox가 유효한지 확인
                if (bbox == null || bbox.IsEmpty)
                {
                    Debug.WriteLine($"[GeometryExtractor] Empty BoundingBox for item: {GetDisplayName(item)}");
                    return null;
                }

                // DXTnavis 모델로 변환 (Navisworks Point3D → DXTnavis Point3D)
                var min = new DXTPoint3D(bbox.Min.X, bbox.Min.Y, bbox.Min.Z);
                var max = new DXTPoint3D(bbox.Max.X, bbox.Max.Y, bbox.Max.Z);
                var bboxModel = new BBox3D(min, max);

                // 안정적인 ObjectId 생성
                var objectId = GetStableObjectId(item, hierarchyPath);

                // GeometryRecord 생성
                var record = new GeometryRecord(objectId, bboxModel)
                {
                    DisplayName = GetDisplayName(item),
                    Category = GetCategory(item)
                };

                return record;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GeometryExtractor] Error extracting BBox: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 모든 ModelItem에서 BoundingBox 일괄 추출
        /// </summary>
        /// <param name="items">ModelItem 컬렉션</param>
        /// <param name="cancellationToken">취소 토큰</param>
        /// <returns>ObjectId → GeometryRecord 딕셔너리</returns>
        public Dictionary<Guid, GeometryRecord> ExtractAllBoundingBoxes(
            IEnumerable<ModelItem> items,
            CancellationToken cancellationToken = default,
            Dictionary<ModelItem, ModelItem> childToParent = null)
        {
            var result = new Dictionary<Guid, GeometryRecord>();
            var modelItemMap = new Dictionary<Guid, ModelItem>();
            // Phase 24: extraction 시점에 각 아이템의 StableObjectId를 기록
            var itemToStableId = new Dictionary<ModelItem, Guid>(new ModelItemReferenceComparer());
            var itemList = new List<ModelItem>(items);
            int total = itemList.Count;
            int processed = 0;
            int successful = 0;

            OnStatusChanged($"BoundingBox 추출 시작: {total:N0}개 객체");
            var sw = Stopwatch.StartNew();

            foreach (var item in itemList)
            {
                // 취소 확인
                if (cancellationToken.IsCancellationRequested)
                {
                    OnStatusChanged("추출 취소됨");
                    break;
                }

                // 계층 경로 생성
                var hierarchyPath = BuildHierarchyPath(item);

                // BoundingBox 추출
                var record = ExtractBoundingBox(item, hierarchyPath);
                if (record != null && record.ObjectId != Guid.Empty)
                {
                    result[record.ObjectId] = record;
                    modelItemMap[record.ObjectId] = item;
                    itemToStableId[item] = record.ObjectId;
                    successful++;
                }

                processed++;

                // 100개마다 진행률 보고
                if (processed % 100 == 0 || processed == total)
                {
                    int percentage = (int)(100.0 * processed / total);
                    OnProgressChanged(percentage);
                    OnStatusChanged($"추출 중: {processed:N0}/{total:N0} ({successful:N0}개 성공)");
                }
            }

            // Phase 24: parent → child 관계 구축
            // childToParent는 CollectAllModelItems에서 traversal 시점에 캡처됨
            // 동일 인스턴스를 사용하므로 wrapper 불안정성 문제 없음
            var parentChildMap = new Dictionary<Guid, List<Guid>>();
            if (childToParent != null)
            {
                foreach (var kvp in childToParent)
                {
                    Guid childId;
                    Guid parentId;
                    if (!itemToStableId.TryGetValue(kvp.Key, out childId))
                        continue;
                    if (!itemToStableId.TryGetValue(kvp.Value, out parentId))
                        continue;

                    List<Guid> children;
                    if (!parentChildMap.TryGetValue(parentId, out children))
                    {
                        children = new List<Guid>();
                        parentChildMap[parentId] = children;
                    }
                    children.Add(childId);
                }
            }

            sw.Stop();
            OnProgressChanged(100);
            OnStatusChanged($"추출 완료: {successful:N0}개 BoundingBox ({sw.Elapsed.TotalSeconds:F1}초, {parentChildMap.Count}개 container 관계)");

            LastModelItemMap = modelItemMap;
            LastParentChildMap = parentChildMap;
            return result;
        }

        /// <summary>
        /// Phase 24: ModelItem reference equality comparer
        /// CollectAllModelItems에서 수집한 동일 인스턴스 간 비교에 사용
        /// (parent.Children과 달리 동일 traversal의 인스턴스는 reference-equal)
        /// </summary>
        private class ModelItemReferenceComparer : IEqualityComparer<ModelItem>
        {
            public bool Equals(ModelItem x, ModelItem y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(ModelItem obj)
            {
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
            }
        }

        /// <summary>
        /// 문서의 전체 모델에서 BoundingBox 추출
        /// </summary>
        /// <param name="document">Navisworks Document</param>
        /// <param name="cancellationToken">취소 토큰</param>
        /// <returns>ObjectId → GeometryRecord 딕셔너리</returns>
        public Dictionary<Guid, GeometryRecord> ExtractFromDocument(
            Document document,
            CancellationToken cancellationToken = default)
        {
            if (document?.Models == null || document.Models.Count == 0)
            {
                OnStatusChanged("모델이 없습니다.");
                return new Dictionary<Guid, GeometryRecord>();
            }

            // 모든 ModelItem 수집 + Phase 24: parent-child 관계 기록
            var allItems = new List<ModelItem>();
            var childToParent = new Dictionary<ModelItem, ModelItem>(new ModelItemReferenceComparer());
            CollectAllModelItems(document.Models.RootItems, allItems, childToParent);

            OnStatusChanged($"총 {allItems.Count:N0}개 객체 발견");

            return ExtractAllBoundingBoxes(allItems, cancellationToken, childToParent);
        }

        /// <summary>
        /// 선택된 ModelItem에서 BoundingBox 추출
        /// </summary>
        /// <param name="selection">선택된 ModelItem 컬렉션</param>
        /// <param name="cancellationToken">취소 토큰</param>
        /// <returns>ObjectId → GeometryRecord 딕셔너리</returns>
        public Dictionary<Guid, GeometryRecord> ExtractFromSelection(
            ModelItemCollection selection,
            CancellationToken cancellationToken = default)
        {
            if (selection == null || selection.Count == 0)
            {
                OnStatusChanged("선택된 객체가 없습니다.");
                return new Dictionary<Guid, GeometryRecord>();
            }

            // 선택된 아이템에서 재귀적으로 모든 자식 수집 + parent-child 관계 기록
            var allItems = new List<ModelItem>();
            var childToParent = new Dictionary<ModelItem, ModelItem>(new ModelItemReferenceComparer());
            foreach (var item in selection)
            {
                allItems.Add(item);
                if (item.Children != null && item.Children.Any())
                {
                    CollectAllModelItems(item.Children, allItems, childToParent, item);
                }
            }

            OnStatusChanged($"선택 객체에서 총 {allItems.Count:N0}개 객체 발견");

            return ExtractAllBoundingBoxes(allItems, cancellationToken, childToParent);
        }

        /// <summary>
        /// Phase 24: 외부에서 ModelItem의 StableObjectId를 계산할 수 있도록 공개
        /// parent.Children으로 접근한 child의 StableObjectId를 알아내기 위해 사용
        /// (Navisworks가 새 wrapper 객체를 반환하므로 Dictionary 키 비교 불가)
        /// </summary>
        public Guid ComputeStableObjectId(ModelItem item)
        {
            var hierarchyPath = BuildHierarchyPath(item);
            return GetStableObjectId(item, hierarchyPath);
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// 모든 ModelItem을 재귀적으로 수집
        /// Hidden 객체와 하위 노드는 스킵 (NavisworksDataExtractor.TraverseAndExtractProperties와 동일)
        /// Phase 24: traversal 시점에 child → parent 관계도 기록 (wrapper 안정성 보장)
        /// </summary>
        private void CollectAllModelItems(ModelItemEnumerableCollection rootItems,
            List<ModelItem> result, Dictionary<ModelItem, ModelItem> childToParent,
            ModelItem parentItem = null)
        {
            foreach (var item in rootItems)
            {
                // Hidden 객체 스킵: NavisworksDataExtractor와 동일하게 처리
                // Hidden 부모의 하위 노드도 자동으로 스킵됨
                if (item.IsHidden) continue;

                result.Add(item);
                if (parentItem != null)
                    childToParent[item] = parentItem;

                if (item.Children != null && item.Children.Any())
                {
                    CollectAllModelItems(item.Children, result, childToParent, item);
                }
            }
        }

        /// <summary>
        /// 계층 경로 문자열 생성
        /// NavisworksDataExtractor.TraverseAndExtractProperties()와 동일한 " > " 구분자 사용
        /// </summary>
        private string BuildHierarchyPath(ModelItem item)
        {
            if (item == null) return string.Empty;

            var parts = new List<string>();
            var current = item;

            while (current != null)
            {
                var name = GetDisplayName(current);
                if (!string.IsNullOrEmpty(name))
                {
                    parts.Insert(0, name);
                }
                current = current.Parent;
            }

            return string.Join(" > ", parts);
        }

        /// <summary>
        /// ModelItem의 표시 이름 가져오기
        /// NavisworksDataExtractor.GetDisplayName()와 동일한 폴백 체인
        /// </summary>
        private string GetDisplayName(ModelItem item)
        {
            if (item == null) return string.Empty;

            try
            {
                // 1. DisplayName 우선
                if (!string.IsNullOrWhiteSpace(item.DisplayName))
                    return item.DisplayName;

                // 2. "Item" 카테고리의 "Name" 속성
                foreach (var category in item.PropertyCategories)
                {
                    if (category == null) continue;

                    if (category.DisplayName == "Item")
                    {
                        DataPropertyCollection properties = null;
                        try { properties = category.Properties; }
                        catch { continue; }

                        if (properties == null) continue;

                        foreach (DataProperty property in properties)
                        {
                            if (property == null) continue;
                            try
                            {
                                if (property.DisplayName == "Name")
                                {
                                    var value = property.Value?.ToString();
                                    if (!string.IsNullOrWhiteSpace(value))
                                        return value;
                                }
                            }
                            catch { continue; }
                        }
                    }
                }

                // 3. ClassDisplayName
                try
                {
                    if (!string.IsNullOrWhiteSpace(item.ClassDisplayName))
                        return item.ClassDisplayName;
                }
                catch { }

                // 4. InstanceGuid
                if (item.InstanceGuid != Guid.Empty)
                    return item.InstanceGuid.ToString();

                // 5. Authoring ID
                var authoringId = GetAuthoringId(item);
                if (!string.IsNullOrEmpty(authoringId))
                    return authoringId;

                // 6. 최종 폴백
                return $"Unknown_{item.GetHashCode():X8}";
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// ModelItem의 카테고리 가져오기
        /// </summary>
        private string GetCategory(ModelItem item)
        {
            if (item == null) return string.Empty;

            try
            {
                return item.ClassDisplayName ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        #endregion

        #region Stable Object ID (v1.3.0 패턴 재사용)

        /// <summary>
        /// ModelItem에 대한 안정적인 고유 ID 생성
        /// NavisworksDataExtractor.GetStableObjectId() 로직 기반
        /// </summary>
        private Guid GetStableObjectId(ModelItem item, string hierarchyPath)
        {
            if (item == null)
                return Guid.Empty;

            // 1. InstanceGuid가 유효하면 사용
            if (item.InstanceGuid != Guid.Empty)
                return item.InstanceGuid;

            // 2. Item 카테고리의 GUID 속성 확인
            var itemGuid = GetItemPropertyGuid(item);
            if (itemGuid != Guid.Empty)
                return itemGuid;

            // 3. Authoring ID 확인 (Revit Element ID 등)
            var authoringId = GetAuthoringId(item);
            if (!string.IsNullOrEmpty(authoringId))
            {
                var sourceGuid = GetModelSourceGuid(item);
                return CreateDeterministicGuid(sourceGuid.ToString() + "|" + authoringId);
            }

            // 4. 계층 경로 기반 Synthetic ID (최종 폴백)
            var sourceGuid2 = GetModelSourceGuid(item);
            var pathKey = sourceGuid2.ToString() + "|" + hierarchyPath;
            return CreateDeterministicGuid(pathKey);
        }

        /// <summary>
        /// Item 카테고리에서 GUID 속성 추출
        /// </summary>
        private Guid GetItemPropertyGuid(ModelItem item)
        {
            try
            {
                foreach (var category in item.PropertyCategories)
                {
                    if (category == null) continue;

                    if (category.DisplayName == "Item" || category.Name == "LcOaNode")
                    {
                        DataPropertyCollection properties = null;
                        try { properties = category.Properties; }
                        catch { continue; }

                        if (properties == null) continue;

                        foreach (DataProperty property in properties)
                        {
                            if (property == null) continue;

                            try
                            {
                                var propName = property.DisplayName ?? property.Name ?? string.Empty;
                                if (propName == "GUID" || propName == "Guid" || propName == "guid")
                                {
                                    var value = property.Value?.ToString();
                                    if (!string.IsNullOrEmpty(value) && Guid.TryParse(value, out var guid))
                                    {
                                        return guid;
                                    }
                                }
                            }
                            catch { continue; }
                        }
                    }
                }
            }
            catch { }

            return Guid.Empty;
        }

        /// <summary>
        /// Authoring 도구의 고유 ID 추출 (Revit Element ID, AutoCAD Handle 등)
        /// NavisworksDataExtractor.GetAuthoringId()와 동일한 prefix 로직 사용
        /// </summary>
        private string GetAuthoringId(ModelItem item)
        {
            try
            {
                foreach (var category in item.PropertyCategories)
                {
                    if (category == null) continue;

                    DataPropertyCollection properties = null;
                    try { properties = category.Properties; }
                    catch { continue; }

                    if (properties == null) continue;

                    foreach (DataProperty property in properties)
                    {
                        if (property == null) continue;

                        try
                        {
                            var propName = property.DisplayName ?? property.Name ?? string.Empty;

                            // Revit Element ID
                            if (propName == "Element ID" || propName == "Id" || propName == "ElementId")
                            {
                                var value = property.Value?.ToString();
                                if (!string.IsNullOrEmpty(value))
                                    return $"Revit:{value}";
                            }

                            // AutoCAD Handle
                            if (propName == "Handle" || propName == "Object Handle")
                            {
                                var value = property.Value?.ToString();
                                if (!string.IsNullOrEmpty(value))
                                    return $"AutoCAD:{value}";
                            }

                            // IFC GlobalId
                            if (propName == "GlobalId" || propName == "IfcGlobalId")
                            {
                                var value = property.Value?.ToString();
                                if (!string.IsNullOrEmpty(value))
                                    return $"IFC:{value}";
                            }
                        }
                        catch { continue; }
                    }
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// 모델 파일의 SourceGuid 가져오기
        /// NavisworksDataExtractor.GetModelSourceGuid()와 동일한 ancestor fallback 포함
        /// </summary>
        private Guid GetModelSourceGuid(ModelItem item)
        {
            try
            {
                // item.Model이 있으면 SourceGuid 사용
                if (item?.Model != null)
                {
                    return item.Model.SourceGuid;
                }

                // 없으면 첫 번째 조상의 Model에서 가져오기
                var ancestor = item?.Ancestors.FirstOrDefault();
                if (ancestor?.Model != null)
                {
                    return ancestor.Model.SourceGuid;
                }
            }
            catch { }

            return Guid.Empty;
        }

        /// <summary>
        /// 문자열에서 결정적 GUID 생성 (MD5 해시 기반)
        /// </summary>
        private Guid CreateDeterministicGuid(string input)
        {
            using (var md5 = MD5.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(input);
                var hash = md5.ComputeHash(bytes);
                return new Guid(hash);
            }
        }

        #endregion

        #region Event Helpers

        private void OnProgressChanged(int percentage)
        {
            ProgressChanged?.Invoke(this, percentage);
        }

        private void OnStatusChanged(string message)
        {
            StatusChanged?.Invoke(this, message);
            Debug.WriteLine($"[GeometryExtractor] {message}");
        }

        #endregion
    }
}
