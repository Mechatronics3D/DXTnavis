<div align="center">
  <img src=".github/thumbnail.png" alt="DXTnavis" width="600" />

# DXTnavis

**Navisworks 2025 BIM Data Extraction & 4D Simulation Automation Plugin**

[![Version](https://img.shields.io/badge/Version-1.8.0-blue?style=flat-square)]()
[![Release](https://img.shields.io/github/v/release/tygwan/DXTnavis?style=flat-square&color=green)](https://github.com/tygwan/DXTnavis/releases/latest)
[![Navisworks](https://img.shields.io/badge/Navisworks-2025-FF6D00?style=flat-square&logo=autodesk&logoColor=white)](https://www.autodesk.com/products/navisworks)
[![.NET](https://img.shields.io/badge/.NET_Framework-4.8-512BD4?style=flat-square&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-x64-green?style=flat-square)]()

<br/>

*BIM 모델에서 속성, 기하정보, 3D 메시를 추출하고*
*Pipeline 4D 시공 시뮬레이션을 자동화하는 Navisworks 플러그인*

[Features](#features) | [Pipeline 4D Guide](#pipeline-4d-step-by-step-guide) | [Architecture](#architecture) | [Installation](#installation) | [Changelog](CHANGELOG.md)

</div>

---

## What is DXTnavis?

DXTnavis는 Navisworks 2025 플러그인으로, **대규모 BIM 모델의 데이터 추출**과 **4D 시공 시뮬레이션 자동화**를 수행합니다.

핵심 가치는 **수작업 제거**입니다:

| 작업 | Before (수동) | After (DXTnavis) | 단축률 |
|------|:------------:|:----------------:|:-----:|
| BIM 속성 추출 | 4시간+ | 5분 | 98% |
| 4D 시뮬레이션 셋업 | 2일+ | 10분 | 99% |
| Pipeline 4D 스케줄 | 1주+ | 5분 | 99% |
| 445K 속성 필터링 | 불가능 | 실시간 | NEW |
| 3D 메시 추출 (GLB) | 불가능 | 원클릭 | NEW |

---

## Features

### 1. BIM Data Export

> 445K+ 속성을 가진 대규모 BIM 모델에서 원하는 데이터를 추출하고 내보내기

- **AllProperties CSV**: 모든 객체 x 모든 속성을 flat CSV로 추출 (1행 = 1객체)
- **Refined XLSX**: 피벗 형태 Excel 내보내기 (카테고리별 시트 분리)
- **Hierarchy Navigation**: L0~L10 레벨 기반 트리 탐색, 색상 배지, 노드 아이콘
- **Property Search**: 이름, 속성값, SysPath 검색
- **Object Grouping**: 445K 속성 → ~5K 그룹 최적화, 체크박스 필터

### 2. Pipeline 4D Simulation

> SP3D Pipeline 프로젝트에서 외부 스케줄 없이 TimeLiner 4D 시뮬레이션 자동 생성

```
AllProperties CSV → Pipeline/PipeRun 추출 → 그룹핑 → 시간 매핑 → Selection Set + CSV
                                                                     │
                                          TimeLiner CSV Import ←─────┘
                                                │
                                   "작업 자동 추가 > 모든 세트에 대해"
                                                │
                                         4D 시뮬레이션 재생
```

- **자동 파싱**: AllProperties CSV에서 Pipeline/PipeRun 컬럼 자동 감지
- **시간 자동 매핑**: Hybrid 전략 (`기본시간 + 객체수 × 단위시간`)
- **Selection Set 생성**: PipeRun 단위로 3D 객체를 자동 그룹화
- **TimeLiner CSV**: Navisworks Field Selector 호환 포맷 자동 생성
- **검증 완료**: 146 Pipeline, 334 PipeRun, 4D 시뮬레이션 end-to-end 동작 확인

### 3. AWP 4D Automation

> 외부 스케줄 CSV를 Navisworks TimeLiner와 자동 연결

```
CSV File → Schedule Parser → Object Matcher → Property Write → Selection Set → TimeLiner Task
(한영매핑)   (SyncID 추출)    (자동 매칭)      (ComAPI)         (.NET API)       (.NET API)
```

- **6-Step Pipeline**: CSV 로드부터 TimeLiner Task 생성까지 자동화
- **SyncID 매칭**: Element ID, InstanceGuid 기반 자동 매칭
- **Direct TimeLiner**: CSV 없이 원클릭 연결 (7단계 → 3단계, 57% 단축)

### 4. 3D Geometry & Mesh Export

> Navisworks NWD에서 glTF 2.0 GLB 메시를 추출하여 웹 3D 뷰어와 연동

- **GLB Mesh Export**: COM API 기반 per-object GLB 추출, LCS→WCS 좌표 변환
- **BBox/Centroid**: World 좌표계 AABB 바운딩 박스, 중심점 자동 계산
- **Spatial Adjacency**: BBox 인접성 검출, Union-Find 연결 그룹, RDF/TTL 생성
- **Unified CSV**: 22-column 통합 스키마 (1 row = 1 object)

### 5. 3D Viewport Control

| Select in 3D | Show Only | Show All | Zoom | Reset Home |
|:---:|:---:|:---:|:---:|:---:|
| 필터 → 3D 선택 | 필터 객체만 표시 | 전체 복원 | 선택 객체 이동 | 초기 뷰포인트 |

---

## Pipeline 4D: Step-by-Step Guide

### Step 1: AllProperties CSV 내보내기

1. DXTnavis 플러그인의 **Search Set** 탭에서 프로젝트 전체 선택
2. **Export** 영역에서 **AllProperties** 버튼 클릭
3. 저장 경로 선택 → CSV 파일 생성

### Step 2: Pipeline 4D 스케줄 생성

1. **Pipeline 4D** 탭으로 이동
2. Step 1에서 생성한 AllProperties CSV 파일 선택
3. Pipeline/PipeRun 컬럼이 자동 감지됨을 확인
4. 시간 매핑 설정 (기본값 권장):
   - 전략: Hybrid (base + per-object)
   - 기본 시간: 8시간, 객체당 추가: 0.5시간
5. **Preview** 버튼으로 스케줄 미리보기

### Step 3: Selection Set + CSV Export 실행

1. **Execute** 버튼 클릭 → Selection Set 자동 생성
   - 구조: `Pipeline Sets/{Pipeline}/{Pipeline_PipeRun}`
2. **CSV Export** 버튼 → TimeLiner Import용 CSV 생성

생성되는 CSV:
```csv
작업 이름,동기화 ID,작업 유형,계획된 시작 날짜,계획된 끝 날짜
P-015\P-015_Dist.Unit B01-4-P-0102,1,구성,2026-01-15,2026-01-18
```

### Step 4: TimeLiner에 CSV 가져오기

1. Navisworks **TimeLiner** 패널 열기
2. **데이터 소스** 탭 → **추가** → **CSV (쉼표로 구분된 값)**
3. CSV 파일 선택 → 필드 매핑 확인 → **"다시 작성"** 클릭

### Step 5: 3D 객체 연결

1. TimeLiner **작업** 탭에서 아무 작업 하나를 **우클릭**
2. **"작업 자동 추가"** → **"모든 세트에 대해"** 클릭

> **중요**: "규칙을 사용하여 자동 연결" > "작업에 항목 연결"은 동작하지 않습니다.
> 반드시 **"작업 자동 추가 > 모든 세트에 대해"**를 사용하세요.

### Step 6: 4D 시뮬레이션 실행

TimeLiner **시뮬레이트** 탭 → **재생** → 각 PipeRun이 계획된 날짜에 따라 순차적으로 나타남

<details>
<summary><b>Pipeline 4D Technical Details</b></summary>

| 항목 | 설명 |
|------|------|
| **Selection Set 이름** | `{Pipeline}_{PipeRun}` (중복 방지) |
| **CSV 인코딩** | CP949 (한국어 Navisworks 호환) |
| **날짜 형식** | `yyyy-MM-dd` (Field Selector 호환) |
| **Task 계층** | 백슬래시(`\`)로 Parent\Child 구조 |
| **시간 계산** | Hybrid: `BaseDuration + ObjectCount x HoursPerObject` |

**현재 한계:**
- Pipeline 객체만 4D에 포함 (구조물, 장비 등은 별도 Set 필요)
- "작업 자동 추가" 단계는 Navisworks UI에서 수동 실행 필요

</details>

---

## Architecture

### Hybrid API Strategy

Navisworks는 .NET API와 COM API 두 가지를 제공합니다. DXTnavis는 용도에 맞게 조합합니다:

| Feature | API | Why |
|---------|-----|-----|
| Property Read | .NET API | 표준 데이터 접근 |
| **Property Write** | **COM API** | .NET API는 Read-Only |
| Selection Set | .NET API | AddCopy (fresh traversal 패턴) |
| TimeLiner Task | .NET API | 2-Phase: TasksCopyFrom → Selection Link |
| Task ↔ Set 연결 | **Navisworks UI** | "작업 자동 추가 > 모든 세트에 대해" |
| **Mesh Extract** | **COM API** | GenerateSimplePrimitives() |
| **ViewPoint Save** | **COM API** | .NET API 미지원 |

### MVVM Pattern

```
View (XAML)          ViewModel (Partial Class)         Services
─────────────       ──────────────────────────        ──────────────
DXwindow.xaml   ←→  Core / Filter / Search        ←→  DataExtractor
TabControl           Selection / Snapshot               ObjectMatcher
TreeView             Tree / Export                      SelectionSetService
DataGrid             AWP4D / Pipeline4D                 TimeLinerService
                                                        PipelineScheduleBuilder
                          ↕
                     Models
                     ObjectGroup, ScheduleData
                     PipelineScheduleOptions
                     GeometryRecord, BBox3D
```

### Key Technical Decisions

<details>
<summary><b>ComAPI Reverse Engineering — Property Write</b></summary>

Navisworks .NET API는 Property를 Read-Only로만 제공합니다.
4D 자동화를 위해 Custom Property 기입이 필수였으며, ComAPI `SetUserDefined()`를 발견하여 해결했습니다.

```csharp
// .NET API: Read-Only (Write 불가)
modelItem.PropertyCategories  // ← 읽기만 가능

// ComAPI: Write 가능
InwOpState10 comState = ComApiBridge.State;
InwOaPath comPath = ComApiBridge.ToInwOaPath(modelItem);
InwGUIPropertyNode2 propNode = comState.GetGUIPropertyNode(comPath, true);
propNode.SetUserDefined(0, "AWP Schedule", "AWP_Internal", propVec);
```

</details>

<details>
<summary><b>COM Late-Binding — 3D Mesh LCS→WCS 변환</b></summary>

`GetLocalToWorldMatrix()`는 `InwLTransform3f` COM 객체를 반환하는데,
C# `as Array` 캐스트가 항상 실패합니다. COM Interop Late-binding으로 해결했습니다.

```csharp
// ❌ 실패: COM 객체는 Array로 직접 캐스트 불가
Array matrix = transformObj as Array;  // 항상 null

// ✅ 성공: Late-binding으로 Matrix 속성 접근
var matrixData = transformObj.GetType().InvokeMember(
    "Matrix", BindingFlags.GetProperty, null, transformObj, null);
```

</details>

<details>
<summary><b>3-Phase Fresh Traversal — WeakRef GC 해결</b></summary>

Navisworks는 ModelItem을 WeakReference로 관리합니다. 캐시에 저장하면 GC가 원본을 회수하여 `ObjectDisposedException`이 발생합니다.

**해결: 3-Phase 패턴**
1. **Phase A**: ScheduleData에서 GUID → schedule index 매핑 생성
2. **Phase B**: 모델 트리 1회 순회, fresh ModelItem을 해당 schedule의 컬렉션에 즉시 Add
3. **Phase C**: 수집 직후 바로 SelectionSet 생성 (ModelItem이 살아있는 동안)

</details>

<details>
<summary><b>Synthetic ID — 계층 구조 보존</b></summary>

`InstanceGuid`가 Empty인 경우(CATIA, PDMS 등)에도 계층 구조를 보존하기 위해
MD5 해시 기반 결정적 GUID 생성 시스템을 구현했습니다.

```
Fallback: InstanceGuid → Item GUID → Authoring ID → Hierarchy Path Hash
지원 ID: Revit Element ID, AutoCAD Handle, IFC GlobalId
```

</details>

---

## Installation

### Requirements

| Component | Version |
|-----------|---------|
| Navisworks Manage | 2025 |
| .NET Framework | 4.8 |
| Platform | x64 |

### For Users (Plugin Only)

`DXTnavis/` 폴더를 아래 경로에 복사하고 Navisworks를 재시작합니다:

```
C:\Program Files\Autodesk\Navisworks Manage 2025\Plugins\DXTnavis\
├── DXTnavis.dll              ← 플러그인 본체
├── ClosedXML.dll             ← Refined XLSX 생성
├── DocumentFormat.OpenXml.dll
├── Newtonsoft.Json.dll
├── System.Text.Json.dll
└── ... (총 17개 DLL)
```

> Navisworks 실행 → **Home 탭** → **DXTnavis** 버튼 클릭

### For Developers (Build from Source)

```bash
# Visual Studio 2022에서 빌드 (Release x64)
MSBuild DXTnavis.csproj /p:Configuration=Release /p:Platform=x64
```

> 빌드 후 자동 배포: `C:\Program Files\Autodesk\Navisworks Manage 2025\Plugins\`

---

## Output Formats

| Format | Content | Consumer |
|--------|---------|----------|
| `AllProperties.csv` | 모든 객체 x 모든 속성 | Pipeline 4D, Excel |
| `Refined.xlsx` | 피벗 형태 속성 | Excel, 보고서 |
| `hierarchy.csv` | 모델 계층 구조 | Python, Analytics |
| `geometry.csv` | BBox + Centroid | GIS, 3D Viewer |
| `unified.csv` | 22-col 통합 (1obj=1row) | Knowledge Graph |
| `mesh/{uuid}.glb` | glTF 2.0 Binary | Three.js, Blender |
| `spatial.ttl` | RDF 트리플 | SPARQL, Neo4j |
| `pipeline_schedule.csv` | TimeLiner 스케줄 | Navisworks TimeLiner |

---

## Project Structure

<details>
<summary><b>Click to expand</b></summary>

```
dxtnavis/
├── Services/
│   ├── NavisworksDataExtractor.cs         # 속성 추출 + Synthetic ID
│   ├── FullModelExporterService.cs        # AllProperties CSV Export
│   ├── RefinedXlsxExporter.cs             # Refined XLSX Export
│   ├── SelectionSetService.cs             # Selection Set 생성 (3-Phase)
│   ├── TimeLinerService.cs                # TimeLiner Task 생성 (2-Phase)
│   ├── PipelineScheduleBuilder.cs         # Pipeline 4D 스케줄 빌더
│   ├── ObjectMatcher.cs                   # SyncID/GUID → ModelItem 매칭
│   ├── AWP4DAutomationService.cs          # AWP 4D 통합 파이프라인
│   ├── PropertyWriteService.cs            # ComAPI Property Write
│   ├── Geometry/
│   │   ├── GeometryExtractor.cs           # BBox 추출
│   │   ├── MeshExtractor.cs               # COM API GLB 메시 추출
│   │   └── GeometryRdfIntegrator.cs       # RDF/TTL 변환
│   └── Spatial/
│       ├── AdjacencyDetector.cs           # BBox 인접성 검출
│       └── ConnectedComponentFinder.cs    # Union-Find 그룹
├── ViewModels/                             # MVVM Partial Class Pattern
│   ├── DXwindowViewModel.cs               # Core + Filter + Search + ...
│   ├── DXwindowViewModel.Export.cs        # Export + Full Pipeline
│   ├── AWP4DViewModel.cs                  # AWP 4D
│   ├── PipelineScheduleViewModel.cs       # Pipeline 4D
│   └── ScheduleBuilderViewModel.cs        # Schedule Builder
├── Models/
│   ├── ObjectGroupModel.cs                # 객체 그룹 (445K→5K)
│   ├── ScheduleData.cs                    # 스케줄 데이터
│   ├── PipelineScheduleOptions.cs         # Pipeline 4D 옵션
│   └── Geometry/                          # BBox3D, Point3D, GeometryRecord
├── Views/
│   └── DXwindow.xaml                      # 메인 UI
└── docs/
    └── guides/
        └── pipeline-4d-developer-guide.md # Pipeline 4D 개발자 가이드
```

</details>

---

## Development

```
Phases:  █████████████████████ 19/19 Complete
Version: v1.8.0 (2026-03-29)
Period:  2025-12-29 ~ 2026-03-29 (91 days)
```

| Phase | Feature | Version |
|:-----:|---------|:-------:|
| 1-3 | Property Filter, UI, 3D Integration | v0.1~0.2 |
| 4-7 | CSV Export, ComAPI, Code Quality, CSV Viewer | v0.4~0.5 |
| 8 | AWP 4D Automation Pipeline | v0.6 |
| 9-12 | Select All, Schedule Builder, Object Grouping | v0.7~1.0 |
| 13-14 | TimeLiner Enhancement, Direct Execution | v1.1~1.2 |
| 15-17 | Geometry, Unified CSV, Spatial Connectivity | v1.4~1.5 |
| 18 | 3D Mesh GLB Export (glTF 2.0) | v1.6 |
| **19** | **Pipeline 4D Schedule + Workflow Complete** | **v1.8** |

### Release History

| Version | Key Feature | Date |
|:-------:|-------------|:----:|
| **v1.8.0** | **Pipeline 4D Workflow Complete** | 2026-03-29 |
| v1.7.0 | Pipeline 4D Schedule Builder | 2026-03-22 |
| v1.6.0 | 3D Mesh GLB Export (glTF 2.0) | 2026-02-14 |
| v1.5.0 | Unified CSV + Spatial Connectivity | 2026-02-10 |
| v1.4.0 | Geometry Export (BBox/Centroid/RDF) | 2026-02-06 |
| v1.2.0 | Direct TimeLiner Execution | 2026-01-21 |
| v1.0.0 | Grouped Data Structure (445K→5K) | 2026-01-20 |
| v0.6.0 | AWP 4D Automation Pipeline | 2026-01-11 |
| v0.1.0 | Initial Release | 2026-01-03 |

**[Full Changelog](CHANGELOG.md)** | **[Developer Guide](docs/guides/pipeline-4d-developer-guide.md)**

---

<div align="center">

**Developer** - Yoon Taegwan | **AI Assistant** - Claude (Anthropic)

<sub>v1.8.0 | 19 Phases Complete | 91 Days of Development</sub>

</div>
