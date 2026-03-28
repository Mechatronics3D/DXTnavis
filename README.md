<div align="center">

# DXTnavis

**Navisworks 2025 BIM Data Extraction & 4D Automation Plugin**

[![Version](https://img.shields.io/badge/Version-1.7.1-blue?style=flat-square)]()
[![Navisworks](https://img.shields.io/badge/Navisworks-2025-FF6D00?style=flat-square&logo=autodesk&logoColor=white)](https://www.autodesk.com/products/navisworks)
[![.NET](https://img.shields.io/badge/.NET_Framework-4.8-512BD4?style=flat-square&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![WPF](https://img.shields.io/badge/WPF-MVVM-0078D4?style=flat-square&logo=windows&logoColor=white)]()
[![GLB](https://img.shields.io/badge/glTF_2.0-GLB_Export-00B140?style=flat-square)]()
[![RDF](https://img.shields.io/badge/RDF-Turtle-9B59B6?style=flat-square)]()
[![Platform](https://img.shields.io/badge/Platform-x64-green?style=flat-square)]()

<br/>

*BIM 모델에서 속성, 기하정보, 3D 메시를 추출하고 4D 시뮬레이션을 자동화하는 Navisworks 플러그인*
*SP3D Pipeline 스케줄 자동 생성 지원*

[Impact](#-impact) | [Features](#-features) | [Architecture](#-architecture) | [Pipeline 4D Guide](#pipeline-4d-step-by-step-guide) | [Quick Start](#-quick-start) | [Changelog](CHANGELOG.md)

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
AllProperties CSV ──→ Pipeline/PipeRun 추출 ──→ 그룹핑 ──→ Time Mapping ──→ Selection Set + CSV Export
 (DisplayString:       (자동 컬럼 감지)      (146 Pipeline  (Hybrid 전략)    ├→ PipeRun별 3D Selection Set
  접두사 제거)                                 334 PipeRun)                   └→ TimeLiner Import용 CSV

CSV Import ──→ TimeLiner "다시 작성" ──→ "작업 자동 추가 > 모든 세트에 대해" ──→ 4D Simulation
 (Field Selector)  (Task 계층 생성)      (Selection Set ↔ Task 자동 연결)        (시공 시뮬레이션)
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
<b>Selection Set + CSV</b><br/>
<sub>PipeRun별 3D Set 생성<br/>TimeLiner CSV Export</sub>
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
| Selection Set | .NET API | AddCopy/InsertCopy (fresh traversal) |
| TimeLiner Task | .NET API | 2-Phase: TasksCopyFrom → Selection Link |
| TimeLiner ↔ Set 연결 | **Navisworks UI** | "작업 자동 추가 > 모든 세트에 대해" |
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

## Pipeline 4D: Step-by-Step Guide

### Overview

Pipeline 4D는 SP3D Pipeline 모델에서 외부 스케줄 없이 TimeLiner 4D 시뮬레이션을 자동 생성합니다.
PipeRun 단위로 Selection Set과 Schedule CSV를 생성하고, Navisworks TimeLiner에서 3D 객체와 연결합니다.

### Step 1: AllProperties CSV 내보내기

1. DXTnavis 플러그인의 **Search Set** 탭에서 프로젝트 전체 선택
2. **Export** 영역에서 **AllProperties** 버튼 클릭
3. 저장 경로 선택 → CSV 파일 생성

> AllProperties CSV에는 `Pipeline`, `PipeRun` 컬럼이 포함됩니다.
> `DisplayString:P-015` 형태의 접두사는 자동으로 제거됩니다.

### Step 2: Pipeline 4D 스케줄 생성

1. **Pipeline 4D** 탭으로 이동
2. **CSV 파일 로드**: Step 1에서 생성한 AllProperties CSV 선택
3. Pipeline/PipeRun 컬럼이 자동 감지됨을 확인
4. **시간 매핑 설정** (기본값 권장):
   - 전략: Hybrid (base + per-object)
   - 기본 시간: 8시간
   - 객체당 추가: 0.5시간
   - 근무시간: 8시간/일
5. **Preview** 버튼으로 스케줄 미리보기

### Step 3: Selection Set + CSV 내보내기 실행

1. **Execute** 버튼 클릭 → 다음이 자동 실행됩니다:
   - **Selection Set 생성**: `Pipeline Sets/{Pipeline}/{Pipeline_PipeRun}` 계층 구조
   - 각 PipeRun의 3D 객체가 해당 Selection Set에 등록됩니다
2. **CSV Export** 버튼 클릭 → TimeLiner Import용 CSV 생성

**생성되는 CSV 형식** (Navisworks Field Selector 호환):
```csv
작업 이름,동기화 ID,작업 유형,계획된 시작 날짜,계획된 끝 날짜
P-015\P-015_Dist.Unit B01-4-P-0102,Dist.Unit B01-4-P-0102,구성,2026-01-15,2026-01-18
P-015\P-015_Dist.Unit B01-4-P-0103,Dist.Unit B01-4-P-0103,구성,2026-01-18,2026-01-20
```

> **참고**: `작업 이름`의 `\` (백슬래시)는 TimeLiner에서 계층 구조를 생성합니다.
> TaskName에 Pipeline 접두사가 붙어 중복을 방지합니다 (예: `P-015_PipeRunName`).

### Step 4: TimeLiner에 CSV 가져오기

1. Navisworks **TimeLiner** 패널 열기
2. **데이터 소스** 탭 → **추가** → **CSV (쉼표로 구분된 값)**
3. Step 3에서 생성한 CSV 파일 선택
4. **필드 선택기** 대화상자에서 컬럼 매핑 확인:
   | CSV 컬럼 | TimeLiner 필드 |
   |---------|---------------|
   | 작업 이름 | 작업 이름 |
   | 동기화 ID | 동기화 ID |
   | 작업 유형 | 작업 유형 |
   | 계획된 시작 날짜 | 계획된 시작 |
   | 계획된 끝 날짜 | 계획된 끝 |
5. **"다시 작성"** 클릭 (최초 가져오기 시)
   - 이후 업데이트 시에는 **"동기화"** 사용

### Step 5: Selection Set ↔ Task 연결

1. TimeLiner **작업** 탭에서 아무 작업 하나를 **우클릭**
2. **"작업 자동 추가"** → **"모든 세트에 대해"** 클릭
3. Selection Set 이름과 Task leaf 이름이 매칭되어 3D 객체가 자동 연결됨

> **중요**: "규칙을 사용하여 자동 연결" > "작업에 항목 연결" 기능은 이 워크플로우에서 동작하지 않습니다.
> 반드시 **"작업 자동 추가 > 모든 세트에 대해"**를 사용하세요.

### Step 6: 4D 시뮬레이션 실행

1. TimeLiner **시뮬레이트** 탭으로 이동
2. **재생** 버튼으로 4D 시공 시뮬레이션 확인
3. 각 PipeRun이 계획된 날짜에 따라 순차적으로 나타남

### Pipeline 4D Output Summary

```
Navisworks 내부:
├── Selection Sets/
│   └── Pipeline Sets/
│       ├── P-015/
│       │   ├── P-015_Dist.Unit B01-4-P-0102  (18 objects)
│       │   ├── P-015_Dist.Unit B01-4-P-0103  (5 objects)
│       │   └── ...
│       ├── P-016/
│       │   └── ...
│       └── ... (146 Pipelines)
│
├── TimeLiner Tasks/
│   └── Pipeline Schedule/
│       ├── P-015/
│       │   ├── P-015_Dist.Unit B01-4-P-0102  Jan15→Jan18
│       │   ├── P-015_Dist.Unit B01-4-P-0103  Jan18→Jan20
│       │   └── ...
│       └── ... (334 PipeRuns = 334 Tasks)
│
외부 파일:
└── pipeline_schedule_YYYYMMDD_HHMMSS.csv   (TimeLiner Import용)
```

### Key Technical Details

| 항목 | 설명 |
|------|------|
| **Selection Set 이름** | `{Pipeline}_{PipeRun}` (중복 방지) |
| **CSV 인코딩** | CP949 (한국어 Navisworks 호환) |
| **날짜 형식** | `yyyy-MM-dd` (Field Selector 호환) |
| **Task 계층** | 백슬래시(`\`)로 Parent\Child 구조 |
| **객체 연결 방식** | "작업 자동 추가 > 모든 세트에 대해" |
| **시간 계산** | Hybrid: `BaseDuration + ObjectCount × HoursPerObject` |

### Limitations

- 현재 **Pipeline 객체만** 4D 시뮬레이션에 포함됩니다 (구조물, 장비 등은 별도 Set 필요)
- "작업 자동 추가" 단계는 수동으로 실행해야 합니다 (Navisworks UI 한계)
- AllProperties CSV 내보내기 시 대용량 모델은 1~2분 소요될 수 있습니다

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
4. Full Pipeline으로 Geometry + Mesh + Spatial 통합 Export
```

### Pipeline 4D Quick Start

```
1. Search Set 탭에서 프로젝트 전체 선택 → AllProperties CSV 내보내기
2. Pipeline 4D 탭에서 CSV 로드 → Preview → Execute (Selection Set 생성)
3. CSV Export 버튼으로 TimeLiner 스케줄 CSV 생성
4. TimeLiner 데이터 소스 → CSV 추가 → 필드 매핑 → "다시 작성"
5. TimeLiner 작업 우클릭 → "작업 자동 추가" → "모든 세트에 대해"
6. 시뮬레이트 탭에서 4D 시공 시뮬레이션 재생
```

> 상세 가이드: [Pipeline 4D: Step-by-Step Guide](#pipeline-4d-step-by-step-guide)

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
Version: v1.7.1 (2026-03-23)
Period:  2025-12-29 ~ 2026-03-23 (85 days)
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
| **v1.7.1** | **Pipeline 4D Workflow Complete** | 2026-03-23 |
| v1.7.0 | Pipeline 4D Schedule Builder | 2026-03-22 |
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

<sub>Last Updated: 2026-03-23 | v1.7.1 | 19 Phases Complete</sub>

</div>
