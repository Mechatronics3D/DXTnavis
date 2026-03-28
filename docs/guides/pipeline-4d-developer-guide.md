# Pipeline 4D 개발자 가이드

> Pipeline 4D의 3대 핵심 기능 — AllProperties Export, Pipeline Schedule CSV, Selection Set 자동 생성 — 의 코드 위치와 동작 원리를 설명합니다.

---

## 목차

1. [전체 워크플로우 개요](#1-전체-워크플로우-개요)
2. [AllProperties CSV Export](#2-allproperties-csv-export)
3. [Pipeline Schedule CSV Export](#3-pipeline-schedule-csv-export)
4. [Selection Set 자동 생성](#4-selection-set-자동-생성)
5. [코드 파일 요약표](#5-코드-파일-요약표)
6. [주의사항 & 제약조건](#6-주의사항--제약조건)

---

## 1. 전체 워크플로우 개요

Pipeline 4D는 3단계로 동작합니다:

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        Pipeline 4D 전체 흐름                            │
│                                                                         │
│  [Step 1]              [Step 2]                [Step 3]                 │
│  AllProperties CSV  →  Pipeline Schedule CSV  →  Selection Set 생성     │
│  (BIM 속성 추출)       (일정 계산 + CSV)         (3D 객체 그룹)          │
│                                                                         │
│  파일 위치:            파일 위치:               Navisworks 내부:         │
│  사용자 지정 경로       사용자 지정 경로          Selection Sets 패널     │
└─────────────────────────────────────────────────────────────────────────┘
                                │
                                ▼
              Navisworks TimeLiner에서 CSV 가져오기 (수동)
                                │
                                ▼
              "작업 자동 추가 > 모든 세트에 대해" (수동)
                                │
                                ▼
                        4D 시뮬레이션 재생
```

**비유로 설명하면:**

- **Step 1 (AllProperties)**: 건물의 모든 부품 목록을 만드는 것. 어떤 파이프가 어디에 있고 어떤 속성을 갖는지 전부 기록.
- **Step 2 (Pipeline Schedule)**: 부품 목록을 보고 "이 파이프는 1월 15일~18일에 설치" 같은 공사 일정표를 만드는 것.
- **Step 3 (Selection Set)**: Navisworks 3D 뷰에서 각 파이프 그룹을 클릭 한 번으로 선택할 수 있도록 묶어두는 것.

---

## 2. AllProperties CSV Export

### 한 줄 요약

> Navisworks 모델의 **모든 객체 × 모든 속성**을 하나의 CSV 파일로 추출합니다.

### 코드 위치

| 역할 | 파일 | 메서드 |
|------|------|--------|
| UI 버튼 핸들러 | `ViewModels/DXwindowViewModel.Export.cs:86` | `ExportAllPropertiesAsync()` |
| 실제 생성 로직 | `Services/FullModelExporterService.cs:24` | `ExportAllPropertiesToCsv()` |
| 속성 추출 엔진 | `Services/NavisworksDataExtractor.cs` | `TraverseAndExtractProperties()` |

### 동작 순서

```
사용자가 "AllProperties" 버튼 클릭
  │
  ├─ 1단계: 모델 트리 전체 순회
  │    NavisworksDataExtractor가 모든 ModelItem을 방문하면서
  │    각 객체의 모든 속성(Category, PropertyName, Value)을 수집
  │    → List<HierarchicalPropertyRecord>
  │
  ├─ 2단계: 속성 구조 분석
  │    수집된 속성에서 고유한 "카테고리|속성명" 조합을 추출
  │    → 이것이 CSV의 컬럼 헤더가 됨
  │    → 객체별로 속성값을 딕셔너리에 정리
  │
  └─ 3단계: CSV 파일 작성
       헤더: ObjectId, ParentId, Level, 객체이름, 속성1, 속성2, ...
       본문: 한 행 = 한 객체, 해당 속성이 없으면 빈칸
       인코딩: UTF-8
```

### 출력 예시

```csv
ObjectId,ParentId,Level,객체이름,Element|Pipeline,Element|PipeRun,...
abc-123,parent-456,7,Pipe-001,DisplayString:P-015,DisplayString:Dist.Unit B01,...
def-789,parent-456,7,Elbow-001,DisplayString:P-015,DisplayString:Dist.Unit B01,...
```

### 핵심 포인트

- **STA 스레드 필수**: Navisworks API는 UI 스레드에서만 동작. `Task.Run()` 사용 금지.
- **DoEvents 패턴**: 100개 객체마다 `WinForms.Application.DoEvents()` 호출하여 60초 COM 타임아웃(ContextSwitchDeadlock) 방지.
- Pipeline 4D에서 이 CSV의 `Pipeline`, `PipeRun` 컬럼을 사용하여 객체를 그룹핑합니다.

---

## 3. Pipeline Schedule CSV Export

### 한 줄 요약

> AllProperties CSV를 읽어서 Pipeline/PipeRun별로 그룹핑하고, 자동으로 공사 일정을 계산한 뒤, Navisworks TimeLiner가 읽을 수 있는 CSV를 생성합니다.

### 코드 위치

| 역할 | 파일 | 메서드 |
|------|------|--------|
| UI 버튼 핸들러 | `ViewModels/PipelineScheduleViewModel.cs:602` | `ExportCsv()` |
| Preview 생성 | `ViewModels/PipelineScheduleViewModel.cs` | `Preview()` |
| CSV 파싱+그룹핑 | `Services/PipelineScheduleBuilder.cs:182` | `ExtractAndGroupFromCsv()` |
| 시간 자동 매핑 | `Services/PipelineScheduleBuilder.cs` | `ApplyTimeMapping()` |
| ScheduleData 변환 | `Services/PipelineScheduleBuilder.cs` | `ConvertToScheduleData()` |
| CSV 파일 생성 | `Services/PipelineScheduleBuilder.cs:103` | `ExportTimeLinerCsv()` |
| 옵션 빌드 | `ViewModels/PipelineScheduleViewModel.cs:679` | `BuildOptions()` |
| 옵션 모델 | `Models/PipelineScheduleOptions.cs` | `PipelineScheduleOptions` class |

### 동작 순서

```
사용자가 "Preview" 버튼 클릭
  │
  ├─ 1단계: CSV 파싱 + 그룹핑 (ExtractAndGroupFromCsv)
  │    AllProperties CSV를 읽어서:
  │    - Pipeline, PipeRun, ObjectId 컬럼을 자동 감지
  │    - "DisplayString:" 접두사 자동 제거
  │    - Pipeline → PipeRun → Objects 계층으로 그룹핑
  │
  │    예: P-015 (Pipeline)
  │        ├── Dist.Unit B01-4-P-0102 (PipeRun) → [Pipe-001, Elbow-001, ...] 18개 객체
  │        └── Dist.Unit B01-4-P-0103 (PipeRun) → [Pipe-002, Tee-001, ...] 5개 객체
  │
  ├─ 2단계: 시간 매핑 (ApplyTimeMapping)
  │    각 PipeRun에 공사 기간을 자동 계산:
  │
  │    duration(시간) = BaseDuration(8h) + 객체수 × HoursPerObject(0.5h)
  │    days(일수) = ceil(duration / 근무시간(8h/일))
  │
  │    예: 18개 객체 PipeRun → 8 + 18×0.5 = 17시간 → ceil(17/8) = 3일
  │
  │    PipeRun들을 순차 배치: PipeRun1(Jan15~17) → PipeRun2(Jan18~19) → ...
  │    Pipeline 사이에는 GapDays(기본 0일) 추가
  │
  └─ 3단계: ScheduleData 변환 (ConvertToScheduleData)
       각 PipeRun을 1개 ScheduleData로 변환:
         TaskName    = "P-015_Dist.Unit B01-4-P-0102"  (유니크 이름)
         ParentSet   = "P-015"                          (폴더 이름)
         SyncID      = "Dist.Unit B01-4-P-0102"         (원래 PipeRun명)
         ObjectIds   = "guid1;guid2;guid3;..."           (속하는 객체 GUID들)
         PlannedStart/End = 계산된 날짜

사용자가 "CSV Export" 버튼 클릭
  │
  └─ ExportTimeLinerCsv()
       ScheduleData 리스트 → TimeLiner Import용 CSV 변환:

       헤더:  작업 이름, 동기화 ID, 작업 유형, 계획된 시작 날짜, 계획된 끝 날짜
       본문:  P-015\P-015_Dist.Unit B01-4-P-0102, 1, 구성, 2026-01-15, 2026-01-18

       - "작업 이름"의 백슬래시(\)는 TimeLiner에서 계층 구조 생성
       - 날짜 형식: yyyy-MM-dd (시간 없음)
       - 인코딩: CP949 (한국어 Navisworks 호환)
```

### 시간 계산 전략

| 전략 | 계산식 | 설명 |
|------|--------|------|
| **Hybrid (기본)** | `base + count × per_object` | 기본 시간 + 객체 수 비례 |
| FixedDuration | `base`만 사용 | 모든 PipeRun 동일 기간 |
| ObjectCountBased | `count × per_object`만 | 객체 수에만 비례 |

### 출력 CSV 예시

```csv
작업 이름,동기화 ID,작업 유형,계획된 시작 날짜,계획된 끝 날짜
P-015\P-015_Dist.Unit B01-4-P-0102,1,구성,2026-01-15,2026-01-18
P-015\P-015_Dist.Unit B01-4-P-0103,2,구성,2026-01-18,2026-01-20
P-016\P-016_Recovery Stage 2-6-X-0002,3,구성,2026-01-20,2026-01-22
```

### 핵심 포인트

- **컬럼명이 Navisworks Field Selector의 필드명과 정확히 일치**해야 자동 매핑됨.
- **TaskName에 Pipeline 접두사**를 붙여 중복 방지 (동일 PipeRun명이 다른 Pipeline에 존재 가능).
- **CP949 인코딩** 필수 — UTF-8로 저장하면 Navisworks에서 한글 깨짐.

---

## 4. Selection Set 자동 생성

### 한 줄 요약

> ScheduleData의 ObjectIds(GUID 목록)를 사용하여, 모델 트리를 1회 순회하면서 각 PipeRun에 속하는 3D 객체를 수집하고, Navisworks Selection Set으로 등록합니다.

### 코드 위치

| 역할 | 파일 | 메서드 |
|------|------|--------|
| Execute에서 호출 | `ViewModels/PipelineScheduleViewModel.cs:545-551` | Execute Step 3 |
| 생성 로직 (3-Phase) | `Services/SelectionSetService.cs:183` | `CreatePipelineSets()` |
| 트리 순회 수집 | `Services/SelectionSetService.cs:326` | `CollectItemsForPipelineSets()` |
| 폴더 생성 | `Services/SelectionSetService.cs` | `GetOrCreateRootFolder()`, `CreateFolderPath()` |
| Set 생성 | `Services/SelectionSetService.cs` | `CreateSelectionSet()` |

### 동작 순서 — 3-Phase 패턴

Selection Set 생성은 **WeakRef GC 문제**를 해결하기 위해 3단계로 나뉩니다.

> **WeakRef GC 문제란?**
> Navisworks는 ModelItem을 WeakReference로 관리합니다. 캐시에 저장해두면 .NET GC가 원본을 회수해서 `ObjectDisposedException`이 발생합니다. 따라서 ModelItem을 수집한 직후 바로 사용해야 합니다.

```
CreatePipelineSets(schedules, objectMatcher, options)
  │
  ├─ Phase A: GUID 파싱 (line 197-222)
  │    ──────────────────────────────
  │    각 ScheduleData에서 ObjectIds 문자열을 파싱하여
  │    GUID → schedule 인덱스 매핑을 생성합니다.
  │
  │    입력: schedules[0].ObjectIds = "guid-A;guid-B;guid-C"
  │          schedules[1].ObjectIds = "guid-D;guid-E"
  │
  │    결과: guidToScheduleIndices = {
  │            guid-A → [0],
  │            guid-B → [0],
  │            guid-C → [0],
  │            guid-D → [1],
  │            guid-E → [1]
  │          }
  │
  │    비유: "이 부품 번호가 적힌 부품을 찾으면 몇 번째 상자에 넣을지" 미리 정해두는 것.
  │
  ├─ Phase B: 모델 트리 1회 순회 (line 228-240, 326-361)
  │    ────────────────────────────────────────────────
  │    Navisworks 모델의 전체 트리를 루트부터 재귀 순회합니다.
  │    각 노드(ModelItem)를 방문할 때:
  │      1. InstanceGuid가 guidToScheduleIndices에 있는지 확인
  │      2. 있으면 → 해당 schedule의 ModelItemCollection에 직접 Add
  │
  │    핵심: 캐시에서 꺼내지 않고 트리에서 갓 만난 fresh ModelItem을 사용
  │         → WeakRef GC 문제 완전 회피
  │
  │    비유: 창고(모델 트리)를 한 바퀴 돌면서, 부품 번호가 적힌 부품을 만나면
  │          미리 정해둔 상자에 바로 넣는 것. 메모해뒀다가 나중에 찾으러 가면
  │          이미 치워져서(GC) 없을 수 있으므로 즉시 넣는 게 핵심.
  │
  │    10,000 노드마다 DoEvents() 호출 (ContextSwitchDeadlock 방지)
  │
  └─ Phase C: SelectionSet 즉시 생성 (line 248-308)
       ────────────────────────────────────────────
       Phase B에서 수집한 ModelItem들이 아직 살아있는 동안
       즉시 Navisworks Selection Set을 생성합니다.

       각 schedule에 대해:
         1. 폴더 생성: "Pipeline Sets/{Pipeline명}"
         2. Set 생성: "{Pipeline}_{PipeRun}" 이름으로 SelectionSet 생성
         3. doc.SelectionSets.AddCopy()/InsertCopy()로 등록
         4. 5개마다 DoEvents()

       비유: 상자에 부품을 다 넣었으면, 바로 선반(Selection Set 패널)에 올려놓는 것.
             너무 오래 들고 있으면 부품이 사라질(GC) 수 있으므로 즉시 등록.
```

### 생성되는 구조

```
Navisworks Selection Sets 패널:
└── Pipeline Sets/                        ← 루트 폴더 (options.SelectionSetRootFolder)
    ├── P-015/                            ← Pipeline 폴더
    │   ├── P-015_Dist.Unit B01-4-P-0102  ← SelectionSet (18개 객체)
    │   ├── P-015_Dist.Unit B01-4-P-0103  ← SelectionSet (5개 객체)
    │   └── ...
    ├── P-016/
    │   └── P-016_Recovery Stage 2-6-X-0002  ← SelectionSet
    └── ... (총 146 Pipeline 폴더, 334 SelectionSet)
```

### 핵심 포인트

- **트리 순회는 딱 1번**만 합니다. 334개 PipeRun의 객체를 동시에 수집하므로 효율적.
- **Set 이름 = CSV의 leaf task 이름**: "작업 자동 추가 > 모든 세트에 대해" 기능이 이름 매칭으로 연결.
- **빈 Set은 건너뜀**: `options.SkipEmptySelectionSets = true`일 때 객체가 0개인 PipeRun은 생성하지 않음.

---

## 5. 코드 파일 요약표

### 핵심 파일 (3개 기능에 직접 관여)

| # | 파일 | 주요 역할 | 기능 |
|---|------|----------|------|
| 1 | `Services/FullModelExporterService.cs` | AllProperties CSV 생성 | Step 1 |
| 2 | `Services/NavisworksDataExtractor.cs` | 모델 속성 추출 엔진 | Step 1 |
| 3 | `Services/PipelineScheduleBuilder.cs` | CSV 파싱, 시간 매핑, TimeLiner CSV 생성 | Step 2 |
| 4 | `Services/SelectionSetService.cs` | 3-Phase Selection Set 자동 생성 | Step 3 |
| 5 | `ViewModels/DXwindowViewModel.Export.cs` | AllProperties UI 핸들러 | Step 1 UI |
| 6 | `ViewModels/PipelineScheduleViewModel.cs` | Pipeline 4D 탭 전체 UI/로직 | Step 2,3 UI |
| 7 | `Models/PipelineScheduleOptions.cs` | 옵션 모델 + enum 정의 | 설정 |

### 보조 파일

| 파일 | 역할 |
|------|------|
| `Services/ObjectMatcher.cs` | InstanceGuid → ModelItem 검색 (Step 3 매칭 시 사용) |
| `Services/TimeLinerService.cs` | TimeLiner Task 자동 생성 (Execute Step 4) |
| `Models/ScheduleData.cs` | PipeRun 1개의 스케줄 정보 데이터 모델 |
| `Views/DXwindow.xaml` | Pipeline 4D 탭 UI 레이아웃 |

### 데이터 흐름도

```
[NavisworksDataExtractor]          [PipelineScheduleBuilder]        [SelectionSetService]
        │                                   │                              │
  TraverseAndExtract               ExtractAndGroupFromCsv          CreatePipelineSets
        │                                   │                              │
        ▼                                   ▼                              ▼
HierarchicalPropertyRecord[]       PipelineGroup[]               ModelItemCollection[]
        │                                   │                              │
        ▼                                   ▼                              ▼
[FullModelExporterService]           ApplyTimeMapping             SelectionSet 등록
        │                                   │                      (Navisworks API)
        ▼                                   ▼
  AllProperties.csv              ConvertToScheduleData
                                        │
                                        ▼
                                 List<ScheduleData>
                                   │           │
                                   ▼           ▼
                          ExportTimeLinerCsv   CreatePipelineSets
                                   │           (→ Step 3)
                                   ▼
                            pipeline_schedule.csv
```

---

## 6. 주의사항 & 제약조건

### 절대 금지 사항

| 금지 | 이유 | 올바른 방법 |
|------|------|------------|
| `Task.Run()`에서 Navisworks API 호출 | STA 스레드 위반 → 크래시 | UI 스레드에서 직접 실행 |
| ModelItem을 Dictionary에 캐시 | WeakRef GC → ObjectDisposedException | 트리에서 fresh 수집 후 즉시 사용 |
| CSV 날짜를 `yyyy-MM-dd HH:mm`으로 | TimeLiner Field Selector 인식 불가 | `yyyy-MM-dd`만 사용 |
| CSV를 UTF-8로 저장 | Navisworks에서 한글 깨짐 | CP949 인코딩 사용 |
| TimeLiner "작업에 항목 연결" 규칙 | 이 워크플로우에서 동작하지 않음 | "작업 자동 추가 > 모든 세트에 대해" 사용 |

### 성능 관련

| 상황 | 대응 |
|------|------|
| 장시간 UI 스레드 작업 | `DoEvents()`를 주기적으로 호출 (5~10,000 단위) |
| 대규모 모델 (445K+ 속성) | AllProperties 내보내기 1~2분 소요, 정상 동작 |
| 334개 PipeRun Selection Set | 트리 1회 순회로 모든 Set 동시 수집 → 효율적 |

### 현재 한계

- Pipeline 객체만 4D 시뮬레이션 포함 (구조물, 장비는 별도 Set 필요)
- "작업 자동 추가" 단계는 Navisworks UI에서 수동 실행 필요
- PipeRun명이 Pipeline 내에서 중복되면 마지막 것만 남음 (현재 발생하지 않음)
