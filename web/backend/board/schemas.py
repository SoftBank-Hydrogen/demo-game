"""Request bodies and their validation rules. JSON uses camelCase (displayName, imageIds, ...)."""
from datetime import date
from typing import Annotated, Literal

from pydantic import BaseModel, ConfigDict, Field, StringConstraints
from pydantic.alias_generators import to_camel

Title = Annotated[str, StringConstraints(strip_whitespace=True, min_length=1, max_length=200)]
Body = Annotated[str, StringConstraints(strip_whitespace=True, min_length=1, max_length=20000)]
Note = Annotated[str, StringConstraints(strip_whitespace=True, max_length=2000)]
Name = Annotated[str, StringConstraints(strip_whitespace=True, min_length=1, max_length=100)]
Status = Literal["todo", "doing", "done"]
MAX_IMAGES_PER_POST = 4


class In(BaseModel):
    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=True, extra="forbid")


class Login(In):
    # Test app: any name works; a new name creates the account on first login.
    username: Annotated[str, StringConstraints(strip_whitespace=True, min_length=1, max_length=30)]
    password: str = Field(min_length=1, max_length=128)


class PostIn(In):
    title: Title
    body: Body
    image_ids: list[int] = Field(default_factory=list, max_length=MAX_IMAGES_PER_POST)


class PostPatch(In):
    title: Title | None = None
    body: Body | None = None
    image_ids: list[int] | None = Field(default=None, max_length=MAX_IMAGES_PER_POST)


class ProjectIn(In):
    name: Name
    description: Note = ""


class ProjectPatch(In):
    name: Name | None = None
    description: Note | None = None


class TaskIn(In):
    title: Title
    description: Note = ""
    status: Status = "todo"
    assignee_id: int | None = None
    due_date: date | None = None


class TaskPatch(In):
    # Fields left out are unchanged; `"assigneeId": null` clears the assignee.
    title: Title | None = None
    description: Note | None = None
    status: Status | None = None
    assignee_id: int | None = None
    due_date: date | None = None
