# AI Spec Pipeline — Architecture

비정형 요구사항을 intake → spec → jira/qa/design → code-analysis → patch 순서로 자동화하는 로컬 툴.

---

## 1. 전체 시스템 구조

```mermaid
graph TB
    subgraph Frontend["Frontend (React/Vite :5173)"]
        UI[KanbanBoard + SourcePanel]
        API_CLIENT[api.ts — fetch + SSE]
    end

    subgraph Backend[".NET 10 API (:5001)"]
        RUN[RunController]
        PB[PromptBuilder]
        JOB[JobRegistry]
        PII[PiiTokenizer]

        subgraph Runners["CLI Runners"]
            CLAUDE_CLI[ClaudeCliRunner\n로컬 claude CLI]
            CLAUDE_VTX[ClaudeVertexRunner\nVertex AI Claude]
            GEMINI_VTX[GeminiVertexRunner\nVertex AI Gemini]
        end

        subgraph Services["External Services"]
            GH[GitHubService]
            JIRA[JiraService]
            SEARCH[RepoSearchService]
            WS[WorkspaceManager]
        end
    end

    subgraph Storage["로컬 스토리지"]
        PROMPTS[prompts/]
        WORKSPACES[workspaces/local/\ndate-id/]
        LS[localStorage\n세션 캐시]
    end

    subgraph External["외부 서비스"]
        CLAUDE_API[Claude CLI\nHaiku 4.5]
        VERTEX[Vertex AI\nSonnet 4.6 / Gemini]
        GITHUB[GitHub REST API]
        JIRA_API[Jira REST API]
    end

    UI --> API_CLIENT
    API_CLIENT -->|POST /api/run/stream/:stage\nSSE| RUN
    API_CLIENT -->|GET /api/history| WS
    API_CLIENT -->|POST /api/jira/create| JIRA
    API_CLIENT -->|POST /api/github/push| GH

    RUN --> PII
    RUN --> PB
    RUN --> JOB
    RUN --> SEARCH
    PB -->|base.system.md + policy.md\n+ SKILL.md + input| PROMPTS

    RUN -->|Runner 선택| CLAUDE_CLI
    RUN -->|Vertex.ProjectId 설정 시| CLAUDE_VTX
    RUN -->|Provider=gemini| GEMINI_VTX

    CLAUDE_CLI --> CLAUDE_API
    CLAUDE_VTX --> VERTEX
    GEMINI_VTX --> VERTEX

    SEARCH --> GITHUB
    GH --> GITHUB
    JIRA --> JIRA_API

    WS --> WORKSPACES
    LS -.->|세션 복원| UI
```

---

## 2. 파이프라인 스테이지 흐름

```mermaid
flowchart LR
    INPUT([비정형 입력\nSlack/메모/이슈]) --> INTAKE

    INTAKE[intake\nHaiku 4.5\n문제 정의 + Q&A]
    INTAKE -->|Q&A 답변 확정| SPEC

    SPEC[spec\nSonnet 4.6\nSSOT 통합 스펙]

    SPEC --> JIRA
    SPEC --> QA
    SPEC --> DESIGN
    SPEC --> CODE_ANALYSIS

    JIRA[jira\nHaiku 4.5\nJSON 티켓]
    QA[qa\nSonnet 4.6\n테스트 케이스]
    DESIGN[design\nHaiku 4.5\nDesign Package v1]
    CODE_ANALYSIS[code-analysis\nSonnet 4.6\nGitHub 코드 분석]

    CODE_ANALYSIS --> PATCH
    PATCH[patch\nSonnet 4.6\nJSON 파일 패치]

    PATCH --> PUSH[GitHub\nBranch Push]
    PATCH --> PR[GitHub\nDraft PR]
    JIRA --> JIRA_TICKET[Jira 티켓 생성\n+ Remote Link]

    style SPEC fill:#4a9eff,color:#fff
    style INTAKE fill:#6c757d,color:#fff
    style JIRA fill:#6c757d,color:#fff
    style QA fill:#4a9eff,color:#fff
    style DESIGN fill:#6c757d,color:#fff
    style CODE_ANALYSIS fill:#4a9eff,color:#fff
    style PATCH fill:#4a9eff,color:#fff
```

---

## 3. 백엔드 레이어 구조

```mermaid
graph TD
    subgraph Controllers
        RC[RunController\n/api/run]
        GHC[GitHubController\n/api/github]
        JC[JiraController\n/api/jira]
        KC[KnowledgeController\n/api/knowledge]
        SC[SettingsController\n/api/settings]
    end

    subgraph Application["Application (CQRS)"]
        CMD[RunStageCommand]
        HDL[RunStageHandler]
        RES[RunStageResult]
    end

    subgraph Infrastructure
        PB[PromptBuilder\n프롬프트 조립]
        PII2[PiiTokenizer\nPII 마스킹]
        RSS[RepoSearchService\n병렬 코드 검색]
        GHS[GitHubService\nGitHub REST]
        JS[JiraService\nJira REST]
        SS[SettingsService\nJSON 설정]
        JR[JobRegistry\n인메모리 Job 추적]
    end

    subgraph Domain
        JOB2[Job\nID/Status/Path]
        STATUS[JobStatus\nQueued/Running/Done/Failed]
    end

    subgraph Workspace
        WM[WorkspaceManager\n디렉토리 생성/조회/삭제]
        WL[WorkspaceLayout\n경로 헬퍼]
    end

    RC --> CMD --> HDL
    HDL --> PII2
    HDL --> PB
    HDL --> RSS
    HDL --> JR
    HDL --> WM
    GHC --> GHS
    JC --> JS
    SC --> SS
```

---

## 4. SSE 스트리밍 요청 시퀀스

```mermaid
sequenceDiagram
    participant Browser
    participant RunController
    participant PiiTokenizer
    participant RepoSearch
    participant PromptBuilder
    participant ICliRunner
    participant LLM as Claude/Vertex AI

    Browser->>RunController: POST /api/run/stream/{stage}\n{InputText, AllOutputs}
    RunController->>PiiTokenizer: Tokenize(inputText)
    PiiTokenizer-->>RunController: (maskedText, piiMap)

    alt code-analysis 또는 patch 스테이지
        RunController->>RepoSearch: SearchAsync(FE/BE URLs, keywords)
        RepoSearch-->>RunController: GitHub 파일 컨텍스트 (60KB 예산)
    end

    RunController->>PromptBuilder: BuildAsync(profile, maskedText)
    PromptBuilder-->>RunController: base.system + [policy] + SKILL + template + input

    RunController->>ICliRunner: StreamAsync(prompt, onChunk)
    ICliRunner->>LLM: claude CLI / Vertex AI HTTP
    loop SSE 청크
        LLM-->>ICliRunner: 텍스트 청크
        ICliRunner-->>RunController: onChunk(text)
        RunController-->>Browser: SSE data: {chunk}
    end

    RunController->>PiiTokenizer: Detokenize(output, piiMap)
    RunController->>RunController: StripCodeFence + RunVerifyScript
    RunController->>WorkspaceManager: Save out/{stage}.{ext}
    RunController-->>Browser: SSE data: {done:true, output, tokens}
```

---

## 5. 프롬프트 조립 구조

```mermaid
graph LR
    subgraph 조립 순서
        A[base.system.md\n전역 지시사항]
        B[policy.md\n비즈니스 정책\nspec/jira 스테이지만]
        C[SKILL.md\n에이전트 역할 + 출력 포맷]
        D[template.md\n출력 스키마 참조]
        E[Input Text\n사용자 데이터\n+ 상위 스테이지 출력]
    end

    A --> PROMPT[최종 프롬프트]
    B -.->|조건부| PROMPT
    C --> PROMPT
    D --> PROMPT
    E --> PROMPT

    PROMPT --> LLM_OUT[LLM 출력]
```

---

## 6. 워크스페이스 디렉토리 구조

```mermaid
graph TD
    WS[workspaces/local/]
    SESSION[date-guid/]
    INPUT[input.txt]
    PROMPT_FILE[prompt.md]
    OUT[out/]
    LOGS[logs/]

    INTAKE_MD[intake.md]
    SPEC_MD[spec.md]
    JIRA_JSON[jira.json]
    QA_MD[qa.md]
    DESIGN_JSON[design.json]
    DESIGN_HTML[design.html]
    CA_MD[code-analysis.md]
    PATCH_JSON[patch.json]

    LOG_FILE[run.log]
    META[meta.json\n토큰/소요시간]

    WS --> SESSION
    SESSION --> INPUT
    SESSION --> PROMPT_FILE
    SESSION --> OUT
    SESSION --> LOGS
    OUT --> INTAKE_MD
    OUT --> SPEC_MD
    OUT --> JIRA_JSON
    OUT --> QA_MD
    OUT --> DESIGN_JSON
    OUT --> DESIGN_HTML
    OUT --> CA_MD
    OUT --> PATCH_JSON
    LOGS --> LOG_FILE
    LOGS --> META
```

---

## 7. Runner 선택 로직 (DI)

```mermaid
flowchart TD
    START([Program.cs DI 등록]) --> CHECK_VERTEX{Vertex:ProjectId\n설정됨?}
    CHECK_VERTEX -->|No| LOCAL[ClaudeCliRunner\nclaude CLI 로컬 실행\nHaiku 4.5]
    CHECK_VERTEX -->|Yes| CHECK_PROVIDER{Vertex:Provider}
    CHECK_PROVIDER -->|gemini| GEMINI[GeminiVertexRunner\nVertex AI Gemini]
    CHECK_PROVIDER -->|claude 또는 기본| CLAUDE_V[ClaudeVertexRunner\nVertex AI Claude Sonnet 4.6]
```

---

## 스테이지별 모델 / 입출력 요약

| 스테이지 | 기본 모델 | 입력 | 출력 형식 |
|---------|---------|------|---------|
| intake | Haiku 4.5 | 비정형 텍스트 | Markdown (문제 정의 + Q&A) |
| spec | Sonnet 4.6 | intake + decisions | Markdown (SSOT 스펙) |
| jira | Haiku 4.5 | spec | JSON |
| qa | Sonnet 4.6 | spec | Markdown (테스트 케이스) |
| design | Haiku 4.5 | spec | JSON (Design Package v1) |
| code-analysis | Sonnet 4.6 | spec + GitHub 코드 | Markdown |
| patch | Sonnet 4.6 | code-analysis + spec | JSON 배열 |
| policy-update | Sonnet 4.6 | policy + 새 결정사항 | Markdown |
