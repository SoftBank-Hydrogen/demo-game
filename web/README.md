# TeamBoard — 팀 게시판 / 프로젝트 관리 (Sky 시연용 웹앱)

이미지를 붙일 수 있는 팀 게시판과 프로젝트·할 일 관리 웹앱이다. 화면(React)과 API(FastAPI)를 **따로 배포**한다.

| 기능 | 내용 |
| --- | --- |
| 회원 | 가입, 로그인, 로그아웃. 비밀번호는 scrypt 해시, 로그인 토큰은 해시만 DB에 저장 |
| 게시글 | 목록(페이지), 보기, 쓰기, 고치기, 지우기. 고치기·지우기는 작성자만 |
| 이미지 | 글 하나에 최대 4장, 한 장 5MB. PNG·JPEG·GIF·WebP만(파일 내용으로 판별, SVG 거부) |
| 프로젝트·할 일 | 프로젝트 만들기(이름 변경·삭제는 만든 사람만), 할 일 추가·담당자·마감일·상태(할 일 → 진행 중 → 완료)·삭제 |
| 검색 | 상단 검색창 하나로 글, 프로젝트, 할 일을 함께 찾기 |

## 폴더

```
web/
  backend/                 FastAPI (Python 3.13)
    main.py                앱 시작점: app = FastAPI(...), /, /health
    board/config.py        환경 변수 읽기
    board/db.py            SQLite 연결, migrations/*.sql 적용
    board/security.py      비밀번호 해시, 로그인 토큰
    board/storage.py       이미지 파일 저장 (로컬 폴더; S3로 바꿀 때 이 파일만 수정)
    board/schemas.py       입력 검사 규칙
    board/routes/          auth, posts, images, projects, search
    migrations/001_init.sql
    tests/                 pytest 13개
  frontend/                React + Vite
    src/api.js             API 호출 (주소는 /config.json에서 읽음)
    src/auth.jsx           로그인 상태
    src/pages/             화면들
    public/config.json     개발용 API 주소
    server.js              배포용 정적 서버 (npm start)
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
2. **화면**
   ```
   cd web/frontend
   npm ci
   npm run dev
   ```
3. 브라우저에서 `http://localhost:5173` → "Create an account"로 가입한다. 다른 브라우저(또는 시크릿 창)에서 한 명 더 가입하면 둘이 함께 쓰는 모습을 볼 수 있다.

데이터는 `web/backend/data/`(`board.db`, `uploads/`)에 쌓인다. 지우면 처음 상태가 된다(git에는 올라가지 않음).

## 테스트

- API: `cd web/backend` → `.venv/Scripts/python.exe -m pytest -q` (13개: 가입·로그인, 권한, 이미지 검사, 페이지·검색, 프로젝트·할 일)
- 브라우저: 위처럼 API와 화면을 띄운 뒤 저장소 루트에서
  ```
  npm ci --prefix Tools
  node Tools/board-browser-check.cjs http://localhost:5173
  ```
  데스크톱 1명 + 휴대폰 화면 1명이 실제 클릭·입력으로 가입 → 이미지 글 → 프로젝트·할 일 → 검색 → 수정 → 로그아웃·재로그인까지 확인한다. Google Chrome이 필요하다.

## 설정 (환경 변수)

| 앱 | 변수 | 기본값 | 뜻 |
| --- | --- | --- | --- |
| API | `DATABASE_PATH` | `data/board.db` | SQLite 파일 위치 |
| API | `UPLOAD_DIR` | `data/uploads` | 이미지 파일 폴더 |
| API | `CORS_ORIGINS` | `http://localhost:5173,http://127.0.0.1:5173` | API를 부를 수 있는 화면 주소(쉼표로 여러 개) |
| API | `MAX_UPLOAD_MB` | `5` | 이미지 한 장 최대 크기 |
| API | `SESSION_DAYS` | `7` | 로그인 유지 기간 |
| 화면 | `API_BASE_URL` | (없으면 `public/config.json`) | 브라우저가 부를 API 주소 |
| 화면 | `PORT`, `HOST` | `3000`, `0.0.0.0` | 배포용 서버 주소 |

화면은 빌드 결과에 API 주소를 넣지 않고 시작할 때 `/config.json`을 읽는다. 그래서 **같은 빌드를 어디에 배포해도 `API_BASE_URL`만 바꾸면 된다.**

## 배포 (Sky)

두 폴더를 **각각 하나의 앱**으로 올린다. Sky의 정적 분석 결과는 아래와 같다(직접 확인함).

| 폴더 | Sky가 보는 앱 | 실행 | 확인 경로 |
| --- | --- | --- | --- |
| `web/backend` | `python-asgi` | `python -m uvicorn main:app --host 0.0.0.0 --port $PORT` | `/` 또는 `/health` |
| `web/frontend` | `nodejs` | `npm start` (먼저 `vite build`가 자동 실행됨) | `/` 또는 `/health` |

순서:
1. API를 먼저 배포해 주소를 얻는다(예: `https://api.example.com`).
2. 화면을 배포할 때 `API_BASE_URL=https://api.example.com`을 준다.
3. API에 `CORS_ORIGINS=https://화면주소`를 준다. 빠뜨리면 브라우저가 "Cannot reach the server" 또는 CORS 오류를 낸다.

**데이터 보존:** SQLite 파일과 이미지는 컨테이너 안의 디스크에 저장된다. 재배포하면 사라지므로 Sky가 이 두 가지를 짚어 줄 것이다(`DATA-SQLITE-01`, `STORAGE-DURABILITY-01`). 계속 보관하려면
- 볼륨을 붙이고 `DATABASE_PATH`, `UPLOAD_DIR`를 그 볼륨 안으로 지정하거나
- DB는 PostgreSQL(RDS 등)로, 이미지는 S3로 옮긴다. 이미지는 `board/storage.py`만 바꾸면 되고, DB는 `board/db.py`와 SQL 문장(`?` 자리표시자 등)을 바꿔야 한다.

## API 한눈에

모든 `/api/*`는 가입·로그인을 빼고 `Authorization: Bearer <토큰>`이 필요하다. 오류는 `{"detail": "..."}`.

| 메서드 | 주소 | 내용 |
| --- | --- | --- |
| POST | `/api/auth/register` | `{username, password, displayName}` → `{token, user}` |
| POST | `/api/auth/login` | `{username, password}` → `{token, user}` |
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

- 글에 붙이지 않고 버린 업로드 이미지(올렸다가 글을 저장하지 않은 경우)는 서버에 남는다. 정리 작업은 아직 없다.
- 로그인 시도 횟수 제한은 없다. 공개 서비스로 쓰려면 추가해야 한다.
- 로그인 토큰은 브라우저 `localStorage`에 둔다(화면과 API 주소가 달라도 동작하도록).
