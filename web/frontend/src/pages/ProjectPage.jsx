import { useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { api } from '../api.js';
import { useAuth } from '../auth.jsx';
import { STATUSES, useLoad } from '../util.js';

// One project: tasks in three columns. Anyone can add, move, assign or delete tasks;
// only the owner can rename or delete the project.
export default function ProjectPage() {
  const { id } = useParams();
  const { user } = useAuth();
  const navigate = useNavigate();
  const { data: project, error, setData } = useLoad(() => api(`/api/projects/${id}`), [id]);
  const { data: members } = useLoad(() => api('/api/users'), []);
  const [actionError, setActionError] = useState('');

  // Runs a change and shows its error, if any. Returns whether it worked.
  const run = async action => {
    setActionError('');
    try { await action(); return true; } catch (err) { setActionError(err.message); return false; }
  };
  const replaceTask = task => setData(p => ({ ...p, tasks: p.tasks.map(t => (t.id === task.id ? task : t)) }));
  const updateTask = (task, changes) => run(async () => replaceTask(await api(`/api/tasks/${task.id}`, { method: 'PATCH', body: changes })));
  const deleteTask = task => run(async () => {
    await api(`/api/tasks/${task.id}`, { method: 'DELETE' });
    setData(p => ({ ...p, tasks: p.tasks.filter(t => t.id !== task.id) }));
  });
  const addTask = body => run(async () => {
    const task = await api(`/api/projects/${id}/tasks`, { method: 'POST', body });
    setData(p => ({ ...p, tasks: [...p.tasks, task] }));
  });
  const rename = () => {
    const name = window.prompt('Project name', project.name);
    if (name && name.trim() !== project.name)
      run(async () => setData(await api(`/api/projects/${id}`, { method: 'PATCH', body: { name } })));
  };
  const remove = () => {
    if (window.confirm(`Delete “${project.name}” and all its tasks?`))
      run(async () => { await api(`/api/projects/${id}`, { method: 'DELETE' }); navigate('/projects'); });
  };

  if (error) return <p className="error">{error.message}</p>;
  if (!project) return <p className="muted">Loading…</p>;
  const owner = project.owner.id === user.id;

  return (
    <>
      <Link to="/projects" className="back">← Projects</Link>
      <div className="page-head">
        <div>
          <h1>{project.name}</h1>
          {project.description && <p className="muted">{project.description}</p>}
        </div>
        {owner && (
          <div className="actions">
            <button className="secondary" onClick={rename}>Rename</button>
            <button className="danger" onClick={remove}>Delete project</button>
          </div>
        )}
      </div>
      <NewTask members={members || []} onAdd={addTask} />
      {actionError && <p className="error" role="alert">{actionError}</p>}

      <div className="columns">
        {STATUSES.map((status, index) => {
          const tasks = project.tasks.filter(t => t.status === status.id);
          return (
            <section key={status.id} className={`column ${status.id}`} aria-label={status.label}>
              <h2>{status.label} <span className="count">{tasks.length}</span></h2>
              {tasks.map(task => (
                <TaskCard key={task.id} task={task} members={members || []}
                  prev={STATUSES[index - 1]} next={STATUSES[index + 1]}
                  onChange={changes => updateTask(task, changes)} onDelete={() => deleteTask(task)} />
              ))}
            </section>
          );
        })}
      </div>
    </>
  );
}

function NewTask({ members, onAdd }) {
  const [title, setTitle] = useState('');
  const [assigneeId, setAssigneeId] = useState('');
  const [dueDate, setDueDate] = useState('');

  const submit = async event => {
    event.preventDefault();
    if (await onAdd({ title, assigneeId: assigneeId ? Number(assigneeId) : null, dueDate: dueDate || null })) {
      setTitle(''); setAssigneeId(''); setDueDate('');
    }
  };

  return (
    <form className="card inline-form" onSubmit={submit}>
      <input value={title} onChange={e => setTitle(e.target.value)} placeholder="New task" required maxLength={200} aria-label="New task title" />
      <select value={assigneeId} onChange={e => setAssigneeId(e.target.value)} aria-label="Assignee">
        <option value="">Unassigned</option>
        {members.map(m => <option key={m.id} value={m.id}>{m.displayName}</option>)}
      </select>
      <input type="date" value={dueDate} onChange={e => setDueDate(e.target.value)} aria-label="Due date" />
      <button>Add task</button>
    </form>
  );
}

function TaskCard({ task, members, prev, next, onChange, onDelete }) {
  const overdue = task.dueDate && task.status !== 'done' && task.dueDate < new Date().toISOString().slice(0, 10);
  return (
    <article className="card task">
      <h3>{task.title}</h3>
      <div className="task-meta">
        <select value={task.assignee?.id ?? ''} aria-label={`Assignee of ${task.title}`}
          onChange={e => onChange({ assigneeId: e.target.value ? Number(e.target.value) : null })}>
          <option value="">Unassigned</option>
          {members.map(m => <option key={m.id} value={m.id}>{m.displayName}</option>)}
        </select>
        {task.dueDate && <span className={overdue ? 'due overdue' : 'due'}>Due {task.dueDate}</span>}
      </div>
      <div className="task-actions">
        {prev && <button className="secondary small" onClick={() => onChange({ status: prev.id })}>← {prev.label}</button>}
        {next && <button className="secondary small" onClick={() => onChange({ status: next.id })}>{next.label} →</button>}
        <button className="ghost small" onClick={onDelete} aria-label={`Delete ${task.title}`}>Delete</button>
      </div>
    </article>
  );
}
