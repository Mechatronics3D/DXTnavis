<div align="center">

# DXTnavis

**Navisworks 2025 BIM Data Extraction & 4D Automation Plugin**

[![Version](https://img.shields.io/badge/Version-1.7.0-blue?style=flat-square)]()
[![Navisworks](https://img.shields.io/badge/Navisworks-2025-FF6D00?style=flat-square&logo=autodesk&logoColor=white)](https://www.autodesk.com/products/navisworks)
[![.NET](https://img.shields.io/badge/.NET_Framework-4.8-512BD4?style=flat-square&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![WPF](https://img.shields.io/badge/WPF-MVVM-0078D4?style=flat-square&logo=windows&logoColor=white)]()
[![GLB](https://img.shields.io/badge/glTF_2.0-GLB_Export-00B140?style=flat-square)]()
[![RDF](https://img.shields.io/badge/RDF-Turtle-9B59B6?style=flat-square)]()
[![Platform](https://img.shields.io/badge/Platform-x64-green?style=flat-square)]()

<br/>

*BIM 모델에서 속성, 기하정보, 3D 메시를 추출하고 4D 시뮬레이션을 자동화하는 Navisworks 플러그인*
*SP3D Pipeline 스케줄 자동 생성 지원*

[Impact](#-impact) | [Features](#-features) | [Architecture](#-architecture) | [Quick Start](#-quick-start) | [Changelog](CHANGELOG.md)

---

### Plugin Interface

![DXTnavis Main Page](snapshots/dxtnavis_main_page.png)

</div>

---

## Impact

<table>
<tr>
<th width="50%">Before (Manual)</th>
<th width="50%">After (DXTnavis)</th>
</tr>
<tr>
<td>

**BIM Property Export** - 4+ hours
- Navisworks에서 수동 검색/복사
- Excel에 수동 붙여넣기
- 445K 속성 필터링 불가

**4D Simulation Setup** - 2+ days
- CSV 수동 매핑
- Selection Set 수동 생성
- TimeLiner Task 수동 연결

**Pipeline 4D Schedule** - 1+ week
- Pipeline/PipeRun 수동 분류
- 객체별 시간 수동 계산
- 146 Pipeline × 334 PipeRun 수동 매핑

**3D Geometry Extraction** - Not possible
- NWD에서 메시 추출 도구 없음
- 좌표 변환 수동 계산
- 외부 뷰어 연동 불가

</td>
<td>

**BIM Property Export** - 5 minutes
- 원클릭 CSV Export (Raw + Refined)
- Level/Category/Path 필터링
- 445K+ 속성 실시간 처리

**4D Simulation Setup** - 10 minutes
- CSV → TimeLiner 자동 파이프라인
- SyncID 기반 자동 매칭
- 원클릭 Selection Set + Task 생성

**Pipeline 4D Schedule** - 5 minutes
- AllProperties CSV 자동 파싱
- Pipeline/PipeRun 자동 그룹핑
- 시간 자동 매핑 + TimeLiner 생성

**3D Geometry Extraction** - 15 minutes
- GLB 메시 자동 추출 (glTF 2.0)
- LCS→WCS 좌표 자동 변환
- BBox + Centroid + RDF 출력

</td>
</tr>
</table>

```
Performance Summary
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Property Export    ████████████████████░  4h → 5min    (98% ↓)
4D Setup           ████████████████████░  2d → 10min   (99% ↓)
Select All         ████████████████████░  445K → 5K    (99% ↓)
Geometry Export    ████████████████████░  N/A → 15min  (NEW)
Mesh Extract       ████████████████████░  N/A → 1-click(NEW)
Pipeline 4D        ████████████████████░  1wk → 5min   (99% ↓)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

---

## Features

### Scenario 1: BIM Data Management

> *"445K+ 속성을 가진 대규모 BIM 모델에서 원하는 데이터를 빠르게 찾고 내보내기"*

<table>
<tr>
<td align="center" width="25%">
<h3>🌳</h3>
<b>Hierarchy Navigation</b><br/>
<sub>Level-based expand/collapse<br/>L0~L10, 색상 배지, 노드 아이콘</sub>
</td>
<td align="center" width="25%">
<h3>🔍</h3>
<b>Property Viewer & Search</b><br/>
<sub>Category → Property → Value<br/>이름, 속성, SysPath 검색</sub>
</td>
<td align="center" width="25%">
<h3>📊</h3>
<b>Object Grouping</b><br/>
<sub>445K → ~5K 그룹 최적화<br/>체크박스 필터, Expander UI</sub>
</td>
<td align="center" width="25%">
<h3>📤</h3>
<b>CSV Import & Export</b><br/>
<sub>Raw + Refined 동시 저장<br/>UTF-8/EUC-KR 자동 감지</sub>
</td>
</tr>
</table>

### Scenario 2: 4D Construction Simulation

> *"스케줄 CSV에서 Navisworks TimeLiner 4D 시뮬레이션까지 원클릭 자동화"*

```
CSV File ──→ Schedule Parser ──→ Object Matcher ──→ Property Write ──→ Selection Set ──→ TimeLiner Task
 (한영매핑)    (SyncID 추출)     (자동 매칭)       (ComAPI)          (.NET API)        (.NET API)
```

<table>
<tr>
<td align="center" width="33%">
<h3>🎬</h3>
<b>AWP 4D Automation</b><br/>
<sub>6-Step Pipeline<br/>SyncID 매칭, Dry Run 검증</sub>
</td>
<td align="center" width="33%">
<h3>📅</h3>
<b>Schedule Builder</b><br/>
<sub>선택 객체 → Schedule CSV<br/>ParentSet 전략, 미리보기</sub>
</td>
<td align="center" width="33%">
<h3>⚡</h3>
<b>Direct TimeLiner</b><br/>
<sub>CSV 없이 1클릭 연결<br/>7단계 → 3단계 (57% 단축)</sub>
</td>
</tr>
</table>

### Scenario 3: Pipeline 4D Schedule Automation

> *"SP3D Pipeline 프로젝트에서 외부 스케줄 없이 TimeLiner 4D 시뮬레이션 자동 생성"*

```
AllProperties CSV ──→ Pipeline/PipeRun 추출 ──→ 그룹핑 ──→ Time Mapping ──→ TimeLiner
 (DisplayString:       (자동 컬럼 감지)      (146 Pipeline  (Hybrid 전략)    Task + Set
  접두사 제거)                                 334 PipeRun)                   자동 생성
```

<table>
<tr>
<td align="center" width="25%">
<h3>📂</h3>
<b>CSV Auto-Parse</b><br/>
<sub>Pipeline/PipeRun 자동 감지<br/>DisplayString: 접두사 제거</sub>
</td>
<td align="center" width="25%">
<h3>🔗</h3>
<b>Hierarchical Grouping</b><br/>
<sub>Pipeline → PipeRun → Objects<br/>146 Pipelines, 334 PipeRuns</sub>
</td>
<td align="center" width="25%">
<h3>⏱️</h3>
<b>Time Mapping</b><br/>
<sub>Hybrid: base + per-object<br/>Spatial ordering 지원</sub>
</td>
<td align="center" width="25%">
<h3>🚀</h3>
<b>1-Click Execute</b><br/>
<sub>Selection Set + TimeLiner<br/>DryRun, CSV Export</sub>
</td>
</tr>
</table>

### Scenario 4: 3D Geometry & Mesh Export

> *"Navisworks NWD에서 glTF 2.0 GLB 메시를 추출하여 웹 3D 뷰어와 연동"*

```
ModelItem ──→ COM Fragment ──→ GenerateSimplePrimitives() ──→ LCS→WCS Transform ──→ GLB File
              (Late-binding)    (Vertex/Triangle Callback)     (4x4 Matrix)          (glTF 2.0)
```

<table>
<tr>
<td align="center" width="25%">
<h3>🧊</h3>
<b>3D Mesh Export</b><br/>
<sub>COM API GLB 추출<br/>Normal, BBox, Fallback</sub>
</td>
<td align="center" width="25%">
<h3>🔲</h3>
<b>BBox Geometry</b><br/>
<sub>World 좌표계 AABB<br/>Centroid 자동 계산</sub>
</td>
<td align="center" width="25%">
<h3>🔗</h3>
<b>Spatial Analysis</b><br/>
<sub>인접성 검출, Union-Find<br/>RDF/TTL 트리플 생성</sub>
</td>
<td align="center" width="25%">
<h3>📋</h3>
<b>Unified CSV</b><br/>
<sub>22-column 통합 스키마<br/>1 row = 1 object</sub>
</td>
</tr>
</table>

### Scenario 5: 3D Viewport Control

<table>
<tr>
<td align="center" width="20%"><b>Select in 3D</b><br/><sub>필터 → 3D 선택</sub></td>
<td align="center" width="20%"><b>Show Only</b><br/><sub>필터 객체만 표시</sub></td>
<td align="center" width="20%"><b>Show All</b><br/><sub>전체 복원</sub></td>
<td align="center" width="20%"><b>Zoom</b><br/><sub>선택 객체 이동</sub></td>
<td align="center" width="20%"><b>Reset Home</b><br/><sub>초기 뷰포인트</sub></td>
</tr>
</table>

---

## Architecture

### Full Pipeline (5-Stage Export)

```
┌────────────────────────────────────────────────────────────────┐
│                    Full Pipeline Export                         │
├─────────┬─────────┬─────────┬──────────┬──────────────────────┤
│ Stage 1 │ Stage 2 │ Stage 3 │ Stage 4  │ Stage 5              │
│Hierarchy│Geometry │  Mesh   │ Spatial  │ Unified CSV          │
│  CSV    │BBox+CSV │  GLB    │Adjacency │ 22-col Schema        │
│         │manifest │per-obj  │ RDF/TTL  │ 1row=1obj            │
└─────────┴─────────┴─────────┴──────────┴──────────────────────┘
                         │
                         ▼
              export_YYYYMMDD_HHMMSS/
              ├── hierarchy.csv
              ├── geometry.csv
              ├── manifest.json
              ├── unified.csv
              ├── adjacency.csv
              ├── connected_groups.csv
              ├── spatial_relationships.ttl
              └── mesh/
                  ├── {uuid}.glb
                  └── ...
```

### Hybrid API Strategy

Navisworks는 두 가지 API를 제공합니다. DXTnavis는 용도에 맞게 조합합니다.

| Feature | API | Reason |
|---------|-----|--------|
| Property Read | .NET API | 표준 데이터 접근 |
| **Property Write** | **ComAPI** | .NET API는 Read-Only |
| Selection Set | .NET API | AddCopy/InsertCopy 메서드 |
| TimeLiner Task | .NET API | TasksCopyFrom 메서드 |
| 3D Viewport | .NET API | Selection, Visibility |
| **Mesh Extract** | **ComAPI** | GenerateSimplePrimitives() |
| **ViewPoint Save** | **ComAPI** | .NET API 미지원 기능 |

### MVVM Architecture

```
┌──────────────────┐     ┌─────────────────────────┐     ┌─────────────┐
│   View (XAML)    │ ←──→│  ViewModel (Partial)     │ ←──→│   Services  │
│ DXwindow.xaml    │     │  Core / Filter / Search  │     │  Extractor  │
│ TabControl       │     │  Selection / Snapshot    │     │  Matcher    │
│ TreeView         │     │  Tree / Export           │     │  Writer     │
│ DataGrid         │     │  AWP4D / Schedule        │     │  Validator  │
│                  │     │  Pipeline4D              │     │  PipelineSB │
└──────────────────┘     └─────────────────────────┘     └─────────────┘
                                    ↕
                          ┌─────────────────┐
                          │  Models          │
                          │  ObjectGroup     │
                          │  GeometryRecord  │
                          │  BBox3D / Point3D│
                          │  ScheduleData    │
                          │  PipelineSchedule│
                          └─────────────────┘
```

---

## Technical Decisions

### ComAPI Reverse Engineering

> Navisworks .NET API는 Property를 Read-Only로만 제공합니다.
> 4D 자동화를 위해 Custom Property 기입이 필수였으며, ComAPI `SetUserDefined()`를 발견하여 해결했습니다.

```csharp
// .NET API: Read-Only (Write 불가)
modelItem.PropertyCategories  // ← 읽기만 가능

// ComAPI: Write 가능 (DXTnavis가 사용하는 방식)
InwOpState10 comState = ComApiBridge.State;
InwOaPath comPath = ComApiBridge.ToInwOaPath(modelItem);
InwGUIPropertyNode2 propNode = (InwGUIPropertyNode2)comState.GetGUIPropertyNode(comPath, true);
propNode.SetUserDefined(0, "AWP Schedule", "AWP_Internal", propVec);
```

### COM Late-Binding for 3D Mesh

> `GetLocalToWorldMatrix()`는 `InwLTransform3f` COM 객체를 반환하는데,
> C# `as Array` 캐스트가 항상 실패합니다. COM Interop Late-binding으로 해결했습니다.

```csharp
// ❌ 실패: COM 객체는 Array로 직접 캐스트 불가
Array matrix = transformObj as Array;  // 항상 null

// ✅ 성공: Late-binding으로 Matrix 속성 접근
var matrixData = transformObj.GetType().InvokeMember(
    "Matrix",
    System.Reflection.BindingFlags.GetProperty,
    null, transformObj, null);
```

이 패턴으로 fragment별 LCS→WCS 4x4 변환 행렬을 추출하여,
메시 정점을 Local Coordinate Space에서 World Coordinate Space로 정확하게 변환합니다.

### Synthetic ID for Hierarchy Preservation

> `InstanceGuid`가 Empty인 경우(CATIA, PDMS 등)에도 계층 구조를 보존하기 위해
> MD5 해시 기반 결정적 GUID 생성 시스템을 구현했습니다.

```
Fallback 순서: InstanceGuid → Item GUID → Authoring ID → Hierarchy Path Hash
지원 ID: Revit Element ID, AutoCAD Handle, IFC GlobalId
```

---

## Quick Start

```
1. Visual Studio 2022에서 DXTnavis.sln 열고 빌드 (Release x64)
2. Navisworks Manage 2025 실행 → Home 탭 → DXTnavis 클릭
3. 계층 구조 로드 → 필터링 → 3D 제어
4. AWP 4D 탭에서 스케줄 CSV 로드 → Execute
5. Full Pipeline으로 Geometry + Mesh + Spatial 통합 Export
```

---

## Installation

### Requirements

| Component | Version |
|-----------|---------|
| Visual Studio | 2022+ |
| .NET Framework | 4.8 |
| Navisworks Manage | 2025 |
| Platform | x64 |

### Build & Deploy

```bash
# Visual Studio에서 빌드 (관리자 권한 필요)
# Configuration: Release, Platform: x64
MSBuild DXTnavis.csproj /p:Configuration=Release /p:Platform=x64
```

> 빌드 후 자동 배포: `C:\Program Files\Autodesk\Navisworks Manage 2025\Plugins\`

---

## Development Status

```
Phases:  █████████████████████ 19/19 Complete
Version: v1.7.0 (2026-03-22)
Period:  2025-12-29 ~ 2026-03-22 (84 days)
```

| Phase | Feature | Version | Status |
|:-----:|---------|:-------:|:------:|
| 1 | Property Filtering | v0.1.0 | ✅ |
| 2 | UI Enhancement | v0.2.0 | ✅ |
| 3 | 3D Integration | v0.2.0 | ✅ |
| 4 | CSV Enhancement | v0.4.0 | ✅ |
| 5 | ComAPI Research | v0.5.0 | ✅ |
| 6 | Code Quality (Partial Class) | v0.5.0 | ✅ |
| 7 | CSV Viewer | v0.5.0 | ✅ |
| 8 | AWP 4D Automation Pipeline | v0.6.0 | ✅ |
| 9 | UI Enhancement (Select All) | v0.7.0 | ✅ |
| 10 | Schedule Builder | v0.8.0 | ✅ |
| 11 | Object Grouping MVP | v0.9.0 | ✅ |
| 12 | Grouped Data Structure | v1.0.0 | ✅ |
| 13 | TimeLiner Enhancement | v1.1.0 | ✅ |
| 14 | Direct TimeLiner Execution | v1.2.0 | ✅ |
| 15 | Geometry Export (BBox/Centroid) | v1.4.0 | ✅ |
| 16 | Unified CSV Export | v1.5.0 | ✅ |
| 17 | Spatial Connectivity | v1.5.0 | ✅ |
| 18 | 3D Mesh GLB Export | v1.6.0 | ✅ |
| 19 | **Pipeline 4D Schedule Builder** | **v1.7.0** | ✅ |

### Release History

| Version | Key Feature | Date |
|:-------:|-------------|:----:|
| **v1.7.0** | **Pipeline 4D Schedule Builder** | 2026-03-22 |
| v1.6.0 | 3D Mesh GLB Export (glTF 2.0) | 2026-02-14 |
| v1.5.0 | Unified CSV + Spatial Connectivity | 2026-02-10 |
| v1.4.0 | Geometry Export (BBox/Centroid/RDF) | 2026-02-06 |
| v1.3.0 | Synthetic ID Generation | 2026-02-05 |
| v1.2.0 | Direct TimeLiner Execution | 2026-01-21 |
| v1.1.0 | TimeLiner Enhancement (TaskType/DateMode) | 2026-01-21 |
| v1.0.0 | Grouped Data Structure (445K→5K) | 2026-01-20 |
| v0.9.0 | Object Grouping MVP | 2026-01-20 |
| v0.8.0 | Schedule Builder | 2026-01-19 |
| v0.6.0 | AWP 4D Automation Pipeline | 2026-01-11 |
| v0.5.0 | ViewModel Refactoring, CSV Viewer | 2026-01-09 |
| v0.4.0 | Object Search, Dual CSV Export | 2026-01-08 |
| v0.3.0 | Tree Expand/Collapse | 2026-01-06 |
| v0.2.0 | 3D Selection, Visibility, Zoom | 2026-01-05 |
| v0.1.0 | Level Filter, SysPath Filter, TreeView | 2026-01-03 |

**[Full Changelog](CHANGELOG.md)**

---

## Project Structure

<details>
<summary><b>Click to expand</b></summary>

```
dxtnavis/
├── Services/
│   ├── NavisworksDataExtractor.cs        # 속성 추출 + Synthetic ID
│   ├── NavisworksSelectionService.cs     # 3D 선택/표시 제어
│   ├── DisplayStringParser.cs            # VariantData 타입 파싱
│   ├── SnapshotService.cs                # 뷰포인트/캡처
│   ├── HierarchyFileWriter.cs            # Hierarchy CSV
│   ├── PropertyFileWriter.cs             # Property CSV + Verbose
│   ├── PropertyWriteService.cs           # ComAPI Property Write
│   ├── SelectionSetService.cs            # Selection Set 생성
│   ├── TimeLinerService.cs               # TimeLiner Task 생성
│   ├── AWP4DAutomationService.cs         # 통합 자동화 파이프라인
│   ├── ObjectMatcher.cs                  # SyncID → ModelItem 매칭
│   ├── AWP4DValidator.cs                 # 검증 서비스
│   ├── ScheduleCsvParser.cs              # 한영 컬럼 매핑 파서
│   ├── PipelineScheduleBuilder.cs       # Pipeline 4D 스케줄 빌더
│   ├── UnifiedCsvExporter.cs             # 22-col 통합 CSV
│   ├── Geometry/
│   │   ├── GeometryExtractor.cs          # BBox 추출 + 배치 처리
│   │   ├── GeometryFileWriter.cs         # manifest.json + geometry.csv
│   │   ├── MeshExtractor.cs              # COM API GLB 메시 추출
│   │   └── GeometryRdfIntegrator.cs      # RDF/TTL 변환
│   └── Spatial/
│       ├── AdjacencyDetector.cs           # BBox 인접성 검출
│       ├── ConnectedComponentFinder.cs    # Union-Find 연결 그룹
│       └── SpatialRelationshipWriter.cs   # adjacency.csv + TTL
├── ViewModels/                            # MVVM Partial Class Pattern
│   ├── DXwindowViewModel.cs              # Core
│   ├── DXwindowViewModel.Filter.cs       # 필터
│   ├── DXwindowViewModel.Search.cs       # 검색
│   ├── DXwindowViewModel.Selection.cs    # 3D 선택
│   ├── DXwindowViewModel.Snapshot.cs     # 스냅샷
│   ├── DXwindowViewModel.Tree.cs         # 트리
│   ├── DXwindowViewModel.Export.cs       # Export + Full Pipeline
│   ├── AWP4DViewModel.cs                 # AWP 4D
│   ├── ScheduleBuilderViewModel.cs       # Schedule Builder
│   ├── PipelineScheduleViewModel.cs     # Pipeline 4D Schedule
│   └── ObjectGroupViewModel.cs           # 객체 그룹화
├── Models/
│   ├── ObjectGroupModel.cs               # 그룹 모델 (v1.0.0)
│   ├── PropertyRecord.cs                 # 속성 레코드
│   ├── FilterOption.cs                   # 필터 옵션
│   ├── ScheduleData.cs                   # 스케줄 데이터
│   ├── DateMode.cs                       # DateMode enum
│   ├── PipelineScheduleOptions.cs       # Pipeline 4D 옵션/모델
│   ├── Geometry/
│   │   ├── Point3D.cs                    # 3D 좌표
│   │   ├── BBox3D.cs                     # Bounding Box
│   │   └── GeometryRecord.cs            # 기하 레코드
│   └── Spatial/
│       ├── AdjacencyRecord.cs            # 인접 관계
│       └── ConnectedGroup.cs             # 연결 그룹
├── Views/
│   └── DXwindow.xaml                     # 메인 UI (6 Tabs)
├── Resources/Ontology/
│   └── dxtnavis-rules.yaml              # BSO 온톨로지 규칙
└── docs/
    ├── phases/                            # Phase 문서 (18개)
    ├── adr/                               # Architecture Decision Records
    └── tech-specs/                        # 기술 명세서
```

</details>

---

## API Dependencies

```xml
<!-- .NET API -->
<Reference Include="Autodesk.Navisworks.Api"/>
<Reference Include="Autodesk.Navisworks.Automation"/>
<Reference Include="Autodesk.Navisworks.Timeliner"/>

<!-- COM API (Property Write, Mesh Extract) -->
<Reference Include="Autodesk.Navisworks.ComApi"/>
<Reference Include="Autodesk.Navisworks.Interop.ComApi"/>
```

---

## Output Formats

| Format | Content | Consumer |
|--------|---------|----------|
| `hierarchy.csv` | 모델 계층 구조 | Excel, Python |
| `geometry.csv` | BBox + Centroid | GIS, 3D Viewer |
| `manifest.json` | Three.js/CesiumJS 호환 | Web 3D |
| `unified.csv` | 22-col 통합 (1obj=1row) | Knowledge Graph |
| `mesh/{uuid}.glb` | glTF 2.0 Binary | Three.js, Blender |
| `adjacency.csv` | 공간 인접 관계 | Network Analysis |
| `spatial_relationships.ttl` | RDF 트리플 | SPARQL, Neo4j |

---

<div align="center">

## Author

**Developer** - Yoon Taegwan
**AI Assistant** - Claude (Anthropic)

---

<sub>Last Updated: 2026-03-22 | v1.7.0 | 19 Phases Complete</sub>

</div>
