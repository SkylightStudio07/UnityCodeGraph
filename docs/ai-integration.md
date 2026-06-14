# AI Integration Plan

Unity Code Graph의 AI 기능은 기본 분석기를 대체하지 않는다. AI 키가 없거나
요청이 실패해도 기존 그래프 생성, 웹뷰어, 런처 기능은 그대로 동작해야 한다.

이 문서는 첫 AI 도입을 위한 최소 설계와 payload 형식을 정의한다.

## 목표

- 선택한 타입 노드에 대해 짧은 역할 설명을 생성한다.
- 선택한 시스템 클러스터에 대해 사람이 읽기 쉬운 요약을 생성한다.
- 선택한 노드나 시스템에 대해 코드 읽기 워크스루를 생성하고, 근거 노드/edge로
  그래프를 이동할 수 있게 한다.
- `Code Calls`, `Flow Trace`, `System Report`처럼 이미 추출된 구조 데이터를
  AI 입력으로 재사용한다.
- API key는 브라우저에 노출하지 않는다.
- AI 설명 결과는 캐시해서 같은 노드에 반복 비용이 들지 않게 한다.

## 비목표

- AI가 그래프를 생성하거나 수정하지 않는다.
- AI 결과를 신뢰 가능한 정적 분석 결과처럼 취급하지 않는다.
- 전체 소스 파일을 기본 payload로 보내지 않는다.
- API key를 browser localStorage, graph JSON, layout JSON에 저장하지 않는다.
- AI key가 없다는 이유로 UI 핵심 기능을 숨기거나 깨뜨리지 않는다.

## 기본 구조

```text
Web Viewer
  └─ POST /ai/explain-node
       로컬 런처 서버
         └─ AI provider API
```

웹뷰어는 API key를 알지 못한다. 런처 프로세스가 환경변수나 로컬 설정에서 key를
읽고, 로컬 서버 endpoint를 통해 AI 요청을 대리한다.

## 설정

환경변수와 런처 설정 화면을 모두 지원한다.

```text
OPENAI_API_KEY
OPENAI_MODEL      # optional, defaults to gpt-5.4-mini
OPENROUTER_API_KEY
OPENROUTER_MODEL
DEEPSEEK_API_KEY
DEEPSEEK_MODEL
```

웹뷰어 우측 상단 `AI` 설정 탭에서 provider, base URL, model, API key를 지정한다.
API key 원문은 브라우저에 저장하지 않고 런처 프로세스로만 전달한다. 사용자가
`Remember API key on this Windows user profile`을 켠 경우에는 런처가
`AppData\Local\UnityCodeGraph\ai-settings.json`에 Windows DPAPI(CurrentUser)로
암호화한 key를 저장한다.

## Provider 설정

웹뷰어 우측 상단 `AI` 설정 탭에서 provider, base URL, model을 지정한다.
provider/model/base URL은 브라우저 localStorage에 저장할 수 있지만, API key는
브라우저에 저장하지 않는다.

현재 지원 범위:

- `openai`: OpenAI Responses API (`/v1/responses`)
- `openrouter`: OpenRouter Chat Completions (`/chat/completions`)
- `deepseek`: DeepSeek Chat Completions (`/chat/completions`)
- `compatible`: OpenAI-compatible Chat Completions (`/chat/completions`)
- `ollama`: Ollama local chat API (`/api/chat`)
- `vertex`: Vertex AI Gemini `generateContent`. Project ID, location, service account JSON,
  또는 `GOOGLE_APPLICATION_CREDENTIALS`를 사용한다.
  `global` location은 `https://aiplatform.googleapis.com` endpoint를 사용하고,
  regional location은 `{location}-aiplatform.googleapis.com` endpoint를 사용한다.

런처 서버 endpoint:

- `GET /ai/config`: 현재 provider/model/base URL 상태 조회. API key 원문은 반환하지 않는다.
- `POST /ai/config`: provider/model/base URL과 API key를 설정한다. `saveApiKey: true`일
  때만 DPAPI 로컬 캐시에 저장한다.
- `GET /ai/models`: provider별 모델 후보를 반환한다. 가능하면 provider에서 실제 목록을 조회하고,
  실패하면 정적 추천 목록을 반환한다.

정적 추천 모델은 자주 바뀌므로, provider가 `/models` 또는 Ollama `/api/tags`를 지원하면
UI의 `Refresh` 결과를 우선한다. DeepSeek의 `deepseek-chat`, `deepseek-reasoner` 구형
별칭은 2026-07-24 폐기 예정이므로 기본 추천에서 제외하고 `deepseek-v4-flash`,
`deepseek-v4-pro`를 사용한다.

## Prompt Contract

이 문서의 설명은 한국어로 유지하되, 실제 AI provider에 전달하는 system prompt,
developer instruction, response schema는 영어로 작성한다. 영어 계약을 기준으로 두면
모델과 provider를 바꿀 때 흔들림이 적고, `language: "ko"` 같은 출력 언어만 payload로
제어하기 쉽다.

### System prompt draft

```text
You explain Unity/C# code graphs for developers.

Rules:
- Use only the supplied JSON payload and extracted examples.
- Do not invent files, methods, dependencies, or runtime behavior.
- If the graph evidence is weak or incomplete, say so explicitly.
- Keep the answer practical: describe responsibility, important touchpoints, and likely risks.
- Treat AI output as an interpretation of extracted graph data, not as a compiler-verified fact.
- Respond in the requested language.
```

### Node explanation instruction draft

```text
Explain the selected C# type node from this Unity code graph.

Return JSON only, matching this shape:
{
  "summary": "short paragraph",
  "responsibilities": ["..."],
  "touchpoints": ["..."],
  "risks": ["..."],
  "confidence": "low | medium | high",
  "disclaimer": "short limitation note"
}
```

## 런처 서버 Endpoint

### `GET /ai/status`

AI 사용 가능 여부를 반환한다.

Response:

```json
{
  "enabled": true,
  "provider": "openai",
  "reason": ""
}
```

키가 없을 때:

```json
{
  "enabled": false,
  "provider": "",
  "reason": "OPENAI_API_KEY is not set"
}
```

### `POST /ai/explain-node`

선택한 타입 노드의 요약을 생성한다.

첫 구현은 OpenAI Responses API를 사용하고, `text.format.type = "json_schema"`로
응답 형태를 제한한다. 브라우저는 `/ai/explain-node`에 그래프 payload만 보내며,
API key는 런처 프로세스의 환경변수에서만 읽는다.

Request:

```json
{
  "schemaVersion": 1,
  "language": "ko",
  "graph": {
    "rootPath": "H:/Unity/MyGame",
    "nodeCount": 180,
    "edgeCount": 640
  },
  "node": {
    "id": "Game.Battle.BattleManager",
    "name": "BattleManager",
    "namespace": "Game.Battle",
    "kind": "class",
    "isUnityType": true,
    "file": "Assets/Scripts/Battle/BattleManager.cs",
    "line": 12,
    "baseTypes": ["MonoBehaviour"],
    "attributes": []
  },
  "degree": {
    "incoming": 4,
    "outgoing": 12
  },
  "relationships": {
    "outgoing": [
      {
        "kind": "creates",
        "target": "Game.Battle.EnemyAction",
        "weight": 3,
        "example": "new EnemyAction(...)"
      }
    ],
    "incoming": [
      {
        "kind": "calls_member",
        "source": "Game.Map.MapNodeView",
        "weight": 1,
        "example": "battleManager.StartBattle(node)"
      }
    ]
  },
  "methods": [
    {
      "id": "Game.Battle.BattleManager.Start@42",
      "name": "Start",
      "signature": "Start()",
      "entryKind": "unity_lifecycle",
      "line": 42
    }
  ],
  "methodCalls": {
    "outgoing": [
      {
        "source": "StartBattle(MapNode)",
        "targetType": "Game.Battle.EnemyAction",
        "target": "Execute()",
        "weight": 2,
        "example": "action.Execute()"
      }
    ],
    "incoming": [
      {
        "sourceType": "Game.Map.MapNodeView",
        "source": "OnClick()",
        "target": "StartBattle(MapNode)",
        "weight": 1,
        "example": "battleManager.StartBattle(node)"
      }
    ]
  },
  "limits": {
    "maxSentRelationships": 12,
    "maxSentMethodCalls": 12,
    "maxSentExamples": 8
  }
}
```

Response:

```json
{
  "summary": "BattleManager는 전투 흐름을 시작하고 EnemyAction 실행을 조율하는 Unity 컴포넌트로 보입니다.",
  "responsibilities": [
    "전투 시작 진입점 관리",
    "맵 노드 또는 UI 이벤트에서 호출되는 전투 흐름 연결",
    "EnemyAction 생성 및 실행"
  ],
  "touchpoints": [
    "MapNodeView에서 StartBattle 호출",
    "EnemyAction 타입 생성 및 실행"
  ],
  "risks": [
    "생성 책임과 흐름 제어가 한 클래스에 모일 가능성이 있습니다."
  ],
  "confidence": "medium",
  "disclaimer": "AI summary is based on extracted graph data, not full semantic compilation."
}
```

### `POST /ai/explain-system`

선택한 시스템 클러스터의 요약을 생성한다. payload는 node 목록, entry method,
internal/external edges, keywords, system report 원본을 포함한다.

## 웹뷰어 UI

노드 선택 시 오른쪽 패널에 `AI Summary` 카드를 추가한다.

상태별 표시:

- AI 사용 불가: `AI summary unavailable`
- 키 있음, 아직 요청 전: `Explain Node`
- 요청 중: `Generating...`
- 성공: summary/responsibilities/touchpoints/risks 표시
- 실패: 짧은 에러와 retry 버튼

시스템 선택 시에는 `Explain System` 버튼을 제공한다.

## 캐시

브라우저 localStorage에는 AI 결과만 캐시한다. API key는 브라우저 캐시에는 절대
저장하지 않는다. 저장을 선택한 API key는 런처의 DPAPI 로컬 설정 파일에서만
관리한다.

키:

```text
UnityCodeGraph:ai:<graphStorageKey>:<nodeId>:<language>
UnityCodeGraph:ai-system:<graphStorageKey>:<systemId>:<language>
```

캐시 값:

```json
{
  "schemaVersion": 1,
  "createdAt": "2026-06-13T00:00:00Z",
  "model": "provider-default",
  "result": {
    "summary": "...",
    "responsibilities": []
  }
}
```

## Payload 축소 규칙

- 전체 노드 목록을 보내지 않는다.
- 선택한 노드, 직접 연결된 타입, 상위 method call 일부만 보낸다.
- example text는 각 edge/method edge당 첫 1개만 보낸다.
- 파일 경로는 기본적으로 그대로 보내되, 추후 옵션으로 프로젝트 루트 기준 상대
  경로만 보내도록 바꿀 수 있다.
- 소스 전문 전송은 별도 옵션으로 분리한다.

## 실패 처리

- `/ai/status`가 disabled면 AI 버튼은 비활성화한다.
- AI 요청 실패는 그래프 렌더링을 막지 않는다.
- timeout을 둔다.
- 실패 결과는 캐시하지 않는다.
- provider 응답이 JSON 형식이 아니면 fallback parser를 시도하지 않고 실패로
  처리한다.

## 구현 순서

1. 런처 서버에 `/ai/status` 추가
2. 웹뷰어에서 AI 사용 가능 상태 표시
3. 웹뷰어에서 node explain payload 생성
4. 런처 서버에 `/ai/explain-node` 추가
5. 결과 캐시 및 `AI Summary` 카드 렌더링
6. `/ai/explain-system` 추가
7. provider 설정 확장

## 구현 전 체크리스트

- API key가 없는 상태에서 기존 기능이 모두 동작하는가?
- 브라우저 개발자 도구에서 API key가 보이지 않는가?
- payload에 전체 소스가 들어가지 않는가?
- AI 실패 시 UI가 멈추지 않는가?
- 같은 노드 재요청 시 캐시가 사용되는가?
- export layout에 AI 캐시가 섞이지 않는가?
