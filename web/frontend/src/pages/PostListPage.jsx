import { useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { api, imageUrl } from '../api.js';
import { formatDate, useLoad } from '../util.js';

const PAGE_SIZE = 10;

export default function PostListPage() {
  const [params, setParams] = useSearchParams();
  const q = params.get('q') || '';
  const page = Math.max(1, Number(params.get('page')) || 1);
  const [text, setText] = useState(q);
  const { data, error, loading } = useLoad(
    () => api(`/api/posts?q=${encodeURIComponent(q)}&page=${page}&pageSize=${PAGE_SIZE}`), [q, page]);
  const pages = data ? Math.max(1, Math.ceil(data.total / PAGE_SIZE)) : 1;
  const go = next => setParams({ ...(q && { q }), page: String(next) });

  return (
    <>
      <div className="page-head">
        <h1>Board</h1>
        <Link className="button" to="/posts/new">New post</Link>
      </div>
      <form className="filter" onSubmit={e => { e.preventDefault(); setParams(text.trim() ? { q: text.trim() } : {}); }}>
        <input type="search" value={text} onChange={e => setText(e.target.value)} placeholder="Filter posts by title or text" />
        <button className="secondary">Filter</button>
      </form>

      {error && <p className="error">{error.message}</p>}
      {loading && !data && <p className="muted">Loading…</p>}
      {data && data.items.length === 0 && <p className="muted">{q ? `No posts match “${q}”.` : 'No posts yet. Write the first one.'}</p>}
      <ul className="post-list">
        {data?.items.map(post => (
          <li key={post.id} className="card post-row">
            <Link to={`/posts/${post.id}`} className="post-link">
              {post.thumbnail && <img src={imageUrl(post.thumbnail)} alt="" className="thumb" />}
              <div>
                <h2>{post.title}</h2>
                <p className="excerpt">{post.excerpt}</p>
                <p className="meta">{post.author.displayName} · {formatDate(post.createdAt)}
                  {post.imageCount > 0 && ` · ${post.imageCount} image${post.imageCount > 1 ? 's' : ''}`}</p>
              </div>
            </Link>
          </li>
        ))}
      </ul>
      {pages > 1 && (
        <div className="pager">
          <button className="secondary" disabled={page <= 1} onClick={() => go(page - 1)}>Previous</button>
          <span>Page {page} of {pages}</span>
          <button className="secondary" disabled={page >= pages} onClick={() => go(page + 1)}>Next</button>
        </div>
      )}
    </>
  );
}
