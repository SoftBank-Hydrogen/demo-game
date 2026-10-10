# SkyBoard — 팀 게시판 / 프로젝트 관리 (Sky 메인 데모 Case B)

이미지를 붙일 수 있는 팀 게시판과 프로젝트·할 일 관리 웹앱이다. **React + FastAPI + SQLite + 로컬 업로드.**
FastAPI 서버 하나가 화면과 API를 함께 제공한다(같은 주소). Sky에는 **ZIP 하나를 올려 Deploy 한 번 → URL 하나**로 배포한다.

| 부분 | 지금 (로컬) | Sky 배포 후: AWS | 온프레미스 |
| --- | --- | --- | --- |
| 앱 (화면 + API) | FastAPI 서버 하나 | ECS 컨테이너 | Docker 컨테이너 |
| DB | `data/board.db` (SQLite) | RDS PostgreSQL (Sky가 자동 이전·코드 변환) | SQLite 그대로 + 볼륨 `/app/data` |
| 이미지 | `data/uploads/` | S3 (`S3_BUCKET`) | 같은 볼륨 `/app/data/uploads` |

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
  backend/                 FastAPI (Python 3.13). 화면도 여기서 제공
    main.py                app = FastAPI(...); /api/*, /uploads/*, /health, 그 외 경로는 화면
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
  static/                  빌드된 화면 (git에 포함). 로컬에서는 API가 이 폴더를 제공
SkyBoard-sky.zip                Sky 업로드용 ZIP (Tools/package-skyboard.py로 생성)
Tools/package-skyboard.py       Sky 업로드용 ZIP 만들기
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

- **써 보기 (터미널 하나):** `.venv/Scripts/python.exe -m uvicorn main:app --reload --host 127.0.0.1 --port 8000` → `http://localhost:8000`
  - 빌드된 `web/static` 화면이 같이 나온다. API 문서는 `http://localhost:8000/docs`
- **화면을 고치면서 보기:** 위 API를 켠 채로 `cd web/frontend` → `npm ci` → `npm run dev` → `http://localhost:5173`
  - 개발 서버가 `/api`, `/uploads`를 8000번 API로 넘겨 준다. 다 고쳤으면 `npm run build`로 `web/static`을 갱신한다

아무 이름과 비밀번호로 들어간다. 시크릿 창에서 다른 이름으로 들어가면 둘이 함께 쓰는 모습을 볼 수 있다.
데이터는 `web/backend/data/`(`board.db`, `uploads/`)에 쌓인다(git 제외). 이 폴더를 지우고 다시 켜면 샘플 데이터로 시작한다.

## 테스트

- API: `cd web/backend` → `.venv/Scripts/python.exe -m pytest -q` (16개)
- 브라우저: 서버를 띄운 뒤 저장소 루트에서
  ```
  npm ci --prefix Tools
  node Tools/board-browser-check.cjs http://localhost:8000
  ```
  데스크톱 1명 + 휴대폰 화면 1명이 실제 클릭·입력으로 들어가기 → 이미지 글 → 프로젝트·할 일 → 검색 → 수정 → 로그아웃·재로그인까지 확인한다. Google Chrome이 필요하다.

## Sky 배포

1. 저장소 루트의 **`SkyBoard-sky.zip`**을 Sky에 올리고 Deploy → 나온 URL로 접속한다.
   - 안에는 `main.py`, `board/`, `Dockerfile`, `requirements.txt`, `public/`(빌드된 화면), `data/board.db`(샘플 DB)가 들어 있다.
   - **GitHub의 `web/backend` 폴더만 올리면 화면과 DB가 빠지므로 이 ZIP을 올린다.**
2. 코드를 고쳤으면 ZIP을 다시 만들어 함께 커밋한다(저장소 루트에서):
   ```
   web/backend/.venv/Scripts/python.exe Tools/package-skyboard.py SkyBoard-sky.zip
   ```

ZIP을 Sky 최신 코드(`sky-platform` origin/main) 분석 함수와 Docker로 확인한 결과:

| 검사 | 결과 |
| --- | --- |
| 실행 방식 (`analyze`) | 기존 Dockerfile 사용. Sky가 넘기는 `PORT`(3000)로 뜬다 |
| SQLite → PostgreSQL 자동 이전 사전 검사 | 통과. `data/board.db` 7개 테이블, 행 8개 → `postgresql` (RDS) |
| 로컬/온프레미스 SQLite 볼륨 조건 | DB 1개, 하위 폴더 `data/`, Dockerfile `WORKDIR /app` → 볼륨 `/app/data`. 이미지(`data/uploads`)도 같은 볼륨에 남는다 |
| Docker 실제 실행 (빈 볼륨을 `/app/data`에, `PORT=3000`) | 화면·API·`/health` 정상, 내부 파일(`/main.py`, `/data/board.db`) 404, 브라우저 확인 7단계 통과, 컨테이너를 새로 만들어도 글·이미지 유지 |

SQLite 자동 이전 조건 때문에 테이블은 **INTEGER/TEXT 칸, INTEGER 기본 키, NOT NULL만** 쓴다(외래 키·UNIQUE·인덱스·기본값·CHECK 없음, `migrations/` 폴더 없음, SQLite 파일 1개). 그 규칙들은 코드에서 지킨다. 자세한 이유는 `board/schema.sql` 맨 위 주석.

### 이미지 저장소
- 온프레미스: 위 볼륨에 저장되므로 따로 할 일이 없다(MinIO를 쓰려면 `S3_BUCKET` + `S3_ENDPOINT_URL`).
- AWS(ECS): 컨테이너 디스크는 재배포 때 지워진다. S3 버킷을 만들고(비공개 그대로) `S3_BUCKET=<버킷 이름>`을 준다. 작업 역할에 그 버킷의 `s3:ListBucket`과 `uploads/*`의 `s3:GetObject`, `s3:PutObject`, `s3:DeleteObject`가 필요하다. Sky에는 아직 앱 파일용 S3 연결 기능이 없다(`durable_files: False`).
- 이미지 주소는 어느 저장소든 `/uploads/<이름>`으로 같다(API가 읽어서 보낸다).
- 저장소를 쓸 수 없으면 서버가 시작할 때 바로 멈추고 이유를 로그에 남긴다.

### 화면만 따로 배포하고 싶을 때 (선택)
`web/static`은 그대로 정적 사이트(S3 + CloudFront, Nginx)로 올릴 수 있다(Sky 정적 판정 `eligible`).
이때는 `web/static/config.json`의 `apiBaseUrl`에 API 주소를 넣는다. API의 `CORS_ORIGINS` 기본값이 `*`라 추가 설정은 없다.

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
