import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { api } from '../api.js';
import { STATUSES, formatDate, useLoad } from '../util.js';

export default function ProjectListPage() {
  const navigate = useNavigate();
  const { data: projects, error } = useLoad(() => api('/api/projects'), []);
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [formError, setFormError] = useState('');

  const create = async event => {
    event.preventDefault();
    setFormError('');
    try {
      const project = await api('/api/projects', { method: 'POST', body: { name, description } });
      navigate(`/projects/${project.id}`);
    } catch (err) { setFormError(err.message); }
  };

  return (
    <>
      <div className="page-head"><h1>Projects</h1></div>
      <form className="card inline-form" onSubmit={create}>
        <input value={name} onChange={e => setName(e.target.value)} placeholder="New project name" required maxLength={100} />
        <input value={description} onChange={e => setDescription(e.target.value)} placeholder="Short description (optional)" maxLength={2000} />
        <button>Create</button>
        {formError && <p className="error">{formError}</p>}
      </form>

      {error && <p className="error">{error.message}</p>}
      {projects && projects.length === 0 && <p className="muted">No projects yet.</p>}
      <ul className="project-grid">
        {projects?.map(project => (
          <li key={project.id} className="card">
            <Link to={`/projects/${project.id}`} className="project-link">
              <h2>{project.name}</h2>
              {project.description && <p className="excerpt">{project.description}</p>}
              <p className="counts">
                {STATUSES.map(s => <span key={s.id} className={`pill ${s.id}`}>{s.label} {project.taskCounts[s.id]}</span>)}
              </p>
              <p className="meta">{project.owner.displayName} · updated {formatDate(project.updatedAt)}</p>
            </Link>
          </li>
        ))}
      </ul>
    </>
  );
}
