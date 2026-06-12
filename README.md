<p align="center">
  <img src="docs/assets/ucg-mark.svg" width="118" alt="Unity Code Graph mark" />
</p>

<h1 align="center">Unity Code Graph</h1>

<p align="center">
  <strong>Unity/C#을 위한, 타입 및 호출 흐름 시각화 로컬 분석 도구</strong>
  <br />
  <em>당신의 프로젝트를 위한 스탠드얼론 도구</em>
</p>

<p align="center">
  <a href="README.md"><strong>한국어</strong></a>
  ·
  <a href="README.en.md">English</a>
</p>

<p align="center">
  <img alt=".NET 9" src="https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet&logoColor=white" />
  <img alt="Windows" src="https://img.shields.io/badge/Windows-Launcher-0078D4?logo=windows&logoColor=white" />
  <img alt="Unity" src="https://img.shields.io/badge/Unity-C%23%20Graph-111111?logo=unity&logoColor=white" />
  <img alt="Roslyn" src="https://img.shields.io/badge/Roslyn-Syntax%20Analysis-8dccff" />
  <img alt="Local first" src="https://img.shields.io/badge/Local--first-No%20Upload-24754f" />
  <img alt="Preview" src="https://img.shields.io/badge/status-preview-f0b429" />
</p>

<p align="center">
  <a href="#빠른-시작">빠른 시작</a>
  ·
  <a href="#런처">런처</a>
  ·
  <a href="#웹-뷰어">웹 뷰어</a>
  ·
  <a href="#빌드와-퍼블리시">빌드</a>
  ·
  <a href="#분석-검증">검증</a>
</p>

---

## 미리보기

![런처](docs/screenshots/launcher.png)

![웹 뷰어](docs/screenshots/web-viewer.png)

## 이것은 무엇인가요?

Unity Code Graph는 Unity 프로젝트나 일반 C# 폴더를 스캔해서 `.cs` 파일 안의
타입 관계와 메서드 호출 관계를 JSON 그래프로 만듭니다. 생성된 그래프는 포함된
웹 뷰어에서 열 수 있고, 노드 위치를 직접 정리하거나 시스템 단위로 접어볼 수
있습니다.

현재 분석 패스는 의도적으로 AI를 사용하지 않습니다. C# 문법에서 기계적으로
확인 가능한 관계를 추출하기 때문에 결과가 로컬에서 반복 가능하고 설명 가능한
형태로 유지됩니다.

## 주요 기능

| 영역 | 기능 |
| --- | --- |
| 코드 스캔 | 로컬 Unity 프로젝트, 일반 C# 폴더, 공개 Git 저장소 |
| 타입 그래프 | 클래스, 구조체, 레코드, 인터페이스, enum 노드 |
| 관계 추출 | 상속, 구현, 필드, 프로퍼티, 파라미터, 지역 변수, 객체 생성, 캐스트, 타입 체크, 어트리뷰트 |
| Unity 패턴 | `GetComponent<T>()`, `AddComponent<T>()`, `FindObjectOfType<T>()`, `CreateInstance<T>()` |
| 호출 그래프 | 문법상 해석 가능한 메서드 호출과 타입 간 호출 요약 |
| 웹 뷰어 | 시스템 클러스터, 시스템 리포트, 플로우 트레이스, Code Calls |
| 레이아웃 | 노드 위치 저장, `Export Layout`, `Import Layout` |
| 런처 | WebView2 GUI, 최근 프로젝트, watch 모드, 내장 로컬 서버 |

## 요구 사항

- Windows
- .NET 9 SDK
- WebView2 Runtime
- Node.js: 단축 빌드의 JavaScript 문법 체크와 선택적 정적 서버 실행에 사용

## 빠른 시작
- 최신 릴리즈는 이 쪽으로.  
https://github.com/SkylightStudio07/UnityCodeGraph/releases
---

전체 빌드와 JavaScript 문법 체크:

```powershell
.\build.bat
```

Unity 프로젝트에서 그래프 생성:

```powershell
dotnet run --project .\UnityCodeGraph -- <Unity 프로젝트 경로> --roots Scripts,Source --output graph.json
```

코드 폴더를 직접 지정해서 생성:

```powershell
dotnet run --project .\UnityCodeGraph -- <Unity 프로젝트 경로>\Assets\Scripts --output graph.json
```

`.cs` 파일 변경 시 자동 재생성:

```powershell
dotnet run --project .\UnityCodeGraph -- <Unity 프로젝트 경로> --roots Scripts,Source --watch --output graph.json
```

## 런처

![런처](docs/screenshots/launcher.png)

소스에서 런처 실행:

```powershell
dotnet run --project .\UnityCodeGraph.Launcher
```

혹은 이미 빌드되어 있는 런처를 사용하려면:

```powershell
UnityCodeGraphLauncher.exe
```

런처에서 할 수 있는 일:

- Unity 프로젝트 폴더 선택
- 공개 Git 저장소 clone
- `Scripts,Source` 같은 코드 폴더 이름 지정
- 한 번 생성 또는 watch 모드 시작
- 최근 프로젝트 다시 열기
- 그래프 캔버스를 브라우저에서 열기

`Open Canvas`는 런처 내장 로컬 정적 서버를 시작합니다. 설정된 `Output JSON`
파일이 존재하면 캔버스가 자동으로 해당 그래프를 불러옵니다.

## 웹 뷰어

![웹 뷰어](docs/screenshots/web-viewer.png)

가장 쉬운 방법은 런처의 `Open Canvas` 버튼으로 접근하는 방법입니다.

직접 열고 싶다면 workspace 루트를 정적 서버로 띄웁니다:

```powershell
dotnet run --project .\UnityCodeGraph -- .\samples\MiniUnityStyle --output .\samples\mini-graph.json
node .\tools\static-server.mjs 5173 .
```

그 다음 브라우저에서 엽니다:

```text
http://localhost:5173/web/
```

### 웹 뷰어 사용법:

![웹 뷰어 노드](docs/screenshots/web-viewer-pinned-node.png)

- `Load JSON`으로 이전에 런처에서 생성한 그래프 파일 열기
- `Type View`로 타입 단위 관계 확인
- `System View`로 시스템 카드 단위 확인
- `Pin View`로 선택된 관계 뷰를 유지한 채 노드 위치 재배치
- `Export Layout` / `Import Layout`로 노드 위치, 필터, 뷰 모드, 줌 상태 이동
- 노드를 선택해 상세 정보, 예시, 코드 호출 요약, 플로우 트레이스 확인

## 빌드와 퍼블리시

빠른 Debug 빌드:

```powershell
.\build.bat
```

Release 빌드 체크:

```powershell
.\build.bat -Release
```

Windows 번들과 zip 생성:

```powershell
.\build.bat -Release -Publish -Zip
```

생성 위치:

```text
dist\UnityCodeGraph-win-x64\
dist\UnityCodeGraph-win-x64.zip
```

퍼블리시된 런처 실행:

```powershell
.\dist\UnityCodeGraph-win-x64\UnityCodeGraphLauncher.exe
```

퍼블리시된 CLI 실행:

```powershell
.\dist\UnityCodeGraph-win-x64\UnityCodeGraph.exe <Unity 프로젝트 경로> --roots Scripts,Source --output graph.json
```

대상 PC에 맞는 .NET 런타임이 없어도 실행 가능한 더 큰 패키지 생성:

```powershell
.\build.bat -Release -Publish -Zip -SelfContained
```


## 분석 검증

파서 회귀 검증 fixture 실행:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\verify-analysis.ps1
```

이 fixture는 using 기반 타입 해석, 타입 제약, Unity 제네릭 호출, 캐스트와
타입 체크, 정적 호출, 메서드 호출 간선, 중복 타입명 오탐 방지를 확인합니다.

## 추출 관계

```text
inherits                         implements
has_attribute                    member_attribute
attribute_type_argument          type_constraint
has_field_type                   has_property_type
has_event_type                   returns
accepts_parameter                uses_local_type
creates                          typeof
casts_to                         type_check
calls_member                     unity_get_component
unity_try_get_component          unity_add_component
unity_find_object                unity_create_scriptable_object
```






## 상태

이 프로젝트는 현재 preview 단계입니다. 분석은 정적 문법 기반이라 빠르고
반복 가능하지만, 컴파일러 semantic model을 사용하는 완전한 타입 해석기는 아직
아닙니다.
