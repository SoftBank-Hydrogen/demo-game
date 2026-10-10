import { Link, useSearchParams } from 'react-router-dom';
import { api } from '../api.js';
import { STATUSES, formatDate, useLoad } from '../util.js';

const statusLabel = id => STATUSES.find(s => s.id === id)?.label;

export default function SearchPage() {
  const [params] = useSearchParams();
  const q = (params.get('q') || '').trim();
  const { data, error, loading } = useLoad(() => (q ? api(`/api/search?q=${encodeURIComponent(q)}`) : Promise.resolve(null)), [q]);

  if (!q) return <p className="muted">Type something in the search box.</p>;
  const empty = data && !data.posts.length && !data.projects.length && !data.tasks.length;

  return (
    <>
      <div className="page-head"><h1>Search: “{q}”</h1></div>
      {error && <p className="error">{error.message}</p>}
      {loading && <p className="muted">Searching…</p>}
      {empty && <p className="muted">Nothing found.</p>}
      {data?.posts.length > 0 && (
        <section className="results">
          <h2>Posts</h2>
          {data.posts.map(p => (
            <Link key={p.id} to={`/posts/${p.id}`} className="card result">
              <strong>{p.title}</strong><span className="excerpt">{p.excerpt}</span>
              <span className="meta">{formatDate(p.createdAt)}</span>
            </Link>
          ))}
        </section>
      )}
      {data?.projects.length > 0 && (
        <section className="results">
          <h2>Projects</h2>
          {data.projects.map(p => (
            <Link key={p.id} to={`/projects/${p.id}`} className="card result">
              <strong>{p.name}</strong><span className="excerpt">{p.description}</span>
            </Link>
          ))}
        </section>
      )}
      {data?.tasks.length > 0 && (
        <section className="results">
          <h2>Tasks</h2>
          {data.tasks.map(t => (
            <Link key={t.id} to={`/projects/${t.projectId}`} className="card result">
              <strong>{t.title}</strong>
              <span className={`pill ${t.status}`}>{statusLabel(t.status)}</span>
              {t.assignee && <span className="meta">{t.assignee.displayName}</span>}
            </Link>
          ))}
        </section>
      )}
    </>
  );
}
