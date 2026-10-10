from conftest import signup


def test_project_and_task_crud(client):
    alice, me = signup(client, "alice")
    bob, bob_user = signup(client, "bob")
    project = client.post("/api/projects", headers=alice, json={"name": "Launch", "description": "Demo day"}).json()
    assert project["owner"]["id"] == me["id"] and project["tasks"] == []

    res = client.post(f"/api/projects/{project['id']}/tasks", headers=bob,
                      json={"title": "Slides", "assigneeId": me["id"], "dueDate": "2026-10-20"})
    assert res.status_code == 201
    task = res.json()
    assert task["status"] == "todo" and task["assignee"]["id"] == me["id"] and task["dueDate"] == "2026-10-20"

    moved = client.patch(f"/api/tasks/{task['id']}", headers=bob, json={"status": "doing", "assigneeId": None}).json()
    assert moved["status"] == "doing" and moved["assignee"] is None and moved["title"] == "Slides"

    listed = client.get("/api/projects", headers=bob).json()
    assert listed[0]["taskCounts"] == {"todo": 0, "doing": 1, "done": 0}

    assert client.patch(f"/api/projects/{project['id']}", headers=bob, json={"name": "Mine"}).status_code == 403
    assert client.patch(f"/api/projects/{project['id']}", headers=alice, json={"name": "Launch v2"}).json()["name"] == "Launch v2"

    assert client.delete(f"/api/tasks/{task['id']}", headers=bob).status_code == 204
    client.post(f"/api/projects/{project['id']}/tasks", headers=alice, json={"title": "Another"})
    assert client.delete(f"/api/projects/{project['id']}", headers=bob).status_code == 403
    assert client.delete(f"/api/projects/{project['id']}", headers=alice).status_code == 204
    assert client.get(f"/api/projects/{project['id']}", headers=alice).status_code == 404


def test_task_validation(client):
    alice, _ = signup(client, "alice")
    project = client.post("/api/projects", headers=alice, json={"name": "P"}).json()
    url = f"/api/projects/{project['id']}/tasks"
    assert client.post(url, headers=alice, json={"title": "x", "status": "blocked"}).status_code == 422
    assert client.post(url, headers=alice, json={"title": "x", "assigneeId": 999}).status_code == 400
    assert client.post(url, headers=alice, json={"title": "   "}).status_code == 422
    assert client.post("/api/projects/999/tasks", headers=alice, json={"title": "x"}).status_code == 404
    task = client.post(url, headers=alice, json={"title": "x"}).json()
    assert client.patch(f"/api/tasks/{task['id']}", headers=alice, json={"status": None}).status_code == 422
    assert client.patch(f"/api/tasks/{task['id']}", headers=alice, json={"unknown": 1}).status_code == 422


def test_search_and_users(client):
    alice, _ = signup(client, "Alice")
    signup(client, "Bob")
    client.post("/api/posts", headers=alice, json={"title": "Release notes", "body": "Ship the board"})
    project = client.post("/api/projects", headers=alice, json={"name": "Board release"}).json()
    client.post(f"/api/projects/{project['id']}/tasks", headers=alice, json={"title": "Write release email"})
    client.post(f"/api/projects/{project['id']}/tasks", headers=alice, json={"title": "Unrelated"})

    found = client.get("/api/search?q=release", headers=alice).json()
    assert [p["title"] for p in found["posts"]] == ["Release notes"]
    assert [p["name"] for p in found["projects"]] == ["Board release"]
    assert [t["title"] for t in found["tasks"]] == ["Write release email"]
    assert client.get("/api/search?q=", headers=alice).status_code == 422

    assert [u["displayName"] for u in client.get("/api/users", headers=alice).json()] == ["Alice", "Bob"]
