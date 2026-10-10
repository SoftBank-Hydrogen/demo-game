-- SkyBoard tables. Applied at every start; IF NOT EXISTS makes that safe.
--
-- Kept deliberately plain so Sky can copy the SQLite data to PostgreSQL automatically:
-- only INTEGER/TEXT columns, one INTEGER PRIMARY KEY per table, NOT NULL, and nothing else
-- (no foreign keys, UNIQUE, indexes, defaults or CHECK). The rules those would enforce live in
-- the code instead: unique names (routes/auth.py), allowed status values (schemas.py),
-- deleting a project's tasks and a post's images with them (routes/projects.py, routes/posts.py).
-- Times are ISO 8601 UTC text, e.g. 2026-10-10T09:30:00Z. Comments must stay outside ( ).

CREATE TABLE IF NOT EXISTS users (
  id INTEGER PRIMARY KEY,
  username TEXT NOT NULL,
  display_name TEXT NOT NULL,
  password_hash TEXT NOT NULL,
  created_at TEXT NOT NULL
);

-- One row per login. Only a hash of the token is stored.
CREATE TABLE IF NOT EXISTS sessions (
  id INTEGER PRIMARY KEY,
  token_hash TEXT NOT NULL,
  user_id INTEGER NOT NULL,
  created_at TEXT NOT NULL,
  expires_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS posts (
  id INTEGER PRIMARY KEY,
  author_id INTEGER NOT NULL,
  title TEXT NOT NULL,
  body TEXT NOT NULL,
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL
);

-- Uploaded image files. The file itself lives in UPLOAD_DIR under `filename`.
CREATE TABLE IF NOT EXISTS images (
  id INTEGER PRIMARY KEY,
  owner_id INTEGER NOT NULL,
  filename TEXT NOT NULL,
  content_type TEXT NOT NULL,
  size INTEGER NOT NULL,
  created_at TEXT NOT NULL
);

-- Which images belong to which post, in display order.
CREATE TABLE IF NOT EXISTS post_images (
  id INTEGER PRIMARY KEY,
  post_id INTEGER NOT NULL,
  image_id INTEGER NOT NULL,
  position INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS projects (
  id INTEGER PRIMARY KEY,
  owner_id INTEGER NOT NULL,
  name TEXT NOT NULL,
  description TEXT NOT NULL,
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL
);

-- status: todo, doing or done. assignee_id and due_date may be empty (NULL).
CREATE TABLE IF NOT EXISTS tasks (
  id INTEGER PRIMARY KEY,
  project_id INTEGER NOT NULL,
  title TEXT NOT NULL,
  description TEXT NOT NULL,
  status TEXT NOT NULL,
  assignee_id INTEGER,
  due_date TEXT,
  created_by INTEGER NOT NULL,
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL
);
