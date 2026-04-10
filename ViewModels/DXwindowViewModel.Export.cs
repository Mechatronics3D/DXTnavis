using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Autodesk.Navisworks.Api;
using DXTnavis.Models;
using DXTnavis.Services;
using DXTnavis.Services.Geometry;
using Microsoft.Win32;
using FolderBrowser = System.Windows.Forms.FolderBrowserDialog;

namespace DXTnavis.ViewModels
{
    /// <summary>
    /// DXwindowViewModel - Export 관련 메서드
    /// v0.5.0: Partial Class 분리
    /// </summary>
    public partial class DXwindowViewModel
    {
        #region Export Methods

        /// <summary>
        /// 현재 UI에 표시된 속성을 파일로 저장
        /// v0.4.0: CSV 저장 시 Raw/Refined 두 파일 동시 생성
        /// </summary>
        private void SaveToFile(FileType fileType)
        {
            try
            {
                var saveDialog = new SaveFileDialog();

                if (fileType == FileType.CSV)
                {
                    saveDialog.Filter = "CSV 파일|*.csv";
                    saveDialog.DefaultExt = "csv";
                }
                else
                {
                    saveDialog.Filter = "JSON 파일|*.json";
                    saveDialog.DefaultExt = "json";
                }

                saveDialog.FileName = $"Properties_{DateTime.Now:yyyyMMdd_HHmmss}";

                if (saveDialog.ShowDialog() == true)
                {
                    var properties = new List<PropertyInfo>(SelectedObjectProperties);
                    var writer = new PropertyFileWriter();

                    if (fileType == FileType.CSV)
                    {
                        // v0.4.0: Raw CSV + Refined CSV 동시 저장
                        var (rawPath, refinedPath) = writer.WriteDualCsv(saveDialog.FileName, properties);
                        MessageBox.Show(
                            $"2개 파일이 저장되었습니다:\n\n• Raw: {System.IO.Path.GetFileName(rawPath)}\n• Refined: {System.IO.Path.GetFileName(refinedPath)}",
                            "저장 완료",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    else
                    {
                        writer.WriteFile(saveDialog.FileName, properties, fileType);
                        MessageBox.Show(
                            "파일이 성공적으로 저장되었습니다.",
                            "저장 완료",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"파일 저장 중 오류가 발생했습니다:\n\n{ex.Message}",
                    "오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 모델 전체 속성을 CSV로 내보내기 (All × Properties)
        /// </summary>
        private async Task ExportAllPropertiesAsync()
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Filter = "CSV 파일|*.csv",
                    DefaultExt = "csv",
                    FileName = $"AllProperties_{DateTime.Now:yyyyMMdd_HHmmss}"
                };

                if (saveDialog.ShowDialog() != true) return;

                // UI 상태 초기화
                IsExporting = true;
                ExportProgressPercentage = 0;
                ExportStatusMessage = "내보내기 시작 중...";

                // 진행률 보고를 위한 Progress 인스턴스
                var progress = new Progress<(int percentage, string message)>(report =>
                {
                    ExportProgressPercentage = report.percentage;
                    ExportStatusMessage = report.message;
                });

                // UI 스레드에서 실행 (Navisworks API는 STA 스레드 필수)
                var exporter = new FullModelExporterService();
                exporter.ExportAllPropertiesToCsv(saveDialog.FileName, progress);

                ExportStatusMessage = "✅ 내보내기 완료!";

                MessageBox.Show(
                    "전체 모델 속성이 성공적으로 저장되었습니다.",
                    "내보내기 완료",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ExportStatusMessage = $"❌ 오류 발생: {ex.Message}";

                MessageBox.Show(
                    $"전체 내보내기 중 오류가 발생했습니다:\n\n{ex.Message}",
                    "오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsExporting = false;
            }
        }

        /// <summary>
        /// Refining_ObjectID_Latest.xlsx 스타일의 서식이 적용된 XLSX 워크북을 내보내기
        /// 5개 시트: Pivot, Pipeline Summary, Class Distribution, Equipment Summary, PipeRun Detail
        /// </summary>
        private async Task ExportRefinedXlsxAsync()
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Filter = "Excel 파일|*.xlsx",
                    DefaultExt = "xlsx",
                    FileName = $"Refining_ObjectID_{DateTime.Now:yyyyMMdd_HHmmss}"
                };

                if (saveDialog.ShowDialog() != true) return;

                // UI 상태 초기화
                IsExporting = true;
                ExportProgressPercentage = 0;
                ExportStatusMessage = "Refined XLSX 내보내기 시작 중...";

                // 진행률 보고를 위한 Progress 인스턴스
                var progress = new Progress<(int percentage, string message)>(report =>
                {
                    ExportProgressPercentage = report.percentage;
                    ExportStatusMessage = report.message;
                });

                // UI 스레드에서 실행 (Navisworks API는 STA 스레드 필수)
                var exporter = new RefinedXlsxExporter();
                exporter.Export(saveDialog.FileName, progress);

                ExportStatusMessage = "Refined XLSX 내보내기 완료!";

                MessageBox.Show(
                    $"Refined XLSX가 성공적으로 저장되었습니다.\n\n{saveDialog.FileName}",
                    "내보내기 완료",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ExportStatusMessage = $"오류 발생: {ex.Message}";

                MessageBox.Show(
                    $"Refined XLSX 내보내기 중 오류가 발생했습니다:\n\n{ex.Message}",
                    "오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsExporting = false;
            }
        }

        /// <summary>
        /// 선택된 객체의 계층 구조를 CSV/JSON으로 내보내기
        /// </summary>
        private async Task ExportSelectionHierarchyAsync()
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Filter = "CSV 파일|*.csv|JSON (Flat)|*.json|JSON (Tree)|*.json",
                    DefaultExt = "csv",
                    FileName = $"Hierarchy_{DateTime.Now:yyyyMMdd_HHmmss}"
                };

                if (saveDialog.ShowDialog() != true) return;

                var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
                if (doc == null)
                {
                    MessageBox.Show("활성 문서가 없습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var selectedItems = doc.CurrentSelection.SelectedItems;
                if (selectedItems == null || selectedItems.Count == 0)
                {
                    MessageBox.Show("먼저 객체를 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                ExportStatusMessage = "계층 구조 데이터 추출 중...";

                // UI 스레드에서 Navisworks API 데이터 추출
                List<HierarchicalPropertyRecord> hierarchicalData = null;
                var extractor = new NavisworksDataExtractor();
                hierarchicalData = extractor.ExtractHierarchicalRecordsFromSelection(selectedItems);

                // 데이터 검증
                if (hierarchicalData == null || hierarchicalData.Count == 0)
                {
                    ExportStatusMessage = "";
                    MessageBox.Show(
                        "선택된 객체에서 추출할 수 있는 속성 데이터가 없습니다.\n\n" +
                        "가능한 원인:\n" +
                        "- 선택된 객체가 숨겨진 상태이거나 형상이 없습니다.\n" +
                        "- 선택된 객체에 속성이 없습니다.",
                        "데이터 없음",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // UI 스레드에서 AllHierarchicalProperties 업데이트
                AllHierarchicalProperties.Clear();
                foreach (var record in hierarchicalData)
                {
                    AllHierarchicalProperties.Add(record);
                }

                // FilteredHierarchicalProperties 동기화
                SyncFilteredProperties();

                // 파일로 저장
                await Task.Run(() =>
                {
                    var writer = new HierarchyFileWriter();
                    int filterIndex = saveDialog.FilterIndex;

                    if (filterIndex == 1) // CSV
                    {
                        writer.WriteToCsv(saveDialog.FileName, hierarchicalData);
                    }
                    else if (filterIndex == 2) // JSON Flat
                    {
                        writer.WriteToJsonFlat(saveDialog.FileName, hierarchicalData);
                    }
                    else // JSON Tree
                    {
                        writer.WriteToJsonTree(saveDialog.FileName, hierarchicalData);
                    }
                });

                ExportStatusMessage = "Export completed!";
                StatusMessage = $"Exported: {hierarchicalData.Count} items to file";

                MessageBox.Show(
                    "Hierarchy exported successfully.",
                    "Export Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ExportStatusMessage = $"❌ 오류: {ex.Message}";
                MessageBox.Show(
                    $"계층 구조 내보내기 중 오류가 발생했습니다:\n\n{ex.Message}",
                    "오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 현재 선택된 객체의 속성을 CSV로 내보내기 (Selection × Properties)
        /// </summary>
        private async Task ExportSelectionPropertiesAsync()
        {
            try
            {
                var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
                if (doc == null)
                {
                    MessageBox.Show("활성 문서가 없습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var selectedItems = doc.CurrentSelection.SelectedItems;
                if (selectedItems == null || selectedItems.Count == 0)
                {
                    MessageBox.Show("먼저 객체를 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var saveDialog = new SaveFileDialog
                {
                    Filter = "CSV 파일|*.csv",
                    DefaultExt = "csv",
                    FileName = $"SelectionProperties_{DateTime.Now:yyyyMMdd_HHmmss}"
                };

                if (saveDialog.ShowDialog() != true) return;

                IsExporting = true;
                ExportStatusMessage = "선택 객체 속성 추출 중...";

                // UI 스레드에서 Navisworks API 호출
                var extractor = new NavisworksDataExtractor();
                var properties = extractor.ExtractPropertiesFromSelection(selectedItems);

                if (properties == null || properties.Count == 0)
                {
                    ExportStatusMessage = "";
                    MessageBox.Show("선택된 객체에서 속성을 추출할 수 없습니다.", "데이터 없음", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 파일 저장
                await Task.Run(() =>
                {
                    var writer = new PropertyFileWriter();
                    writer.WriteFile(saveDialog.FileName, properties, FileType.CSV);
                });

                ExportStatusMessage = "✅ Selection Properties 내보내기 완료!";
                StatusMessage = $"Exported: {properties.Count} properties";

                MessageBox.Show(
                    $"선택 객체 속성이 저장되었습니다.\n\n속성 개수: {properties.Count}",
                    "내보내기 완료",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ExportStatusMessage = $"❌ 오류: {ex.Message}";
                MessageBox.Show(
                    $"Selection Properties 내보내기 중 오류:\n\n{ex.Message}",
                    "오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsExporting = false;
            }
        }

        /// <summary>
        /// 전체 모델의 계층 구조를 CSV/JSON으로 내보내기 (All × Hierarchy)
        /// </summary>
        private async Task ExportAllHierarchyAsync()
        {
            try
            {
                var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
                if (doc == null)
                {
                    MessageBox.Show("활성 문서가 없습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var saveDialog = new SaveFileDialog
                {
                    Filter = "CSV 파일|*.csv|JSON (Flat)|*.json|JSON (Tree)|*.json",
                    DefaultExt = "csv",
                    FileName = $"AllHierarchy_{DateTime.Now:yyyyMMdd_HHmmss}"
                };

                if (saveDialog.ShowDialog() != true) return;

                IsExporting = true;
                ExportStatusMessage = "전체 모델 계층 구조 추출 중...";

                // UI 스레드에서 전체 모델 계층 추출
                var extractor = new NavisworksDataExtractor();
                var hierarchicalData = extractor.ExtractAllHierarchicalRecords();

                if (hierarchicalData == null || hierarchicalData.Count == 0)
                {
                    ExportStatusMessage = "";
                    MessageBox.Show("모델에서 계층 데이터를 추출할 수 없습니다.", "데이터 없음", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 파일 저장
                await Task.Run(() =>
                {
                    var writer = new HierarchyFileWriter();
                    int filterIndex = saveDialog.FilterIndex;

                    if (filterIndex == 1) // CSV
                    {
                        writer.WriteToCsv(saveDialog.FileName, hierarchicalData);
                    }
                    else if (filterIndex == 2) // JSON Flat
                    {
                        writer.WriteToJsonFlat(saveDialog.FileName, hierarchicalData);
                    }
                    else // JSON Tree
                    {
                        writer.WriteToJsonTree(saveDialog.FileName, hierarchicalData);
                    }
                });

                ExportStatusMessage = "✅ All Hierarchy 내보내기 완료!";
                StatusMessage = $"Exported: {hierarchicalData.Count} items";

                MessageBox.Show(
                    $"전체 모델 계층 구조가 저장되었습니다.\n\n항목 개수: {hierarchicalData.Count}",
                    "내보내기 완료",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ExportStatusMessage = $"❌ 오류: {ex.Message}";
                MessageBox.Show(
                    $"All Hierarchy 내보내기 중 오류:\n\n{ex.Message}",
                    "오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsExporting = false;
            }
        }

        #endregion

        #region Geometry Export Methods (Phase 15)

        /// <summary>
        /// 전체 모델의 Geometry (BoundingBox) 데이터를 manifest.json으로 내보내기
        /// Phase 15: Geometry Export System
        /// </summary>
        private async Task ExportGeometryAsync()
        {
            try
            {
                var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
                if (doc == null)
                {
                    MessageBox.Show("활성 문서가 없습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 폴더 선택 다이얼로그
                using (var folderDialog = new FolderBrowser())
                {
                    folderDialog.Description = "Geometry Export 폴더 선택";
                    folderDialog.ShowNewFolderButton = true;

                    if (folderDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                        return;

                    var exportPath = folderDialog.SelectedPath;

                    IsExporting = true;
                    ExportProgressPercentage = 0;
                    ExportStatusMessage = "Geometry 추출 준비 중...";

                    // 서비스 인스턴스 생성
                    var extractor = new GeometryExtractor();
                    var writer = new GeometryFileWriter();

                    // 진행률 이벤트 연결
                    extractor.ProgressChanged += (s, p) =>
                    {
                        ExportProgressPercentage = (int)(p * 0.8); // 80%까지 추출
                        System.Windows.Application.Current?.Dispatcher?.Invoke(() => { });
                    };
                    extractor.StatusChanged += (s, msg) =>
                    {
                        ExportStatusMessage = msg;
                        System.Windows.Application.Current?.Dispatcher?.Invoke(() => { });
                    };

                    writer.ProgressChanged += (s, p) =>
                    {
                        ExportProgressPercentage = 80 + (int)(p * 0.2); // 80-100% 저장
                        System.Windows.Application.Current?.Dispatcher?.Invoke(() => { });
                    };
                    writer.StatusChanged += (s, msg) =>
                    {
                        ExportStatusMessage = msg;
                        System.Windows.Application.Current?.Dispatcher?.Invoke(() => { });
                    };

                    // UI 스레드에서 Navisworks API 호출 (BoundingBox 추출)
                    ExportStatusMessage = "BoundingBox 데이터 추출 중...";
                    var geometryRecords = extractor.ExtractFromDocument(doc);

                    if (geometryRecords == null || geometryRecords.Count == 0)
                    {
                        ExportStatusMessage = "";
                        MessageBox.Show("모델에서 Geometry 데이터를 추출할 수 없습니다.", "데이터 없음", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // 폴더 구조 생성
                    var structure = writer.CreateExportStructure(exportPath);
                    if (!structure.IsValid)
                    {
                        MessageBox.Show("Export 폴더 구조 생성에 실패했습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // 파일 저장 (백그라운드)
                    string manifestPath = null;
                    await Task.Run(() =>
                    {
                        // manifest.json 저장
                        manifestPath = writer.WriteManifest(geometryRecords, exportPath, doc.Title ?? "Navisworks Model");

                        // geometry.csv도 함께 저장 (대안 포맷)
                        writer.WriteCsv(geometryRecords, structure.CsvPath);
                    });

                    ExportProgressPercentage = 100;
                    ExportStatusMessage = "✅ Geometry Export 완료!";
                    StatusMessage = $"Geometry Exported: {geometryRecords.Count} objects";

                    MessageBox.Show(
                        $"Geometry Export 완료!\n\n" +
                        $"• 객체 수: {geometryRecords.Count:N0}\n" +
                        $"• manifest.json: {System.IO.Path.GetFileName(manifestPath)}\n" +
                        $"• geometry.csv: {System.IO.Path.GetFileName(structure.CsvPath)}\n\n" +
                        $"폴더: {exportPath}",
                        "Geometry Export 완료",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                ExportStatusMessage = $"❌ Geometry Export 오류: {ex.Message}";
                MessageBox.Show(
                    $"Geometry Export 중 오류가 발생했습니다:\n\n{ex.Message}",
                    "오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsExporting = false;
            }
        }

        /// <summary>
        /// 선택된 객체의 Geometry 데이터를 내보내기
        /// </summary>
        private async Task ExportSelectionGeometryAsync()
        {
            try
            {
                var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
                if (doc == null)
                {
                    MessageBox.Show("활성 문서가 없습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var selectedItems = doc.CurrentSelection.SelectedItems;
                if (selectedItems == null || selectedItems.Count == 0)
                {
                    MessageBox.Show("먼저 객체를 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 폴더 선택
                using (var folderDialog = new FolderBrowser())
                {
                    folderDialog.Description = "Selection Geometry Export 폴더 선택";
                    folderDialog.ShowNewFolderButton = true;

                    if (folderDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                        return;

                    var exportPath = folderDialog.SelectedPath;

                    IsExporting = true;
                    ExportProgressPercentage = 0;
                    ExportStatusMessage = "선택 객체 Geometry 추출 중...";

                    var extractor = new GeometryExtractor();
                    var writer = new GeometryFileWriter();

                    // 진행률 연결
                    extractor.ProgressChanged += (s, p) => ExportProgressPercentage = (int)(p * 0.8);
                    extractor.StatusChanged += (s, msg) => ExportStatusMessage = msg;
                    writer.ProgressChanged += (s, p) => ExportProgressPercentage = 80 + (int)(p * 0.2);

                    // UI 스레드에서 추출
                    var geometryRecords = extractor.ExtractFromSelection(selectedItems);

                    if (geometryRecords == null || geometryRecords.Count == 0)
                    {
                        ExportStatusMessage = "";
                        MessageBox.Show("선택된 객체에서 Geometry를 추출할 수 없습니다.", "데이터 없음", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // 저장
                    var structure = writer.CreateExportStructure(exportPath);
                    await Task.Run(() =>
                    {
                        writer.WriteManifest(geometryRecords, exportPath, "Selection Export");
                        writer.WriteCsv(geometryRecords, structure.CsvPath);
                    });

                    ExportProgressPercentage = 100;
                    ExportStatusMessage = "✅ Selection Geometry Export 완료!";

                    MessageBox.Show(
                        $"Selection Geometry Export 완료!\n\n객체 수: {geometryRecords.Count:N0}",
                        "Export 완료",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                ExportStatusMessage = $"❌ 오류: {ex.Message}";
                MessageBox.Show($"Selection Geometry Export 오류:\n\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsExporting = false;
            }
        }

        #endregion

        #region Unified CSV Export Methods (Phase 16)

        /// <summary>
        /// 전체 모델의 통합 CSV 내보내기 (Hierarchy + Geometry + Manifest)
        /// Phase 16: Unified CSV Export for Ontology Conversion
        /// </summary>
        private async Task ExportUnifiedCsvAsync()
        {
            try
            {
                var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
                if (doc == null)
                {
                    MessageBox.Show("활성 문서가 없습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var saveDialog = new SaveFileDialog
                {
                    Filter = "CSV 파일|*.csv",
                    DefaultExt = "csv",
                    FileName = $"UnifiedExport_{DateTime.Now:yyyyMMdd_HHmmss}"
                };

                if (saveDialog.ShowDialog() != true) return;

                IsExporting = true;
                ExportProgressPercentage = 0;
                ExportStatusMessage = "통합 CSV 내보내기 준비 중...";

                var exporter = new UnifiedCsvExporter();

                // 진행률 이벤트 연결
                exporter.ProgressChanged += (s, p) =>
                {
                    ExportProgressPercentage = p;
                    System.Windows.Application.Current?.Dispatcher?.Invoke(() => { });
                };
                exporter.StatusChanged += (s, msg) =>
                {
                    ExportStatusMessage = msg;
                    System.Windows.Application.Current?.Dispatcher?.Invoke(() => { });
                };

                // UI 스레드에서 Navisworks API 호출이 필요하므로 동기 실행
                int exportedCount = exporter.ExportFromDocument(saveDialog.FileName);

                ExportProgressPercentage = 100;
                ExportStatusMessage = "✅ 통합 CSV 내보내기 완료!";
                StatusMessage = $"Unified CSV Exported: {exportedCount} objects";

                MessageBox.Show(
                    $"통합 CSV 내보내기 완료!\n\n" +
                    $"• 객체 수: {exportedCount:N0}\n" +
                    $"• 파일: {System.IO.Path.GetFileName(saveDialog.FileName)}\n\n" +
                    $"이 파일을 bim-ontology 프로젝트에서 사용할 수 있습니다.",
                    "Unified CSV Export 완료",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ExportStatusMessage = $"❌ Unified CSV 오류: {ex.Message}";
                MessageBox.Show(
                    $"통합 CSV 내보내기 중 오류가 발생했습니다:\n\n{ex.Message}",
                    "오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsExporting = false;
            }
        }

        /// <summary>
        /// 선택된 객체의 통합 CSV 내보내기
        /// </summary>
        private async Task ExportSelectionUnifiedCsvAsync()
        {
            try
            {
                var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
                if (doc == null)
                {
                    MessageBox.Show("활성 문서가 없습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var selectedItems = doc.CurrentSelection.SelectedItems;
                if (selectedItems == null || selectedItems.Count == 0)
                {
                    MessageBox.Show("먼저 객체를 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var saveDialog = new SaveFileDialog
                {
                    Filter = "CSV 파일|*.csv",
                    DefaultExt = "csv",
                    FileName = $"UnifiedSelection_{DateTime.Now:yyyyMMdd_HHmmss}"
                };

                if (saveDialog.ShowDialog() != true) return;

                IsExporting = true;
                ExportProgressPercentage = 0;
                ExportStatusMessage = "선택 객체 통합 CSV 내보내기 중...";

                var exporter = new UnifiedCsvExporter();

                exporter.ProgressChanged += (s, p) => ExportProgressPercentage = p;
                exporter.StatusChanged += (s, msg) => ExportStatusMessage = msg;

                int exportedCount = exporter.ExportFromSelection(selectedItems, saveDialog.FileName);

                ExportProgressPercentage = 100;
                ExportStatusMessage = "✅ Selection Unified CSV 완료!";

                MessageBox.Show(
                    $"선택 객체 통합 CSV 내보내기 완료!\n\n객체 수: {exportedCount:N0}",
                    "Export 완료",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ExportStatusMessage = $"❌ 오류: {ex.Message}";
                MessageBox.Show($"Selection Unified CSV 오류:\n\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsExporting = false;
            }
        }

        #endregion

        #region Spatial Adjacency Export Methods (Phase 17)

        /// <summary>
        /// Phase 17: 전체 모델 Spatial Adjacency Export
        /// </summary>
        private async System.Threading.Tasks.Task ExportAdjacencyAsync()
        {
            try
            {
                var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
                if (doc == null)
                {
                    MessageBox.Show("Navisworks 문서가 열려있지 않습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var folderDialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "Spatial 분석 결과를 저장할 폴더를 선택하세요",
                    ShowNewFolderButton = true
                };

                if (folderDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

                string outputDir = System.IO.Path.Combine(folderDialog.SelectedPath,
                    string.Format("spatial_{0:yyyyMMdd_HHmmss}", DateTime.Now));

                IsExporting = true;
                ExportProgressPercentage = 0;
                ExportStatusMessage = "Geometry 추출 중...";

                var sw = System.Diagnostics.Stopwatch.StartNew();

                // 1) Geometry 추출
                var geoExtractor = new Services.Geometry.GeometryExtractor();
                geoExtractor.ProgressChanged += (s, p) => ExportProgressPercentage = p / 3;
                geoExtractor.StatusChanged += (s, msg) => ExportStatusMessage = msg;

                var geometries = geoExtractor.ExtractFromDocument(doc);

                // 2) Adjacency 검출
                ExportStatusMessage = string.Format("Adjacency 검출 중... ({0:N0}개 객체)", geometries.Count);
                var detector = new Services.Spatial.AdjacencyDetector();
                detector.ProgressChanged += (s, p) => ExportProgressPercentage = 33 + p / 3;
                detector.StatusChanged += (s, msg) => ExportStatusMessage = msg;

                var adjacencies = detector.Detect(geometries);

                // 3) Connected Components
                ExportStatusMessage = "연결 그룹 분석 중...";
                ExportProgressPercentage = 66;
                var componentFinder = new Services.Spatial.ConnectedComponentFinder();
                var groups = componentFinder.FindAndCompute(adjacencies, geometries);

                // 4) 파일 출력
                ExportStatusMessage = "파일 저장 중...";
                var writer = new Services.Spatial.SpatialRelationshipWriter();
                writer.StatusChanged += (s, msg) => ExportStatusMessage = msg;

                writer.WriteAdjacencyCsv(adjacencies, outputDir);
                writer.WriteGroupsCsv(groups, outputDir);
                writer.WriteTtl(adjacencies, groups, outputDir);

                sw.Stop();
                writer.WriteSummary(adjacencies, groups, outputDir, sw.Elapsed.TotalSeconds);

                ExportProgressPercentage = 100;
                ExportStatusMessage = "Spatial 분석 완료!";

                MessageBox.Show(
                    string.Format("Spatial 분석 완료!\n\n"
                        + "객체 수: {0:N0}\n"
                        + "인접 관계: {1:N0}\n"
                        + "연결 그룹: {2:N0}\n"
                        + "처리 시간: {3:F1}초\n\n"
                        + "저장 위치: {4}",
                        geometries.Count, adjacencies.Count, groups.Count,
                        sw.Elapsed.TotalSeconds, outputDir),
                    "Spatial Analysis",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ExportStatusMessage = string.Format("오류: {0}", ex.Message);
                MessageBox.Show(string.Format("Spatial 분석 오류:\n\n{0}", ex.Message), "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsExporting = false;
            }
        }

        /// <summary>
        /// Phase 17: 선택 객체 Spatial Adjacency Export
        /// </summary>
        private async System.Threading.Tasks.Task ExportSelectionAdjacencyAsync()
        {
            try
            {
                var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
                if (doc == null)
                {
                    MessageBox.Show("Navisworks 문서가 열려있지 않습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var selectedItems = doc.CurrentSelection.SelectedItems;
                if (selectedItems == null || selectedItems.Count == 0)
                {
                    MessageBox.Show("먼저 객체를 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var folderDialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "Spatial 분석 결과를 저장할 폴더를 선택하세요",
                    ShowNewFolderButton = true
                };

                if (folderDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

                string outputDir = System.IO.Path.Combine(folderDialog.SelectedPath,
                    string.Format("spatial_sel_{0:yyyyMMdd_HHmmss}", DateTime.Now));

                IsExporting = true;
                ExportProgressPercentage = 0;
                ExportStatusMessage = "선택 객체 Geometry 추출 중...";

                var sw = System.Diagnostics.Stopwatch.StartNew();

                // 1) 선택 객체 Geometry 추출
                var geoExtractor = new Services.Geometry.GeometryExtractor();
                geoExtractor.ProgressChanged += (s, p) => ExportProgressPercentage = p / 3;
                geoExtractor.StatusChanged += (s, msg) => ExportStatusMessage = msg;

                var geometries = geoExtractor.ExtractFromSelection(selectedItems);

                // 2) Adjacency 검출
                ExportStatusMessage = string.Format("Adjacency 검출 중... ({0:N0}개 객체)", geometries.Count);
                var detector = new Services.Spatial.AdjacencyDetector();
                detector.ProgressChanged += (s, p) => ExportProgressPercentage = 33 + p / 3;
                detector.StatusChanged += (s, msg) => ExportStatusMessage = msg;

                var adjacencies = detector.Detect(geometries);

                // 3) Connected Components
                ExportStatusMessage = "연결 그룹 분석 중...";
                ExportProgressPercentage = 66;
                var componentFinder = new Services.Spatial.ConnectedComponentFinder();
                var groups = componentFinder.FindAndCompute(adjacencies, geometries);

                // 4) 파일 출력
                ExportStatusMessage = "파일 저장 중...";
                var writer = new Services.Spatial.SpatialRelationshipWriter();
                writer.StatusChanged += (s, msg) => ExportStatusMessage = msg;

                writer.WriteAdjacencyCsv(adjacencies, outputDir);
                writer.WriteGroupsCsv(groups, outputDir);
                writer.WriteTtl(adjacencies, groups, outputDir);

                sw.Stop();
                writer.WriteSummary(adjacencies, groups, outputDir, sw.Elapsed.TotalSeconds);

                ExportProgressPercentage = 100;
                ExportStatusMessage = "Selection Spatial 분석 완료!";

                MessageBox.Show(
                    string.Format("Selection Spatial 분석 완료!\n\n"
                        + "객체 수: {0:N0}\n"
                        + "인접 관계: {1:N0}\n"
                        + "연결 그룹: {2:N0}\n"
                        + "처리 시간: {3:F1}초\n\n"
                        + "저장 위치: {4}",
                        geometries.Count, adjacencies.Count, groups.Count,
                        sw.Elapsed.TotalSeconds, outputDir),
                    "Spatial Analysis",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ExportStatusMessage = string.Format("오류: {0}", ex.Message);
                MessageBox.Show(string.Format("Selection Spatial 분석 오류:\n\n{0}", ex.Message), "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsExporting = false;
            }
        }

        #endregion

        #region Test Mesh Export (Phase 18: 단일 객체 GLB 검증)

        /// <summary>
        /// 선택된 단일 객체의 Mesh를 GLB로 추출하여 검증
        /// </summary>
        private async System.Threading.Tasks.Task ExportTestMeshAsync()
        {
            try
            {
                var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
                if (doc == null)
                {
                    MessageBox.Show("Navisworks 문서가 열려있지 않습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var selection = doc.CurrentSelection.SelectedItems;
                if (selection.Count == 0)
                {
                    MessageBox.Show("Mesh를 추출할 객체를 선택하세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var folderDialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "GLB 파일을 저장할 폴더를 선택하세요",
                    ShowNewFolderButton = true
                };
                if (folderDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

                IsExporting = true;
                ExportProgressPercentage = 0;
                ExportStatusMessage = "Mesh 추출 준비 중...";

                var geoExtractor = new Services.Geometry.GeometryExtractor();
                var meshExtractor = new Services.Geometry.MeshExtractor();
                meshExtractor.StatusChanged += (s, msg) => ExportStatusMessage = msg;

                // Resolve to leaf items: parent/group nodes include all descendant geometry
                var leafItems = new List<ModelItem>();
                foreach (var item in selection)
                {
                    if (item.Children != null && item.Children.Any())
                    {
                        // Parent node → collect leaf descendants
                        foreach (var desc in item.Descendants)
                        {
                            if (desc.Children == null || !desc.Children.Any())
                                leafItems.Add(desc);
                        }
                    }
                    else
                    {
                        leafItems.Add(item);
                    }
                }

                int total = leafItems.Count;
                int meshSuccess = 0;
                int processed = 0;
                string outputDir = folderDialog.SelectedPath;

                ExportStatusMessage = string.Format("Leaf 객체 {0}개 발견 (선택 {1}개에서)", total, selection.Count);

                foreach (var item in leafItems)
                {
                    var record = geoExtractor.ExtractBoundingBox(item);
                    if (record == null || record.ObjectId == System.Guid.Empty)
                    {
                        processed++;
                        continue;
                    }

                    ExportStatusMessage = string.Format("Mesh 추출 중: {0} ({1}/{2})",
                        item.DisplayName ?? "unknown", processed + 1, total);

                    var meshData = meshExtractor.ExtractMesh(item, record.ObjectId);

                    // Phase 21: BBox fallback when tessellation fails (with parent container detection)
                    bool isParent = item.Children != null && item.Children.Any();
                    if ((meshData == null || meshData.VertexCount == 0) && record.BBox != null && record.BBox.IsValid && !record.BBox.IsEmpty)
                    {
                        if (isParent)
                        {
                            System.Diagnostics.Debug.WriteLine(
                                string.Format("[TestMesh] Skipped container: {0} (has children)", item.DisplayName));
                        }
                        else
                        {
                            meshData = Services.Geometry.MeshExtractor.GenerateBoxMesh(record.BBox, record.ObjectId);
                            if (meshData != null)
                            {
                                meshData.Quality = "box_placeholder";
                                System.Diagnostics.Debug.WriteLine(
                                    string.Format("[TESS_FALLBACK] ObjectId={0} → Box placeholder. Name={1}",
                                        record.ObjectId.ToString("D"), item.DisplayName));
                            }
                        }
                    }

                    if (meshData != null && meshData.VertexCount > 0)
                    {
                        var glbPath = System.IO.Path.Combine(outputDir,
                            string.Format("{0}.glb", record.ObjectId.ToString("D")));
                        meshExtractor.SaveToGlb(meshData, glbPath);

                        var objPath = System.IO.Path.Combine(outputDir,
                            string.Format("{0}.obj", record.ObjectId.ToString("D")));
                        meshExtractor.SaveToObj(meshData, objPath);

                        meshSuccess++;
                    }

                    processed++;
                    ExportProgressPercentage = (int)(100.0 * processed / total);

                    // COM STA message pump — ContextSwitchDeadlock 방지
                    System.Windows.Forms.Application.DoEvents();
                }

                meshExtractor.Dispose();

                ExportProgressPercentage = 100;
                ExportStatusMessage = "Test Mesh Export 완료!";

                MessageBox.Show(
                    string.Format("Test Mesh Export 완료!\n\n"
                        + "선택 객체: {0}개\n"
                        + "Mesh 성공: {1}개\n"
                        + "저장 위치: {2}\n\n"
                        + "GLB 확인: https://gltf-viewer.donmccurdy.com/",
                        total, meshSuccess, outputDir),
                    "Test Mesh Export",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ExportStatusMessage = string.Format("오류: {0}", ex.Message);
                MessageBox.Show(string.Format("Test Mesh Export 오류:\n\n{0}", ex.Message), "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsExporting = false;
            }
        }

        #endregion

        #region Mesh Diagnostic Export (Phase 19: 계층별 mesh 진단)

        /// <summary>
        /// Phase 19: 선택된 객체의 전체 계층을 순회하며 mesh 진단 CSV 생성
        /// 각 노드별로 fragment 수, tessellation 성공/실패, transform 유무 등을 기록
        /// </summary>
        private async System.Threading.Tasks.Task ExportMeshDiagnosticAsync()
        {
            try
            {
                var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
                if (doc == null)
                {
                    MessageBox.Show("Navisworks 문서가 열려있지 않습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Phase 22: 선택 없으면 전체 문서 진단 (leaf 노드만)
                var selection = doc.CurrentSelection.SelectedItems;
                bool isFullDocument = selection.Count == 0;

                if (isFullDocument)
                {
                    var confirm = MessageBox.Show(
                        "선택된 객체가 없습니다.\n\n전체 문서의 leaf 노드를 진단하시겠습니까?\n(시간이 걸릴 수 있습니다)",
                        "Deep Mesh Diagnostic",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                    if (confirm != MessageBoxResult.Yes) return;
                }

                var saveDialog = new SaveFileDialog
                {
                    Filter = "CSV 파일|*.csv",
                    DefaultExt = "csv",
                    FileName = string.Format("MeshDiag_{0}_{1:yyyyMMdd_HHmmss}",
                        isFullDocument ? "FULL" : "SEL", DateTime.Now)
                };
                if (saveDialog.ShowDialog() != true) return;

                IsExporting = true;
                ExportProgressPercentage = 0;
                ExportStatusMessage = isFullDocument
                    ? "전체 문서 Deep Mesh 진단 시작..."
                    : "Deep Mesh 진단 시작...";

                // Phase 22: leaf 노드 수집 — 선택이 없으면 전체 문서에서 수집
                var leafNodes = new List<(ModelItem item, string path)>();
                int parentSkipped = 0;

                if (isFullDocument)
                {
                    // 전체 문서: RootItems에서 재귀 수집 (leaf만)
                    CollectLeafNodesForDiag(doc.Models.RootItems, "", leafNodes, ref parentSkipped);
                }
                else
                {
                    // 선택된 항목에서 재귀 수집 (leaf만)
                    foreach (var item in selection)
                    {
                        CollectLeafNodesForDiag(item, item.DisplayName ?? "Root", leafNodes, ref parentSkipped);
                    }
                }

                int total = leafNodes.Count;
                ExportStatusMessage = string.Format("Deep 진단: leaf {0:N0}개 분석 중... (parent {1:N0}개 skip)",
                    total, parentSkipped);

                var lines = new List<string>();
                lines.Add(Services.Geometry.MeshDiagnosticInfo.CsvHeader);

                int processed = 0;
                int okCount = 0, emptyTessCount = 0, noFragCount = 0, comFailCount = 0;
                int lineOnlyCount = 0, hiddenCount = 0;

                // Phase 22: 클래스별 통계 수집
                var classSummary = new Dictionary<string, int[]>();
                // [0]=total, [1]=OK, [2]=LINE_ONLY, [3]=EMPTY_TESS, [4]=NO_FRAG, [5]=totalLines, [6]=totalTriangles

                using (var meshExtractor = new Services.Geometry.MeshExtractor())
                {
                    foreach (var (item, path) in leafNodes)
                    {
                        // Phase 22: DeepDiagnoseMesh 사용 (전략별 Line/Point 카운트 포함)
                        var info = meshExtractor.DeepDiagnoseMesh(item);
                        lines.Add(info.ToCsvLine(0, path));

                        // 클래스별 통계 집계
                        string cls = info.ClassDisplayName ?? "(none)";
                        if (!classSummary.ContainsKey(cls))
                            classSummary[cls] = new int[7];
                        classSummary[cls][0]++;

                        switch (info.Status)
                        {
                            case "OK":
                                okCount++;
                                classSummary[cls][1]++;
                                break;
                            case "LINE_ONLY":
                                lineOnlyCount++;
                                classSummary[cls][2]++;
                                break;
                            case "EMPTY_TESS":
                            case "TESS_FAIL":
                                emptyTessCount++;
                                classSummary[cls][3]++;
                                break;
                            case "NO_FRAGMENTS":
                                noFragCount++;
                                classSummary[cls][4]++;
                                break;
                            case "COM_FAIL":
                                comFailCount++;
                                break;
                            case "HIDDEN":
                                hiddenCount++;
                                break;
                        }
                        classSummary[cls][5] += info.LineCount;
                        classSummary[cls][6] += info.TriangleCount;

                        processed++;
                        if (processed % 20 == 0 || processed == total)
                        {
                            ExportProgressPercentage = (int)(100.0 * processed / total);
                            ExportStatusMessage = string.Format(
                                "Deep 진단: {0:N0}/{1:N0} (OK:{2}, LineOnly:{3}, Empty:{4})",
                                processed, total, okCount, lineOnlyCount, emptyTessCount);

                            // COM STA pump — UI freeze 방지
                            if (processed % 10 == 0)
                                System.Windows.Forms.Application.DoEvents();
                        }
                    }
                }

                // Phase 22: 클래스별 요약 CSV 추가
                lines.Add("");
                lines.Add("=== CLASS SUMMARY (leaf nodes only) ===");
                lines.Add("ClassName,Total,OK,LINE_ONLY,EMPTY_TESS,NO_FRAG,TotalLines,TotalTriangles,FailRate%");
                // FailRate 기준 내림차순 정렬
                var sortedClasses = new List<KeyValuePair<string, int[]>>(classSummary);
                sortedClasses.Sort((a, b) =>
                {
                    int failA = a.Value[2] + a.Value[3] + a.Value[4]; // LINE_ONLY + EMPTY_TESS + NO_FRAG
                    int failB = b.Value[2] + b.Value[3] + b.Value[4];
                    return failB.CompareTo(failA); // 실패 많은 순
                });
                foreach (var kvp in sortedClasses)
                {
                    var v = kvp.Value;
                    int failCount = v[2] + v[3] + v[4];
                    double failRate = v[0] > 0 ? 100.0 * failCount / v[0] : 0;
                    lines.Add(string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8:F1}",
                        Services.Geometry.MeshDiagnosticInfo.EscapeCsvPublic(kvp.Key),
                        v[0], v[1], v[2], v[3], v[4], v[5], v[6], failRate));
                }

                // 파일 저장
                await System.Threading.Tasks.Task.Run(() =>
                {
                    System.IO.File.WriteAllLines(saveDialog.FileName, lines, System.Text.Encoding.UTF8);
                });

                ExportProgressPercentage = 100;
                ExportStatusMessage = "Deep Mesh 진단 완료!";

                // 실패율이 높은 상위 3개 클래스 표시
                string topFailClasses = "";
                int topCount = 0;
                foreach (var kvp in sortedClasses)
                {
                    int failCount = kvp.Value[2] + kvp.Value[3] + kvp.Value[4];
                    if (failCount == 0) continue;
                    double failRate = 100.0 * failCount / kvp.Value[0];
                    topFailClasses += string.Format("\n    {0}: {1}개 실패 ({2:F0}%) [Line:{3}]",
                        kvp.Key, failCount, failRate, kvp.Value[5]);
                    topCount++;
                    if (topCount >= 5) break;
                }

                MessageBox.Show(
                    string.Format("Deep Mesh Diagnostic 완료!\n\n"
                        + "범위: {0}\n"
                        + "분석 leaf 노드: {1:N0}개 (parent {2:N0}개 skip)\n"
                        + "── 결과 ──\n"
                        + "  OK (mesh 추출 가능): {3}개\n"
                        + "  LINE_ONLY (삼각형 없음, 선만): {4}개\n"
                        + "  EMPTY_TESS (tessellation 실패): {5}개\n"
                        + "  NO_FRAGMENTS (fragment 없음): {6}개\n"
                        + "  COM_FAIL (COM 변환 실패): {7}개\n"
                        + "  HIDDEN (숨김 객체): {8}개\n"
                        + "\n── 실패율 상위 클래스 ──{9}\n\n"
                        + "파일: {10}",
                        isFullDocument ? "전체 문서" : "선택 객체",
                        total, parentSkipped,
                        okCount, lineOnlyCount, emptyTessCount, noFragCount, comFailCount, hiddenCount,
                        string.IsNullOrEmpty(topFailClasses) ? "\n    (없음)" : topFailClasses,
                        System.IO.Path.GetFileName(saveDialog.FileName)),
                    "Deep Mesh Diagnostic",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ExportStatusMessage = string.Format("오류: {0}", ex.Message);
                MessageBox.Show(string.Format("Mesh Diagnostic 오류:\n\n{0}", ex.Message), "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsExporting = false;
            }
        }

        /// <summary>
        /// Phase 22: 전체 문서에서 leaf 노드만 재귀 수집 (RootItems 버전)
        /// </summary>
        private void CollectLeafNodesForDiag(
            ModelItemEnumerableCollection items, string parentPath,
            List<(ModelItem, string)> result, ref int parentSkipped)
        {
            foreach (var item in items)
            {
                if (item == null) continue;
                string currentPath = string.IsNullOrEmpty(parentPath)
                    ? (item.DisplayName ?? "?")
                    : string.Format("{0} > {1}", parentPath, item.DisplayName ?? "?");

                if (item.Children != null && item.Children.Any())
                {
                    parentSkipped++;
                    CollectLeafNodesForDiag(item.Children, currentPath, result, ref parentSkipped);
                }
                else
                {
                    result.Add((item, currentPath));
                }
            }
        }

        /// <summary>
        /// Phase 22: 단일 항목에서 leaf 노드만 재귀 수집 (선택 버전)
        /// </summary>
        private void CollectLeafNodesForDiag(
            ModelItem item, string parentPath,
            List<(ModelItem, string)> result, ref int parentSkipped)
        {
            if (item == null) return;
            string currentPath = parentPath;

            if (item.Children != null && item.Children.Any())
            {
                parentSkipped++;
                foreach (var child in item.Children)
                {
                    string childPath = string.Format("{0} > {1}", currentPath, child.DisplayName ?? "?");
                    CollectLeafNodesForDiag(child, childPath, result, ref parentSkipped);
                }
            }
            else
            {
                result.Add((item, currentPath));
            }
        }

        /// <summary>
        /// 재귀적으로 계층 구조 수집 (진단용 - 레거시)
        /// </summary>
        private void CollectHierarchyForDiag(ModelItem item, int depth, string parentPath, List<(ModelItem, int, string)> result)
        {
            if (item == null) return;

            string currentPath = parentPath;
            result.Add((item, depth, currentPath));

            if (item.Children != null)
            {
                foreach (var child in item.Children)
                {
                    string childPath = string.Format("{0} > {1}", currentPath, child.DisplayName ?? "?");
                    CollectHierarchyForDiag(child, depth + 1, childPath, result);
                }
            }
        }

        #endregion

        #region Full Pipeline Export (Unified CSV + Spatial + Mesh)

        /// <summary>
        /// 전체 파이프라인: AllProperties CSV + Unified CSV + Geometry + Mesh GLB + Spatial Adjacency 통합 출력
        /// Phase 18: Mesh GLB export 추가
        /// Stage 0: AllProperties CSV 생성 추가
        /// </summary>
        private async System.Threading.Tasks.Task ExportFullPipelineAsync()
        {
            try
            {
                var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
                if (doc == null)
                {
                    MessageBox.Show("Navisworks 문서가 열려있지 않습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var folderDialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "전체 Export 결과를 저장할 폴더를 선택하세요",
                    ShowNewFolderButton = true
                };

                if (folderDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

                string outputDir = System.IO.Path.Combine(folderDialog.SelectedPath,
                    string.Format("dxtnavis_export_{0:yyyyMMdd_HHmmss}", DateTime.Now));
                System.IO.Directory.CreateDirectory(outputDir);

                IsExporting = true;
                ExportProgressPercentage = 0;
                var sw = System.Diagnostics.Stopwatch.StartNew();

                // ──── Stage 0/6: AllProperties CSV ────
                ExportStatusMessage = "[0/6] AllProperties CSV 생성 중...";
                var allPropertiesPath = System.IO.Path.Combine(outputDir,
                    string.Format("AllProperties_{0:yyyyMMdd_HHmmss}.csv", DateTime.Now));
                var allPropExporter = new Services.FullModelExporterService();
                allPropExporter.ExportAllPropertiesToCsv(allPropertiesPath,
                    new Progress<(int percentage, string message)>(report =>
                    {
                        ExportProgressPercentage = report.percentage / 10;
                        ExportStatusMessage = "[0/6] " + report.message;
                    }));
                ExportProgressPercentage = 10;

                // ──── Stage 1/6: Hierarchy 데이터 추출 ────
                ExportStatusMessage = "[1/6] Hierarchy 데이터 추출 중...";

                var unifiedPath = System.IO.Path.Combine(outputDir, "unified.csv");
                var hierarchyExtractor = new NavisworksDataExtractor();
                var hierarchyRecords = hierarchyExtractor.ExtractAllHierarchicalRecords();

                ExportStatusMessage = string.Format("[1/6] Hierarchy 추출 완료: {0:N0}개 속성", hierarchyRecords.Count);
                ExportProgressPercentage = 25;

                // ──── Stage 2/6: Geometry BBox 추출 ────
                ExportStatusMessage = "[2/6] Geometry 추출 중...";

                var geoExtractor = new Services.Geometry.GeometryExtractor();
                geoExtractor.ProgressChanged += (s, p) => ExportProgressPercentage = 25 + (int)(p * 0.15);
                geoExtractor.StatusChanged += (s, msg) => ExportStatusMessage = "[2/6] " + msg;

                var geometries = geoExtractor.ExtractFromDocument(doc);

                ExportProgressPercentage = 40;

                // ──── Stage 3/6: Mesh GLB 추출 ────
                ExportStatusMessage = string.Format("[3/6] Mesh GLB 추출 중... ({0:N0}개 객체)", geometries.Count);

                var meshDir = System.IO.Path.Combine(outputDir, "mesh");
                System.IO.Directory.CreateDirectory(meshDir);

                int meshCount = 0;
                // Phase 21: modelItemMap을 using 블록 밖에서도 접근 가능하도록 선언
                var modelItemMap = geoExtractor.LastModelItemMap;

                // Phase 24+27: container 감지 — BBox 커버리지 기반 정교한 판별
                // 자식이 부모 BBox의 대부분을 커버하면 skip (진짜 container)
                // 자식이 일부만 커버하면 부모 자체 형상이 있으므로 추출 대상 유지
                var parentChildMap = geoExtractor.LastParentChildMap;
                var containerIds = new HashSet<Guid>();
                var partialContainerIds = new HashSet<Guid>();
                int partialContainerCount = 0;
                foreach (var kvp in parentChildMap)
                {
                    var parentId = kvp.Key;
                    var childIds = kvp.Value;

                    // 자식 중 geometry에 등록된 것이 있는지 확인
                    bool hasExportedChild = false;
                    foreach (var childId in childIds)
                    {
                        if (geometries.ContainsKey(childId))
                        {
                            hasExportedChild = true;
                            break;
                        }
                    }
                    if (!hasExportedChild) continue;

                    // 부모 BBox와 자식 BBox 커버리지 비교
                    if (!geometries.ContainsKey(parentId) || geometries[parentId].BBox == null)
                    {
                        containerIds.Add(parentId);
                        continue;
                    }

                    var parentBBox = geometries[parentId].BBox;
                    double parentVol = parentBBox.GetVolume();
                    if (parentVol < 1e-9)
                    {
                        containerIds.Add(parentId);
                        continue;
                    }

                    // 자식 BBox들의 합산 부피 계산
                    double childrenVolSum = 0;
                    foreach (var childId in childIds)
                    {
                        if (geometries.ContainsKey(childId) && geometries[childId].BBox != null)
                            childrenVolSum += geometries[childId].BBox.GetVolume();
                    }

                    double coverageRatio = childrenVolSum / parentVol;

                    // 커버리지 50% 이상이면 진짜 container → skip
                    // 50% 미만이면 부모 자체에 고유 형상 존재 → 추출 대상
                    if (coverageRatio >= 0.5)
                    {
                        containerIds.Add(parentId);
                    }
                    else
                    {
                        partialContainerCount++;
                        partialContainerIds.Add(parentId);
                        System.Diagnostics.Debug.WriteLine(
                            string.Format("[FullPipeline] Phase 27: Partial container kept: {0} ({1}) coverage={2:P1}",
                                parentId, geometries[parentId].DisplayName ?? "?", coverageRatio));
                    }
                }
                System.Diagnostics.Debug.WriteLine(
                    string.Format("[FullPipeline] Phase 24: {0} containers skip, {1} partial containers kept for mesh extraction",
                        containerIds.Count, partialContainerCount));

                // Phase 25: Tessellation 실패 진단 수집
                var tessFailures = new List<string>();
                tessFailures.Add("ObjectId,DisplayName,ClassDisplayName,FailureReason,Detail");
                int noGeometryCount = 0;
                int hiddenCount = 0;
                int noFragmentCount = 0;
                int allStratFailCount = 0;

                using (var meshExtractor = new Services.Geometry.MeshExtractor())
                {
                    meshExtractor.StatusChanged += (s, msg) => ExportStatusMessage = "[3/6] " + msg;

                    // Phase 25: leaf 아이템만 mesh 추출 (container는 Fragments()가 하위 포함 → 중복 방지)
                    var allItems = new List<KeyValuePair<Guid, ModelItem>>(modelItemMap);

                    int meshProcessed = 0;
                    int meshTotal = allItems.Count;

                    ExportGlbProgressText = $"0 / {meshTotal}";
                    ExportStatusMessage = string.Format("[3/6] Mesh GLB 추출 중... ({0:N0}개 객체)", meshTotal);

                    foreach (var kvp in allItems)
                    {
                        var objectId = kvp.Key;
                        var item = kvp.Value;

                        try
                        {
                            // Phase 25: Container skip — Fragments()가 하위 fragment를 집계하므로 중복 방지
                            if (containerIds.Contains(objectId))
                            {
                                if (geometries.ContainsKey(objectId))
                                    geometries[objectId].MeshQuality = "skipped_container";
                                meshProcessed++;
                                if (meshProcessed % 50 == 0 || meshProcessed == meshTotal)
                                    ExportProgressPercentage = 40 + (int)(20.0 * meshProcessed / meshTotal);
                                if (meshProcessed % 10 == 0)
                                    System.Windows.Forms.Application.DoEvents();
                                continue;
                            }

                            var meshData = meshExtractor.ExtractMesh(item, objectId);

                            if (meshData != null && meshData.VertexCount > 0)
                            {
                                var glbPath = System.IO.Path.Combine(meshDir,
                                    string.Format("{0}.glb", objectId.ToString("D")));
                                // Phase 30: SaveToGlb 반환값 체크 — 실패 시 HasMesh=true 방지
                                bool glbSaved = meshExtractor.SaveToGlb(meshData, glbPath);

                                if (glbSaved && geometries.ContainsKey(objectId))
                                {
                                    geometries[objectId].HasMesh = true;
                                    geometries[objectId].MeshUri = string.Format("mesh/{0}.glb", objectId.ToString("D"));
                                    geometries[objectId].MeshQuality = meshData.Quality ?? "full_mesh";
                                    geometries[objectId].VertexCount = meshData.VertexCount;
                                    geometries[objectId].TriangleCount = meshData.TriangleCount;
                                    meshCount++;
                                }
                                else if (!glbSaved)
                                {
                                    System.Diagnostics.Debug.WriteLine(
                                        string.Format("[FullPipeline] SaveToGlb failed for {0}", objectId));
                                }
                            }
                            else
                            {
                                // Phase 25: 실패 이유 수집
                                var reason = meshExtractor.LastFailureReason;
                                var detail = meshExtractor.LastFailureDetail ?? "";
                                string displayName = item.DisplayName ?? "(unnamed)";
                                string classDisp = item.ClassDisplayName ?? "";

                                tessFailures.Add(string.Format("{0},{1},{2},{3},{4}",
                                    objectId,
                                    displayName.Replace(",", ";"),
                                    classDisp.Replace(",", ";"),
                                    reason,
                                    detail.Replace(",", ";")));

                                // 실패 카테고리별 카운트
                                switch (reason)
                                {
                                    case Services.Geometry.TessFailureReason.NoGeometry:
                                        noGeometryCount++;
                                        if (geometries.ContainsKey(objectId))
                                            geometries[objectId].MeshQuality = "no_geometry";
                                        break;
                                    case Services.Geometry.TessFailureReason.Hidden:
                                        hiddenCount++;
                                        if (geometries.ContainsKey(objectId))
                                            geometries[objectId].MeshQuality = "hidden";
                                        break;
                                    case Services.Geometry.TessFailureReason.NoFragments:
                                        noFragmentCount++;
                                        break;
                                    case Services.Geometry.TessFailureReason.AllStrategiesFail:
                                        allStratFailCount++;
                                        break;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine(
                                string.Format("[FullPipeline] Mesh skip {0}: {1}", objectId, ex.Message));
                        }

                        meshProcessed++;
                        ExportGlbProgressText = $"{meshProcessed} / {meshTotal}";
                        if (meshProcessed % 50 == 0 || meshProcessed == meshTotal)
                        {
                            ExportProgressPercentage = 40 + (int)(20.0 * meshProcessed / meshTotal);
                        }

                        // COM STA message pump — ContextSwitchDeadlock 방지 (매 10회)
                        if (meshProcessed % 10 == 0)
                            System.Windows.Forms.Application.DoEvents();
                    }
                }
                ExportGlbProgressText = string.Empty;

                // Phase 25: tessellation_failures.csv 출력
                if (tessFailures.Count > 1) // 헤더 외에 실패 데이터가 있을 때만
                {
                    var failureCsvPath = System.IO.Path.Combine(outputDir, "tessellation_failures.csv");
                    System.IO.File.WriteAllLines(failureCsvPath, tessFailures, System.Text.Encoding.UTF8);
                    System.Diagnostics.Debug.WriteLine(
                        string.Format("[FullPipeline] Phase 25 Tess Failures: NoGeometry={0}, Hidden={1}, NoFragments={2}, AllStratFail={3}, Total={4}",
                            noGeometryCount, hiddenCount, noFragmentCount, allStratFailCount, tessFailures.Count - 1));
                }

                // ──── Phase 26+27: FBX Fallback — gap_supplemented + no_geometry(partial container) 실제 geometry 추출 ────
                int fbxFallbackCount = 0;
                bool fbxAvailable = Services.Geometry.FbxExportService.IsAvailable();

                System.Diagnostics.Debug.WriteLine(
                    string.Format("[FullPipeline] Phase 26: FBX plugin available = {0}", fbxAvailable));

                if (fbxAvailable)
                {
                    // gap_supplemented + no_geometry(partial container) → FBX로 실제 형상 추출
                    var fbxTargetItems = new List<ModelItem>();
                    var fbxTargetIds = new List<Guid>();
                    int gapCount = 0;
                    int partialNoGeoCount = 0;

                    foreach (var kvp in geometries)
                    {
                        if (!modelItemMap.ContainsKey(kvp.Key)) continue;

                        if (kvp.Value.MeshQuality == "gap_supplemented")
                        {
                            fbxTargetItems.Add(modelItemMap[kvp.Key]);
                            fbxTargetIds.Add(kvp.Key);
                            gapCount++;
                        }
                        else if (kvp.Value.MeshQuality == "no_geometry" && partialContainerIds.Contains(kvp.Key))
                        {
                            // Phase 27: partial container — COM API tessellation 실패했지만
                            // 부모 노드에 고유 형상이 있으므로 FBX 내부 tessellator로 추출 시도
                            fbxTargetItems.Add(modelItemMap[kvp.Key]);
                            fbxTargetIds.Add(kvp.Key);
                            partialNoGeoCount++;
                        }
                    }

                    System.Diagnostics.Debug.WriteLine(
                        string.Format("[FullPipeline] Phase 26+27: FBX targets = {0} (gap={1}, partial_container_no_geo={2})",
                            fbxTargetItems.Count, gapCount, partialNoGeoCount));

                    if (fbxTargetItems.Count > 0)
                    {
                        ExportStatusMessage = string.Format("[3/6] FBX fallback: {0}개 객체 export 중 (gap={1}, partial={2})...",
                            fbxTargetItems.Count, gapCount, partialNoGeoCount);

                        var fbxService = new Services.Geometry.FbxExportService();
                        fbxService.StatusChanged += (s, msg) => ExportStatusMessage = "[3/6] FBX: " + msg;

                        var fbxPath = System.IO.Path.Combine(outputDir, "gap_fallback.fbx");
                        bool fbxSuccess = fbxService.ExportTargetItemsAsFbx(fbxTargetItems, fbxPath);

                        if (fbxSuccess && System.IO.File.Exists(fbxPath))
                        {
                            fbxFallbackCount = fbxTargetItems.Count;
                            foreach (var objectId in fbxTargetIds)
                            {
                                if (geometries.ContainsKey(objectId))
                                    geometries[objectId].MeshQuality = "fbx_supplemented";
                            }
                            System.Diagnostics.Debug.WriteLine(
                                string.Format("[FullPipeline] Phase 26+27: FBX fallback exported {0} objects → {1}",
                                    fbxFallbackCount, fbxPath));
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("[FullPipeline] Phase 26+27: FBX fallback failed or not available");
                        }
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[FullPipeline] Phase 26: FBX plugin not available, skipping FBX fallback");
                }

                // Phase 25: BBox Fallback — 진짜 tessellation 실패 객체만 box_placeholder
                // no_geometry, hidden, skipped_container는 box 생성 불필요
                int fallbackCount = 0;
                foreach (var kvp in geometries)
                {
                    if (!kvp.Value.HasMesh && kvp.Value.BBox != null && kvp.Value.BBox.IsValid && !kvp.Value.BBox.IsEmpty)
                    {
                        // Container는 이미 mesh 루프에서 skipped_container 처리됨
                        if (containerIds.Contains(kvp.Key))
                            continue;

                        // Phase 25: no_geometry / hidden 객체는 Navisworks에서도 안 보임 → box 불필요
                        string quality = kvp.Value.MeshQuality;
                        if (quality == "no_geometry" || quality == "hidden")
                            continue;

                        // 진짜 tessellation 실패: box fallback 생성
                        var boxMesh = Services.Geometry.MeshExtractor.GenerateBoxMesh(kvp.Value.BBox, kvp.Key);
                        if (boxMesh != null)
                        {
                            var glbPath = System.IO.Path.Combine(meshDir,
                                string.Format("{0}.glb", kvp.Key.ToString("D")));
                            // Phase 30: SaveToGlb 반환값 체크
                            bool boxSaved;
                            using (var fallbackWriter = new Services.Geometry.MeshExtractor())
                            {
                                boxSaved = fallbackWriter.SaveToGlb(boxMesh, glbPath);
                            }
                            if (boxSaved)
                            {
                                kvp.Value.HasMesh = true;
                                kvp.Value.MeshUri = string.Format("mesh/{0}.glb", kvp.Key.ToString("D"));
                                kvp.Value.MeshQuality = "box_placeholder";
                                kvp.Value.VertexCount = boxMesh.VertexCount;
                                kvp.Value.TriangleCount = boxMesh.TriangleCount;
                                fallbackCount++;
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine(
                                    string.Format("[FullPipeline] Box GLB save failed for {0}", kvp.Key));
                            }
                        }
                    }
                    else if (kvp.Value.HasMesh && string.IsNullOrEmpty(kvp.Value.MeshQuality))
                    {
                        kvp.Value.MeshQuality = "full_mesh";
                    }
                }
                System.Diagnostics.Debug.WriteLine(
                    string.Format("[FullPipeline] Phase 26+27: containers skipped={0}, partial containers={1}, fbx fallback={2}, box fallback={3}, total mesh={4}",
                        containerIds.Count, partialContainerCount, fbxFallbackCount, fallbackCount, meshCount));
                if (fallbackCount > 0)
                    meshCount += fallbackCount;

                ExportProgressPercentage = 60;

                // Unified CSV (Stage 3 mesh 정보 반영된 geometry로 병합 → HasMesh/MeshUri 정확)
                var unifiedExporter = new UnifiedCsvExporter();
                int unifiedCount = unifiedExporter.ExportFromData(hierarchyRecords, geometries, unifiedPath);

                // Geometry CSV + Manifest (mesh 정보 반영된 상태)
                var geoWriter = new Services.Geometry.GeometryFileWriter();
                geoWriter.WriteCsv(geometries, System.IO.Path.Combine(outputDir, "geometry.csv"));
                geoWriter.WriteManifest(geometries, outputDir);

                // ──── Stage 4/6: Adjacency 검출 (leaf 노드만 대상) ────
                // Phase 28: container 노드 제외 — BBox가 자식 전체를 포함하므로
                // spatial hash grid에서 너무 많은 쌍을 생성하여 leaf-leaf 인접 누락 유발
                var leafGeometries = new Dictionary<Guid, Models.Geometry.GeometryRecord>();
                foreach (var kvp in geometries)
                {
                    // container(skipped_container)와 hidden은 adjacency 대상에서 제외
                    string q = kvp.Value.MeshQuality;
                    if (q == "skipped_container" || q == "hidden")
                        continue;
                    leafGeometries.Add(kvp.Key, kvp.Value);
                }
                System.Diagnostics.Debug.WriteLine(
                    string.Format("[FullPipeline] Phase 28: Adjacency leaf filter: {0} total → {1} leaf nodes (skipped {2} containers/hidden)",
                        geometries.Count, leafGeometries.Count, geometries.Count - leafGeometries.Count));

                ExportStatusMessage = string.Format("[4/6] Adjacency 검출 중... ({0:N0}개 leaf 객체)", leafGeometries.Count);

                var detector = new Services.Spatial.AdjacencyDetector();
                detector.ProgressChanged += (s, p) => ExportProgressPercentage = 60 + p / 5;
                detector.StatusChanged += (s, msg) => ExportStatusMessage = "[4/6] " + msg;

                var adjacencies = detector.Detect(leafGeometries);

                // Connected Components
                var componentFinder = new Services.Spatial.ConnectedComponentFinder();
                var groups = componentFinder.FindAndCompute(adjacencies, geometries);
                ExportProgressPercentage = 80;

                // ──── Stage 5/6: 파일 출력 ────
                ExportStatusMessage = "[5/6] 파일 저장 중...";

                var spatialWriter = new Services.Spatial.SpatialRelationshipWriter();
                spatialWriter.StatusChanged += (s, msg) => ExportStatusMessage = "[5/6] " + msg;

                spatialWriter.WriteAdjacencyCsv(adjacencies, outputDir);
                spatialWriter.WriteGroupsCsv(groups, outputDir);
                spatialWriter.WriteTtl(adjacencies, groups, outputDir);

                // ──── Phase 29: Object Validation CSV ────
                ExportStatusMessage = "[5/6] 객체 검증 중...";
                var validationService = new Services.Validation.ObjectValidationService();
                validationService.ProgressChanged += (s, p) => ExportProgressPercentage = 85 + p / 10;
                validationService.StatusChanged += (s, msg) => ExportStatusMessage = "[5/6] " + msg;

                var validationRecords = validationService.Validate(
                    geometries, modelItemMap,
                    containerIds, partialContainerIds,
                    adjacencies, groups, parentChildMap,
                    meshDir);

                validationService.WriteCsv(validationRecords, outputDir);
                ExportProgressPercentage = 95;

                // Phase 29+31: Verdict 집계
                int verdictFailCount = 0;
                int verdictWarnCount = 0;
                int verdictGlbMissing = 0;
                int verdictGlbEmpty = 0;
                int verdictFbxBatchOnly = 0;
                foreach (var vr in validationRecords)
                {
                    if (vr.Verdict == "FAIL_NO_EXTRACT") verdictFailCount++;
                    if (vr.Verdict == "WARN_BOX") verdictWarnCount++;
                    if (vr.Verdict == "FAIL_GLB_MISSING") verdictGlbMissing++;
                    if (vr.Verdict == "FAIL_GLB_EMPTY") verdictGlbEmpty++;
                    if (vr.Verdict == "WARN_FBX_BATCH_ONLY") verdictFbxBatchOnly++;
                }

                sw.Stop();
                spatialWriter.WriteSummary(adjacencies, groups, outputDir, sw.Elapsed.TotalSeconds);

                ExportProgressPercentage = 100;
                ExportStatusMessage = "Full Pipeline Export 완료!";

                MessageBox.Show(
                    string.Format("Full Pipeline Export 완료!\n\n"
                        + "── Unified CSV ──\n"
                        + "  객체 수: {0:N0}\n\n"
                        + "── Geometry ──\n"
                        + "  BBox 추출: {1:N0}개\n\n"
                        + "── Mesh GLB ──\n"
                        + "  GLB 생성: {2:N0}개\n"
                        + "  Container Skip: {7:N0}개\n"
                        + "  Partial Container: {14:N0}개\n"
                        + "  FBX Fallback: {13:N0}개\n"
                        + "  Box Fallback: {8:N0}개\n\n"
                        + "── Tess 실패 진단 ──\n"
                        + "  NoGeometry: {9:N0}개\n"
                        + "  Hidden: {10:N0}개\n"
                        + "  NoFragments: {11:N0}개\n"
                        + "  AllStratFail: {12:N0}개\n\n"
                        + "── Spatial ──\n"
                        + "  인접 관계: {3:N0}\n"
                        + "  연결 그룹: {4:N0}\n\n"
                        + "── Phase 31 검증 ──\n"
                        + "  FAIL_NO_EXTRACT: {15:N0}개\n"
                        + "  FAIL_GLB_MISSING: {17:N0}개\n"
                        + "  FAIL_GLB_EMPTY: {18:N0}개\n"
                        + "  WARN_BOX: {16:N0}개\n"
                        + "  WARN_FBX_BATCH_ONLY: {19:N0}개\n\n"
                        + "처리 시간: {5:F1}초\n"
                        + "저장 위치: {6}",
                        unifiedCount, geometries.Count, meshCount,
                        adjacencies.Count, groups.Count,
                        sw.Elapsed.TotalSeconds, outputDir, containerIds.Count, fallbackCount,
                        noGeometryCount, hiddenCount, noFragmentCount, allStratFailCount,
                        fbxFallbackCount, partialContainerCount, verdictFailCount, verdictWarnCount,
                        verdictGlbMissing, verdictGlbEmpty, verdictFbxBatchOnly),
                    "Full Pipeline Export",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ExportStatusMessage = string.Format("오류: {0}", ex.Message);
                MessageBox.Show(string.Format("Full Pipeline Export 오류:\n\n{0}", ex.Message), "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsExporting = false;
            }
        }

        #endregion
    }
}

