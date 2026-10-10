# SkyBoard — 팀 게시판 / 프로젝트 관리 (Sky 메인 데모 Case B)

이미지를 붙일 수 있는 팀 게시판과 프로젝트·할 일 관리 웹앱이다.
**React + FastAPI + SQLite + 로컬 업로드**로 만들어, Sky가 아래처럼 판단하고 옮기는 장면을 보여 주는 용도다.

| 부분 | 지금 (로컬) | Sky 판단 | AWS | 온프레미스 |
| --- | --- | --- | --- | --- |
| 화면 `web/static` | 빌드된 정적 파일 | Static | S3 + CloudFront | Nginx |
| API `web/backend` | FastAPI | Container | ECS | Docker |
| DB | SQLite 파일 | 영속 관계형 DB | RDS (PostgreSQL) | PostgreSQL |
| 이미지 | `data/uploads/` 폴더 | Object storage | S3 | Volume |

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
  static/                  ← 화면 배포본 (npm run build 결과, git에 포함). 이 폴더를 그대로 올린다
    config.json            API 주소. 배포할 때 이것만 고친다
  backend/                 ← API 배포본 (FastAPI, Python 3.13)
    main.py                앱 시작점: app = FastAPI(...), /, /health
    requirements.txt       fastapi, uvicorn, python-multipart
    board/schema.sql       테이블 (시작할 때 IF NOT EXISTS로 생성)
    board/db.py            SQLite 연결, 새 DB면 seed 복사
    board/storage.py       이미지 파일 저장 (로컬 폴더; S3로 바꿀 때 이 파일만 수정)
    board/security.py      비밀번호 해시, 로그인 토큰
    board/schemas.py       입력 검사 규칙
    board/routes/          auth, posts, images, projects, search
    seed/board.db          샘플 데이터 (SQLite 파일 1개)
    seed.py                seed/board.db를 다시 만드는 스크립트
    tests/                 pytest 14개
  frontend/                화면 소스 (React + Vite). 고친 뒤 npm run build → static/ 갱신
Tools/board-browser-check.cjs   브라우저 자동 확인
```

## 로컬 실행

터미널 두 개가 필요하다.

1. **API** (처음 한 번 가상환경을 만든다)
   ```
   cd web/backend
   uv venv --python 3.13 .venv
   uv pip install --python .venv/Scripts/python.exe -r requirements-dev.txt
   .venv/Scripts/python.exe -m uvicorn main:app --reload --host 127.0.0.1 --port 8000
   ```
   - `uv`가 없으면 `python -m venv .venv` → `.venv\Scripts\pip install -r requirements-dev.txt`
   - `http://localhost:8000/docs`에서 API를 직접 눌러 볼 수 있다(FastAPI 자동 문서)
2. **화면** (둘 중 하나)
   - 고치면서 보기: `cd web/frontend` → `npm ci` → `npm run dev` → `http://localhost:5173`
   - 배포본 그대로 보기: `cd web/frontend` → `npm run preview` → `http://localhost:4173` (`web/static`을 제공)
3. 아무 이름과 비밀번호로 들어간다. 시크릿 창에서 다른 이름으로 들어가면 둘이 함께 쓰는 모습을 볼 수 있다.

데이터는 `web/backend/data/`(`board.db`, `uploads/`)에 쌓인다(git 제외). 이 폴더를 지우고 API를 다시 켜면 샘플 데이터로 다시 시작한다.

## 테스트

- API: `cd web/backend` → `.venv/Scripts/python.exe -m pytest -q` (14개)
- 브라우저: API와 화면을 띄운 뒤 저장소 루트에서
  ```
  npm ci --prefix Tools
  node Tools/board-browser-check.cjs http://localhost:5173
  ```
  데스크톱 1명 + 휴대폰 화면 1명이 실제 클릭·입력으로 들어가기 → 이미지 글 → 프로젝트·할 일 → 검색 → 수정 → 로그아웃·재로그인까지 확인한다. Google Chrome이 필요하다.

## 설정 (환경 변수, API)

| 변수 | 기본값 | 뜻 |
| --- | --- | --- |
| `DATABASE_PATH` | `data/board.db` | SQLite 파일 위치 |
| `SEED_DATABASE` | `seed/board.db` | DB가 없을 때 복사할 샘플. 빈 값이면 빈 DB로 시작 |
| `UPLOAD_DIR` | `data/uploads` | 이미지 파일 폴더 |
| `CORS_ORIGINS` | `*` | API를 부를 수 있는 화면 주소(쉼표로 여러 개). 로그인은 쿠키가 아니라 `Authorization` 헤더라 `*`도 안전하다 |
| `MAX_UPLOAD_MB` | `5` | 이미지 한 장 최대 크기 |
| `SESSION_DAYS` | `7` | 로그인 유지 기간 |

화면은 빌드에 API 주소를 넣지 않고, 시작할 때 `index.html` 옆의 `config.json`을 읽는다. 주소는 `#/posts/3`처럼 `#` 뒤에 있어서, 정적 호스팅(S3, Nginx)에 별도 설정 없이 올려도 새로고침과 직접 접속이 된다.

## 배포 (Sky)

1. **API**: `web/backend`를 올린다 → 공개 주소를 얻는다(예: `https://xxxx.ecs.aws`).
2. **화면**: `web/static/config.json`의 `apiBaseUrl`을 그 주소로 바꾼 뒤 `web/static`을 올린다.

Sky 최신 코드(`sky-platform` origin/main)의 분석 함수로 직접 확인한 결과:

| 검사 | 결과 |
| --- | --- |
| `web/static` 정적 사이트 판정 (`assess_static_site`) | `eligible` → S3 + CloudFront |
| `web/backend` 실행 방식 (`analyze`) | `python-asgi`, `python -m uvicorn main:app --host 0.0.0.0 --port $PORT` |
| SQLite → PostgreSQL 자동 이전 사전 검사 (`preflight_sqlite_conversion`) | 통과. `seed/board.db` 7개 테이블, 행 8개 → `postgresql` |

SQLite 자동 이전 조건 때문에 테이블은 **INTEGER/TEXT 칸, INTEGER 기본 키, NOT NULL만** 쓴다(외래 키·UNIQUE·인덱스·기본값·CHECK 없음, `migrations/` 폴더 없음, SQLite 파일 1개). 그 규칙들은 코드에서 지킨다. 자세한 이유는 `board/schema.sql` 맨 위 주석.

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

## 알아둘 점

- 글에 붙이지 않고 버린 업로드 이미지(올렸다가 글을 저장하지 않은 경우)는 서버에 남는다.
- 로그인 시도 횟수 제한은 없다(테스트용).
- 로그인 토큰은 브라우저 `localStorage`에 둔다(화면과 API 주소가 달라도 동작하도록).
