# DXTnavis Documentation Index

> **Last Updated:** 2026-02-14
> **Current Version:** v1.5.0 (Spatial Connectivity Complete)
> **Next:** v1.6.0 (Phase 18: 3D Mesh GLB Export) 🚧 In Progress
> **Research Target:** EC3 2026 (Corfu), LDAC 2026 (Dubrovnik)

---

## Quick Navigation

| Category | Document | Description |
|----------|----------|-------------|
| **User Manual** | [USER-MANUAL.md](USER-MANUAL.md) | 전체 기능 사용 가이드 (v1.1.0) |
| **Pipeline 4D Guide** | [pipeline-4d-developer-guide](guides/pipeline-4d-developer-guide.md) | Pipeline 4D 코드 구조 및 동작 원리 |
| **Overview** | [CLAUDE.md](../CLAUDE.md) | Project context & quick ref |
| **Changelog** | [CHANGELOG.md](../CHANGELOG.md) | Version history |
| **README** | [README.md](../README.md) | Public documentation |

---

## Development Status

### Overall Progress
```
v1.6.0: [                    ]   0% (Phase 18: Mesh GLB Export) 🚧 CURRENT
v1.5.0: [====================] 100% (Phase 17: Spatial Connectivity) ✅
v1.4.0: [====================] 100% (Released 2026-02-06) ✅
v1.3.0: [====================] 100% (Released 2026-02-05) ✅
v1.2.0: [====================] 100% (Released 2026-01-21) ✅
v1.1.0: [====================] 100% (Released 2026-01-21)
v1.0.0: [====================] 100% (Released 2026-01-20)
```

### Phase Status

| Phase | Task | Status | Progress | Doc |
|-------|------|--------|----------|-----|
| 1 | CSV Property Export | ✅ Complete | 100% | [phase-1](phases/phase-1-csv-export.md) |
| 2 | Filtering & UI | ✅ Complete | 100% | [phase-2](phases/phase-2-filtering-ui.md) |
| 3 | 3D Object Integration | ✅ Complete | 100% | [phase-3](phases/phase-3-3d-integration.md) |
| 4 | 3D Snapshot | ✅ Complete | 100% | [phase-4](phases/phase-4-snapshot-workflow.md) |
| 5 | Data Validation | ✅ Complete | 100% | [phase-5](phases/phase-5-data-validation.md) |
| 6 | Code Quality | ✅ Complete | 100% | - |
| 7 | CSV Viewer | ✅ Complete | 100% | - |
| 8 | AWP 4D Automation | ✅ Complete | 100% | [phase-8](phases/phase-8-awp-4d-automation.md) |
| 9 | UI Enhancement | ✅ Complete | 100% | [phase-9](phases/phase-9-ui-enhancement.md) |
| 10 | Schedule Builder | ✅ Complete | 100% | [phase-10](phases/phase-10-refined-schedule-builder.md) |
| 11 | Object Grouping MVP | ✅ Complete | 100% | [phase-11](phases/phase-11-object-grouping.md) |
| 12 | Grouped Data Structure | ✅ Complete | 100% | [phase-12](phases/phase-12-grouped-data-structure.md) |
| **13** | **TimeLiner Enhancement** | ✅ Complete | 100% | [phase-13](phases/phase-13-timeliner-enhancement.md) |
| **14** | **Direct TimeLiner Exec** | ✅ Complete | 100% | [sprint-v1.1.0](agile/SPRINT-v1.1.0.md) |
| **15** | **Geometry Export System** | ✅ Complete | 100% | [phase-15](phases/phase-15-geometry-export.md) |
| **16** | **Unified CSV Export** | ✅ Complete | 100% | - |
| **17** | **Spatial Connectivity** | ✅ Complete | 100% | [phase-17](phases/phase-17-spatial-connectivity.md) |
| **18** | **3D Mesh GLB Export** | 🚧 In Progress | 60% | [phase-18](phases/phase-18-mesh-glb-export.md) |

---

## v1.6.0 In Progress: 3D Mesh GLB Export (Phase 18) 🚧 NEW!

### 핵심 목표
NWD에서 개별 객체 3D Mesh → GLB 파일 추출 → bim-ontology Dashboard 3D 시각화

### 구현 상황
| 파일 | 설명 | 상태 |
|------|------|------|
| `Services/Geometry/GeometryExtractor.cs` | LastModelItemMap 프로퍼티 | ✅ Done |
| `Services/Geometry/MeshExtractor.cs` | GLB min/max bounds 추가 | ✅ Done |
| `Models/Geometry/GeometryRecord.cs` | MeshUri 하이픈 UUID | ✅ Done |
| `ViewModels/DXwindowViewModel.Export.cs` | 5-stage Pipeline + Test Mesh | ✅ Done |
| `Views/DXwindow.xaml` | Test Mesh 버튼 | ✅ Done |

### Key Documents
- **Phase Doc**: [Phase 18: Mesh GLB Export](phases/phase-18-mesh-glb-export.md)
- **Source Strategy**: [bim-ontology/dxtnavis-mesh-strategy](../../../bim-ontology/docs/dxtnavis-mesh-strategy.md)
- **Mesh Analysis**: [mesh-data-storage-analysis](tech-specs/mesh-data-storage-analysis.md)

---

## v1.5.0 Complete: Spatial Connectivity & Adjacency (Phase 17) ✅

### 핵심 성과
- BBox 기반 인접성 검출 (Brute Force + Spatial Hash Grid)
- Union-Find 연결 컴포넌트 탐색
- adjacency.csv + connected_groups.csv + spatial_relationships.ttl 출력
- inst:navis_ URI 패턴으로 bim-ontology 호환

---

## v1.2.0 Completed: Direct TimeLiner Execution ✅

### Features Released (2026-01-21)
- [x] **직접 TimeLiner 실행**: CSV 없이 1클릭으로 TimeLiner 연결
- [x] **DryRun 미리보기**: 실행 전 결과 확인
- [x] **진행률 표시**: 실시간 진행 상태 (ProgressBar)
- [x] **완전 자동화**: Schedule Builder → TimeLiner 원클릭 연결

### Key Documents
- **Sprint Doc**: [SPRINT-v1.1.0](agile/SPRINT-v1.1.0.md)
- **Phase Doc**: [Phase 13: TimeLiner Enhancement](phases/phase-13-timeliner-enhancement.md)

---

## v1.1.0 Completed: TimeLiner Enhancement (Phase 1)

### Features Released (2026-01-21)
- [x] **TaskType 한글화**: 구성/철거/임시 (UI) → Construct/Demolish/Temporary (API)
- [x] **DateMode 옵션**: PlannedOnly, ActualFromPlanned(권장), BothSeparate
- [x] **확장 ParentSet 전략**: 7가지 (ByLevel, ByFloorLevel, ByCategory, ByArea, Composite, ByProperty, Custom)
- [x] **CSV ActualStart/ActualEnd 컬럼**: DateMode에 따른 자동 생성

### Key Documents
- **Sprint Doc**: [SPRINT-v1.1.0](agile/SPRINT-v1.1.0.md)
- **Phase Doc**: [Phase 13: TimeLiner Enhancement](phases/phase-13-timeliner-enhancement.md)

---

## v0.9.0 Completed: Object Grouping MVP

### Features Released (2026-01-20)
- [x] 객체별 그룹화 보기 (Expander UI)
- [x] Flat/Grouped Mode 전환 토글
- [x] 그룹 선택 시 하위 속성 전체 선택
- [x] 10K 미만 필터링 데이터에서만 활성화
- [x] BoolToVisibilityConverter Invert 파라미터 지원

### Key Documents
- **Phase Doc**: [Phase 11: Object Grouping](phases/phase-11-object-grouping.md)
- **Previous**: [Phase 10: Schedule Builder](phases/phase-10-refined-schedule-builder.md)

---

## v0.8.0 Completed: Schedule Builder

### Features Released (2026-01-19)
- [x] Schedule CSV 자동 생성
- [x] Task 설정 (이름, 유형, 기간, 시작일)
- [x] ParentSet 전략 (ByLevel, ByProperty, Custom)
- [x] 미리보기 DataGrid
- [x] Schedule 탭 UI 추가

### Key Documents
- **Phase Doc**: [Phase 10: Schedule Builder](phases/phase-10-refined-schedule-builder.md)
- **Previous**: [Phase 9: UI Enhancement](phases/phase-9-ui-enhancement.md)

---

## v0.7.0 Completed: UI Enhancement (Select All)

### Features Released (2026-01-19)
- [x] Select All 체크박스 (전체 선택/해제)
- [x] SelectedPropertiesCount 실시간 업데이트
- [x] 문서 구조 정리 (progress → _archive)

### Key Documents
- **Sprint Doc**: [SPRINT-v0.7.0](agile/SPRINT-v0.7.0.md)
- **Phase Doc**: [Phase 9: UI Enhancement](phases/phase-9-ui-enhancement.md)

---

## v0.6.0 Completed: AWP 4D Automation

### Features Released (2026-01-11)
- [x] CSV → TimeLiner 자동 연결 파이프라인
- [x] Property Write (ComAPI SetUserDefined)
- [x] Selection Set 계층 구조 자동 생성
- [x] TimeLiner Task 자동 생성 및 Set 연결
- [x] AWP 4D 탭 UI 통합

### Key Documents
- **Tech Spec**: [AWP-4D-Automation-Spec.md](tech-specs/AWP-4D-Automation-Spec.md)
- **ADR-001**: [ComAPI Property Write](adr/ADR-001-ComAPI-Property-Write.md)
- **ADR-002**: [TimeLiner API Integration](adr/ADR-002-TimeLiner-API-Integration.md)

---

## Document Structure

```
docs/
├── _INDEX.md                    # This file
├── agile/                       # Agile tracking
│   ├── SPRINT-CURRENT.md
│   ├── SPRINT-v0.4.0.md
│   ├── SPRINT-v0.4.1.md
│   ├── SPRINT-v0.4.2.md
│   └── SPRINT-v0.5.0.md
├── adr/                         # Architecture Decision Records
│   ├── ADR-001-ComAPI-Property-Write.md      # ✅ Accepted
│   └── ADR-002-TimeLiner-API-Integration.md  # ✅ Accepted (NEW)
├── phases/                      # Phase documentation
│   ├── phase-1-csv-export.md
│   ├── phase-2-filtering-ui.md
│   ├── phase-3-3d-integration.md
│   ├── phase-4-snapshot-workflow.md
│   ├── phase-5-data-validation.md
│   ├── phase-8-awp-4d-automation.md
│   ├── phase-9-ui-enhancement.md        # v0.7.0 In Progress
│   └── phase-10-refined-schedule-builder.md  # v0.8.0 Planned
├── prd/                         # Product Requirements
│   ├── 3d-snapshot-workflow-prd.md
│   └── v0.4.0-feature-expansion-prd.md
├── _archive/                    # Archived documents
│   └── progress/                # (Deprecated - see CHANGELOG.md)
└── tech-specs/                  # Technical Specifications
    ├── 3d-snapshot-workflow-spec.md
    ├── v0.4.0-tech-spec.md
    └── AWP-4D-Automation-Spec.md  # NEW
```

---

## Key Implementation Files

### Current Services (v0.6.0)
| File | Purpose | Phase | Status |
|------|---------|-------|--------|
| NavisworksDataExtractor.cs | Property extraction | P1 | ✅ |
| NavisworksSelectionService.cs | 3D selection/visibility | P3 | ✅ |
| HierarchyFileWriter.cs | Hierarchy CSV/JSON | P1 | ✅ |
| PropertyFileWriter.cs | Property CSV | P1 | ✅ |
| SnapshotService.cs | Image capture, ViewPoint | P4 | ✅ |
| DisplayStringParser.cs | VariantData parsing | P4 | ✅ |
| PropertyWriteService.cs | ComAPI Property Write | P8 | ✅ |
| SelectionSetService.cs | Selection Set 생성 | P8 | ✅ |
| TimeLinerService.cs | TimeLiner Task 생성 | P8 | ✅ |
| AWP4DAutomationService.cs | 통합 파이프라인 | P8 | ✅ |
| ObjectMatcher.cs | SyncID → ModelItem 매칭 | P8 | ✅ |
| AWP4DValidator.cs | Pre/Post 검증 | P8 | ✅ |
| ScheduleCsvParser.cs | 한영 컬럼 매핑 CSV 파싱 | P8 | ✅ |

### Planned Changes (v0.7.0)
| File | Purpose | Phase | Status |
|------|---------|-------|--------|
| DXwindow.xaml | Select All, GroupStyle 추가 | P9 | 📋 |
| DXwindowViewModel.cs | SelectAllCommand 구현 | P9 | 📋 |
| TestSchedule.csv | AWP 4D 테스트 샘플 | P9 | 📋 |

### ViewModels (Partial Class Pattern)
| File | Lines | Key Features |
|------|-------|--------------|
| DXwindowViewModel.cs | 1020 | Core VM |
| DXwindowViewModel.Filter.cs | 144 | Filter logic |
| DXwindowViewModel.Search.cs | 110 | Search logic |
| DXwindowViewModel.Selection.cs | 219 | 3D selection |
| DXwindowViewModel.Snapshot.cs | 311 | Snapshot/ViewPoint |
| DXwindowViewModel.Tree.cs | 181 | Tree expand/collapse |
| DXwindowViewModel.Export.cs | 397 | CSV export |
| CsvViewerViewModel.cs | - | CSV Viewer (v0.5.0) |
| AWP4DViewModel.cs | - | AWP 4D Automation (v0.6.0) |

---

## Architecture Decision Records (ADR)

| ADR | Title | Status | Date |
|-----|-------|--------|------|
| [ADR-001](adr/ADR-001-ComAPI-Property-Write.md) | ComAPI를 통한 Custom Property Write | ✅ Accepted | 2026-01-09 |
| [ADR-002](adr/ADR-002-TimeLiner-API-Integration.md) | TimeLiner API를 통한 4D 자동화 | ✅ Accepted | 2026-01-11 |

---

## Known Issues

| Issue | Priority | Phase | Status |
|-------|----------|-------|--------|
| ~~검색창 영어 입력 불가~~ | ~~Critical~~ | P2 | ✅ Fixed (v0.4.0) |
| ~~ViewPoint 저장 read-only~~ | ~~Critical~~ | P4 | ✅ Fixed (v0.4.3) |

---

## Version History

| Version | Date | Key Features |
|---------|------|--------------|
| **v1.5.0** | 🚧 WIP | Phase 17: Spatial Connectivity & Adjacency Export |
| **v1.4.0** | 2026-02-06 | Phase 15: Geometry Export (BBox + Mesh + RDF) |
| **v1.3.0** | 2026-02-05 | Synthetic ID Generation for Hierarchy Preservation |
| **v1.2.0** | 2026-01-21 | Direct TimeLiner Execution (1-click) |
| **v1.1.0** | 2026-01-21 | TaskType 한글화, DateMode 옵션, 확장 ParentSet (7가지) |
| **v1.0.0** | 2026-01-20 | Grouped Data Structure (445K→5K 최적화) |
| v0.9.0 | 2026-01-20 | Object Grouping MVP, Expander UI |
| v0.8.0 | 2026-01-19 | Schedule Builder, ParentSet 전략 |
| v0.7.0 | 2026-01-19 | UI Enhancement, Select All |
| v0.6.0 | 2026-01-11 | AWP 4D Automation, TimeLiner 연동, Property Write |
| v0.5.0 | 2026-01-09 | ViewModel 리팩토링, CSV Viewer, ComAPI Research |
| v0.4.3 | 2026-01-09 | 필터 자동 적용, Show Only 토글, ViewPoint 저장 수정 |
| v0.4.2 | 2026-01-09 | Unit 컬럼, AccessViolation 처리 |
| v0.4.1 | 2026-01-08 | 트리 계층 구조 수정 |
| v0.4.0 | 2026-01-08 | 검색창 수정, CSV 4종, DisplayString 파싱 |
| v0.3.0 | 2026-01-06 | Tree expand/collapse |
| v0.2.0 | 2026-01-05 | 3D selection, visibility |
| v0.1.0 | 2026-01-03 | Level filter, SysPath filter |
| v0.0.1 | 2025-12-29 | Initial setup |

---

## Quick Links

### v1.1.0 Documents (TimeLiner Enhancement)
- [User Manual v1.1.0](USER-MANUAL.md)
- [Sprint v1.1.0](agile/SPRINT-v1.1.0.md)
- [Phase 13: TimeLiner Enhancement](phases/phase-13-timeliner-enhancement.md)

### v1.0.0 Documents (Grouped Data Structure)
- [Phase 12: Grouped Data Structure](phases/phase-12-grouped-data-structure.md)
- [Phase 11: Object Grouping](phases/phase-11-object-grouping.md)

### v0.6.0 Documents (AWP 4D)
- [Phase 8: AWP 4D Automation](phases/phase-8-awp-4d-automation.md)
- [Tech Spec: AWP 4D Automation](tech-specs/AWP-4D-Automation-Spec.md)
- [ADR-002: TimeLiner API](adr/ADR-002-TimeLiner-API-Integration.md)
- [ADR-001: ComAPI Property Write](adr/ADR-001-ComAPI-Property-Write.md)

### General
- [Changelog](../CHANGELOG.md)
- [CLAUDE.md](../CLAUDE.md)
- [GitHub](https://github.com/tygwan/DXTnavis)
