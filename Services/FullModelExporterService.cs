using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.Navisworks.Api;
using DXTnavis.Models;
using WinForms = System.Windows.Forms;

namespace DXTnavis.Services
{
    /// <summary>
    /// 모델 전체 속성을 스트리밍 방식으로 내보내는 서비스
    /// 대용량 데이터 처리에 최적화되어 메모리 사용량을 최소화합니다.
    /// </summary>
    public class FullModelExporterService
    {
        /// <summary>
        /// 전체 모델의 모든 속성을 계층 구조 포함하여 CSV 파일로 내보내기
        /// 각 객체를 행으로, 모든 속성을 열로 표시합니다.
        /// </summary>
        /// <param name="filePath">저장할 파일 경로</param>
        /// <param name="progress">진행률 보고를 위한 Progress 인스턴스</param>
        /// <summary>
        /// 이미 로드된 데이터를 사용하여 CSV 내보내기 (재추출 불필요)
        /// </summary>
        public void ExportAllPropertiesToCsv(string filePath, IProgress<(int percentage, string message)> progress, List<HierarchicalPropertyRecord> preloadedData)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("파일 경로가 유효하지 않습니다.", nameof(filePath));

            if (preloadedData == null || preloadedData.Count == 0)
                throw new InvalidOperationException("내보낼 데이터가 없습니다.");

            progress?.Report((0, "캐시된 데이터로 내보내기 시작..."));

            ExportHierarchicalDataToCsv(filePath, preloadedData, progress);
        }

        public void ExportAllPropertiesToCsv(string filePath, IProgress<(int percentage, string message)> progress)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("파일 경로가 유효하지 않습니다.", nameof(filePath));

            var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("활성 문서가 없습니다.");

            progress?.Report((0, "모델 계층 구조 분석 중..."));

            // 계층 구조 데이터 추출
            var extractor = new NavisworksDataExtractor();
            var hierarchicalData = new List<HierarchicalPropertyRecord>();

            int modelIndex = 0;
            int modelCount = doc.Models.Count;
            foreach (var model in doc.Models)
            {
                extractor.TraverseAndExtractProperties(model.RootItem, Guid.Empty, 0, hierarchicalData);
                modelIndex++;
                progress?.Report((modelIndex * 10 / Math.Max(modelCount, 1), $"모델 {modelIndex}/{modelCount} 추출 완료..."));
                WinForms.Application.DoEvents();
            }

            // UI 스레드에서 실행되므로 DoEvents로 ContextSwitchDeadlock 방지
            WinForms.Application.DoEvents();

            if (hierarchicalData.Count == 0)
            {
                throw new InvalidOperationException("내보낼 데이터가 없습니다.");
            }

            ExportHierarchicalDataToCsv(filePath, hierarchicalData, progress);
        }

        /// <summary>
        /// 선택된 객체의 모든 속성을 계층 구조 포함하여 CSV 파일로 내보내기
        /// </summary>
        public void ExportPropertiesFromSelectionToCsv(
            ModelItemCollection selectedItems,
            string filePath,
            IProgress<(int percentage, string message)> progress)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("파일 경로가 유효하지 않습니다.", nameof(filePath));
            if (selectedItems == null || selectedItems.Count == 0)
                throw new ArgumentException("선택된 객체가 없습니다.");

            progress?.Report((0, "선택 객체 속성 분석 중..."));

            var extractor = new NavisworksDataExtractor();
            var hierarchicalData = extractor.ExtractHierarchicalRecordsFromSelection(selectedItems);

            WinForms.Application.DoEvents();

            if (hierarchicalData.Count == 0)
            {
                throw new InvalidOperationException("내보낼 데이터가 없습니다.");
            }

            progress?.Report((10, "속성 구조 분석 중..."));

            var allPropertyKeys = new SortedSet<string>();
            var objectDataMap = new Dictionary<Guid, Dictionary<string, string>>();

            foreach (var record in hierarchicalData)
            {
                string propertyKey = $"{record.Category}|{record.PropertyName}";
                allPropertyKeys.Add(propertyKey);

                if (!objectDataMap.ContainsKey(record.ObjectId))
                {
                    objectDataMap[record.ObjectId] = new Dictionary<string, string>
                    {
                        ["__ObjectId"] = record.ObjectId.ToString(),
                        ["__ParentId"] = record.ParentId.ToString(),
                        ["__Level"] = record.Level.ToString(),
                        ["__DisplayName"] = record.DisplayName
                    };
                }

                objectDataMap[record.ObjectId][propertyKey] = record.PropertyValue;
            }

            WinForms.Application.DoEvents();
            progress?.Report((30, $"총 {objectDataMap.Count:N0}개 객체, {allPropertyKeys.Count:N0}개 고유 속성 발견. CSV 생성 중..."));
            WinForms.Application.DoEvents();

            using (var writer = new StreamWriter(filePath, false, Encoding.UTF8))
            {
                var headerParts = new List<string>
                {
                    "ObjectId",
                    "ParentId",
                    "Level",
                    "객체이름"
                };
                headerParts.AddRange(allPropertyKeys);
                writer.WriteLine(string.Join(",", headerParts.Select(h => EscapeCsvField(h))));

                int processedObjects = 0;
                foreach (var kvp in objectDataMap)
                {
                    try
                    {
                        var objectData = kvp.Value;
                        var rowParts = new List<string>
                        {
                            objectData["__ObjectId"],
                            objectData["__ParentId"],
                            objectData["__Level"],
                            EscapeCsvField(objectData["__DisplayName"])
                        };

                        foreach (var propertyKey in allPropertyKeys)
                        {
                            string value = objectData.ContainsKey(propertyKey) ? objectData[propertyKey] : string.Empty;
                            rowParts.Add(EscapeCsvField(value));
                        }

                        writer.WriteLine(string.Join(",", rowParts));
                        processedObjects++;

                        if (processedObjects % 100 == 0)
                        {
                            int percentage = 30 + (int)((processedObjects / (double)objectDataMap.Count) * 65);
                            progress?.Report((percentage, $"{processedObjects:N0} / {objectDataMap.Count:N0} 객체 저장 중..."));
                            WinForms.Application.DoEvents();
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"객체 저장 중 오류: {ex.Message}");
                    }
                }
            }

            progress?.Report((100, $"✅ 완료! {objectDataMap.Count:N0}개 객체, {allPropertyKeys.Count:N0}개 속성 열 저장됨"));
        }

        /// <summary>
        /// HierarchicalPropertyRecord 리스트를 CSV 파일로 변환하는 공통 메서드
        /// </summary>
        private void ExportHierarchicalDataToCsv(string filePath, List<HierarchicalPropertyRecord> hierarchicalData, IProgress<(int percentage, string message)> progress)
        {
            progress?.Report((10, "속성 구조 분석 중..."));

            // 1단계: 모든 고유한 속성 이름 수집 (Category | PropertyName 형식)
            var allPropertyKeys = new SortedSet<string>();
            var objectDataMap = new Dictionary<Guid, Dictionary<string, string>>();

            foreach (var record in hierarchicalData)
            {
                string propertyKey = $"{record.Category}|{record.PropertyName}";
                allPropertyKeys.Add(propertyKey);

                if (!objectDataMap.ContainsKey(record.ObjectId))
                {
                    objectDataMap[record.ObjectId] = new Dictionary<string, string>
                    {
                        ["__ObjectId"] = record.ObjectId.ToString(),
                        ["__ParentId"] = record.ParentId.ToString(),
                        ["__Level"] = record.Level.ToString(),
                        ["__DisplayName"] = record.DisplayName
                    };
                }

                objectDataMap[record.ObjectId][propertyKey] = record.PropertyValue;
            }

            WinForms.Application.DoEvents();
            progress?.Report((30, $"총 {objectDataMap.Count:N0}개 객체, {allPropertyKeys.Count:N0}개 고유 속성 발견. CSV 생성 중..."));
            WinForms.Application.DoEvents();

            using (var writer = new StreamWriter(filePath, false, Encoding.UTF8))
            {
                // CSV 헤더 작성
                var headerParts = new List<string>
                {
                    "ObjectId",
                    "ParentId",
                    "Level",
                    "객체이름"
                };
                headerParts.AddRange(allPropertyKeys);
                writer.WriteLine(string.Join(",", headerParts.Select(h => EscapeCsvField(h))));

                // 각 객체를 행으로 출력
                int processedObjects = 0;
                foreach (var kvp in objectDataMap)
                {
                    try
                    {
                        var objectData = kvp.Value;
                        var rowParts = new List<string>
                        {
                            objectData["__ObjectId"],
                            objectData["__ParentId"],
                            objectData["__Level"],
                            EscapeCsvField(objectData["__DisplayName"])
                        };

                        // 각 속성 열에 대해 값이 있으면 출력, 없으면 빈 문자열
                        foreach (var propertyKey in allPropertyKeys)
                        {
                            string value = objectData.ContainsKey(propertyKey) ? objectData[propertyKey] : string.Empty;
                            rowParts.Add(EscapeCsvField(value));
                        }

                        writer.WriteLine(string.Join(",", rowParts));
                        processedObjects++;

                        // 100개 객체마다 진행률 보고 + DoEvents
                        if (processedObjects % 100 == 0)
                        {
                            int percentage = 30 + (int)((processedObjects / (double)objectDataMap.Count) * 65);
                            progress?.Report((percentage, $"{processedObjects:N0} / {objectDataMap.Count:N0} 객체 저장 중..."));
                            WinForms.Application.DoEvents();
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"객체 저장 중 오류: {ex.Message}");
                    }
                }
            }

            progress?.Report((100, $"완료! {objectDataMap.Count:N0}개 객체, {allPropertyKeys.Count:N0}개 속성 열 저장됨"));
        }

        /// <summary>
        /// CSV 필드 이스케이프 처리
        /// </summary>
        private string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field))
                return string.Empty;

            // 쉼표, 따옴표, 줄바꿈이 포함된 경우 이스케이프 필요
            if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
            {
                field = field.Replace("\"", "\"\"");
                return $"\"{field}\"";
            }

            return field;
        }
    }
}
