# SkyBoard — 팀 게시판 / 프로젝트 관리 (Sky 메인 데모 Case B)

이미지를 붙일 수 있는 팀 게시판과 프로젝트·할 일 관리 웹앱이다. **React + FastAPI + SQLite + 로컬 업로드.**
화면(React)과 API(FastAPI)는 **서로 다른 구성 요소**다. Sky가 각각을 판단해 배치하는 장면(Full-stack)을 보여 주는 메인 데모다.

| 구성 요소 | 지금 (로컬) | Sky 판단 | AWS | 온프레미스 |
| --- | --- | --- | --- | --- |
| 화면 `frontend/` → `static/` | React SPA (Vite 빌드) | Static | S3 + CloudFront | Nginx |
| API `backend/` | FastAPI | Container | ECS | Docker |
| DB | `data/board.db` (SQLite) | 영속 관계형 DB | RDS PostgreSQL | PostgreSQL 또는 볼륨 |
| 이미지 | `data/uploads/` | Object storage | S3 (`S3_BUCKET`) | Volume (`UPLOAD_DIR`) |

| 기능 | 내용 |
| --- | --- |
| 들어가기 | 이름과 비밀번호만 넣는다. 처음 쓰는 이름이면 그 자리에서 계정이 생긴다(이름 1~30자, 비밀번호 1자 이상). 샘플 계정: `mina` / `demo`, `jun` / `demo` |
| 게시글 | 목록(페이지), 보기, 쓰기, 고치기, 지우기. 고치기·지우기는 작성자만 |
| 이미지 | 글 하나에 최대 4장, 한 장 5MB. PNG·JPEG·GIF·WebP만(파일 내용으로 판별, SVG 거부) |
| 프로젝트·할 일 | 프로젝트 만들기(이름 변경·삭제는 만든 사람만), 할 일 추가·담당자·마감일·상태(할 일 → 진행 중 → 완료)·삭제 |
| 검색 | 상단 검색창 하나로 글, 프로젝트, 할 일을 함께 찾기 |

## 폴더

```
web/
  backend/                 FastAPI (Python 3.13). API만 제공(화면은 별도 구성 요소)
    main.py                app = FastAPI(...); /api/*, /uploads/*, /health
    Dockerfile             배포용 이미지 (PORT 환경 변수로 실행)
    requirements.txt       fastapi, uvicorn, python-multipart, boto3
    board/schema.sql       테이블 (시작할 때 IF NOT EXISTS로 생성)
    board/db.py            SQLite 연결, 새 DB면 seed 복사
    board/storage.py       이미지 저장: 로컬 폴더(기본) 또는 S3 버킷(S3_BUCKET)
    board/security.py      비밀번호 해시, 로그인 토큰
    board/schemas.py       입력 검사 규칙
    board/routes/          auth, posts, images, projects, search
    seed/board.db          샘플 데이터 (SQLite 파일 1개)
    seed.py                seed/board.db를 다시 만드는 스크립트
    tests/                 pytest 16개 (S3는 moto 가짜 S3로 검사)
  frontend/                화면 소스 (React + Vite). npm run build → ../static
  static/                  빌드된 화면 (git에 포함). config.json에 배포된 API 주소를 넣는다
SkyBoard-fullstack.zip          Sky 업로드용 (메인): frontend/ + static/ + backend/
SkyBoard-single-server.zip      예비용: API가 화면까지 함께 제공하는 컨테이너 하나
Tools/package-skyboard.py       위 두 ZIP 만들기
Tools/board-browser-check.cjs   브라우저 자동 확인
```

## 로컬 실행

처음 한 번 가상환경을 만든다.
```
cd web/backend
uv venv --python 3.13 .venv
uv pip install --python .venv/Scripts/python.exe -r requirements-dev.txt
```
(`uv`가 없으면 `python -m venv .venv` → `.venv\Scripts\pip install -r requirements-dev.txt`)

터미널 두 개(API, 화면)를 쓴다.
1. **API:** `web/backend`에서 `.venv/Scripts/python.exe -m uvicorn main:app --reload --host 127.0.0.1 --port 8000` (API 문서: `http://localhost:8000/docs`)
2. **화면:** `web/frontend`에서 `npm ci`(처음 한 번) → `npm run dev` → `http://localhost:5173`
   - 개발 서버가 `/api`, `/uploads`를 8000번 API로 넘겨 준다. 빌드본을 보려면 `npm run build` → `npm run preview` → `http://localhost:4173`

아무 이름과 비밀번호로 들어간다. 시크릿 창에서 다른 이름으로 들어가면 둘이 함께 쓰는 모습을 볼 수 있다.
데이터는 `web/backend/data/`(`board.db`, `uploads/`)에 쌓인다(git 제외). 이 폴더를 지우고 다시 켜면 샘플 데이터로 시작한다.

## 테스트

- API: `cd web/backend` → `.venv/Scripts/python.exe -m pytest -q` (16개)
- 브라우저: 서버를 띄운 뒤 저장소 루트에서
  ```
  npm ci --prefix Tools
  node Tools/board-browser-check.cjs http://localhost:5173
  ```
  데스크톱 1명 + 휴대폰 화면 1명이 실제 클릭·입력으로 들어가기 → 이미지 글 → 프로젝트·할 일 → 검색 → 수정 → 로그아웃·재로그인까지 확인한다. Google Chrome이 필요하다.

## Sky 배포

### 메인: Full-stack (`SkyBoard-fullstack.zip`)
```
frontend/   React + Vite 소스         → 빌드 → S3 + CloudFront (온프렘: Nginx)
static/     빌드된 화면 + config.json  → 위 단계의 결과물. Sky가 빌드 전이라면 이걸 그대로 올린다
backend/    FastAPI + Dockerfile       → ECS (온프렘: Docker)
backend/seed/board.db  SQLite 샘플 DB → RDS PostgreSQL로 이전
```
배포 순서: API를 먼저 배포 → 그 주소를 `static/config.json`의 `apiBaseUrl`에 넣음 → 화면 배포. API의 CORS 기본값은 `*`라 추가 설정은 없다.

구성 요소별로 Sky 최신 코드(`sky-platform` main `71eb4b2`) 분석 함수에 넣어 본 결과:

| 경로 | Sky 판단 | 의미 |
| --- | --- | --- |
| `static/` | 정적 사이트 `eligible` | S3 + CloudFront로 바로 배포 가능 |
| `frontend/` | `needs_build` | React 소스. Sky에 빌드 후 정적 배포하는 경로가 아직 없다 |
| `backend/` | 컨테이너(Dockerfile), SQLite → RDS 사전 검사 통과(7개 테이블, 행 8개) | ECS + RDS |
| ZIP 전체 | 오류: 루트에 `main.py` 필요 | Sky는 아직 업로드를 앱 하나로만 본다. 구성 요소 분류는 Sky 쪽 작업 |

### 예비: 한 서버 (`SkyBoard-single-server.zip`)
API 컨테이너 하나가 화면(`public/`)까지 같이 보낸다(같은 주소, `config.json` 수정 불필요). Sky가 구성 요소 분류를 하기 전에도 Deploy 한 번 → URL 하나로 시연할 수 있다. 샘플 DB는 `data/board.db`(볼륨 `/app/data`). Docker로 Sky와 같은 방식(빈 볼륨, `PORT=3000`)으로 띄워 확인했다: 브라우저 확인 7단계 통과, 컨테이너를 새로 만들어도 글·이미지 유지.

코드를 고쳤으면 두 ZIP을 다시 만들어 함께 커밋한다(저장소 루트에서):
```
web/backend/.venv/Scripts/python.exe Tools/package-skyboard.py SkyBoard-fullstack.zip
web/backend/.venv/Scripts/python.exe Tools/package-skyboard.py SkyBoard-single-server.zip --single-server
```

SQLite 자동 이전 조건 때문에 테이블은 **INTEGER/TEXT 칸, INTEGER 기본 키, NOT NULL만** 쓴다(외래 키·UNIQUE·인덱스·기본값·CHECK 없음, `migrations/` 폴더 없음, SQLite 파일 1개). 그 규칙들은 코드에서 지킨다. 자세한 이유는 `board/schema.sql` 맨 위 주석.

### 이미지 저장소
- 온프레미스: 위 볼륨에 저장되므로 따로 할 일이 없다(MinIO를 쓰려면 `S3_BUCKET` + `S3_ENDPOINT_URL`).
- AWS(ECS): 컨테이너 디스크는 재배포 때 지워진다. S3 버킷을 만들고(비공개 그대로) `S3_BUCKET=<버킷 이름>`을 준다. 작업 역할에 그 버킷의 `s3:ListBucket`과 `uploads/*`의 `s3:GetObject`, `s3:PutObject`, `s3:DeleteObject`가 필요하다. Sky에는 아직 앱 파일용 S3 연결 기능이 없다(`durable_files: False`).
- 이미지 주소는 어느 저장소든 `/uploads/<이름>`으로 같다(API가 읽어서 보낸다).
- 저장소를 쓸 수 없으면 서버가 시작할 때 바로 멈추고 이유를 로그에 남긴다.


## 설정 (환경 변수)

| 변수 | 기본값 | 뜻 |
| --- | --- | --- |
| `PORT` | `8000` (Dockerfile) | 서버 포트 |
| `DATABASE_PATH` | `data/board.db` | SQLite 파일 위치 |
| `SEED_DATABASE` | `seed/board.db` | DB가 없을 때 복사할 샘플. 빈 값이면 빈 DB로 시작 |
| `UPLOAD_DIR` | `data/uploads` | 이미지 폴더 (`S3_BUCKET`이 없을 때) |
| `S3_BUCKET` | (없음) | 주면 이미지를 이 S3 버킷에 저장. 접속 정보는 AWS 표준 방식(작업 역할, `AWS_*` 변수) |
| `S3_PREFIX` | `uploads/` | 버킷 안 경로 앞부분 |
| `S3_ENDPOINT_URL` | (없음) | MinIO 같은 S3 호환 저장소 주소 |
| `CORS_ORIGINS` | `*` | 화면을 따로 배포할 때 API를 부를 수 있는 주소. 로그인은 `Authorization` 헤더라 `*`도 안전하다 |
| `MAX_UPLOAD_MB` | `5` | 이미지 한 장 최대 크기 |
| `SESSION_DAYS` | `7` | 로그인 유지 기간 |

## API 한눈에

모든 `/api/*`는 로그인을 빼고 `Authorization: Bearer <토큰>`이 필요하다. 오류는 `{"detail": "..."}`.

| 메서드 | 주소 | 내용 |
| --- | --- | --- |
| POST | `/api/auth/login` | `{username, password}` → `{token, user, created}` (새 이름이면 계정 생성, `created: true`) |
| POST | `/api/auth/logout` | 이 토큰 로그아웃 |
| GET | `/api/auth/me` | 내 정보 |
| GET | `/api/users` | 전체 멤버 (담당자 고르기용) |
| GET | `/api/posts?q=&page=&pageSize=` | 글 목록 `{items, total, page, pageSize}` |
| POST / GET / PATCH / DELETE | `/api/posts`, `/api/posts/{id}` | 글 쓰기·보기·고치기·지우기 (`imageIds`로 이미지 지정) |
| POST | `/api/images` | 이미지 업로드(form 필드 `file`) → `{id, url}` |
| GET | `/uploads/{name}` | 이미지 파일 (로그인 불필요, 이름은 추측 불가한 무작위 값) |
| GET / POST | `/api/projects` | 프로젝트 목록(할 일 개수 포함)·만들기 |
| GET / PATCH / DELETE | `/api/projects/{id}` | 프로젝트(할 일 포함)·이름 변경·삭제 |
| POST | `/api/projects/{id}/tasks` | 할 일 추가 `{title, assigneeId?, dueDate?, status?}` |
| PATCH / DELETE | `/api/tasks/{id}` | 할 일 수정(보낸 칸만 바뀜)·삭제 |
| GET | `/api/search?q=` | `{posts, projects, tasks}` |
| GET | `/health` | 서버·DB 확인 |

## 알아둘 점

- 글에 붙이지 않고 버린 업로드 이미지(올렸다가 글을 저장하지 않은 경우)는 서버에 남는다.
- 로그인 시도 횟수 제한은 없다(테스트용).
- Dockerfile은 root로 실행한다(Sky 볼륨 권한 문제를 피하려고).
- Sky가 나중에 업로드 폴더까지 "영속 파일"로 감지하게 바뀌면, 지금의 로컬 SQLite 볼륨 기능은 "SQLite 외 요구"로 보고 거부한다. 그때는 Sky 쪽에서 같은 볼륨 안의 파일을 허용해야 한다.
