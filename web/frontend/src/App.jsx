import { useState } from 'react';
import { Navigate, NavLink, Route, Routes, useNavigate, useSearchParams } from 'react-router-dom';
import { useAuth } from './auth.jsx';
import LoginPage from './pages/LoginPage.jsx';
import PostListPage from './pages/PostListPage.jsx';
import PostPage from './pages/PostPage.jsx';
import PostEditorPage from './pages/PostEditorPage.jsx';
import ProjectListPage from './pages/ProjectListPage.jsx';
import ProjectPage from './pages/ProjectPage.jsx';
import SearchPage from './pages/SearchPage.jsx';

export default function App() {
  const { user, checking } = useAuth();
  if (checking) return <p className="center muted">Loading…</p>;
  if (!user) return <LoginPage />;

  return (
    <div className="shell">
      <Header />
      <main className="page">
        <Routes>
          <Route path="/" element={<Navigate to="/posts" replace />} />
          <Route path="/posts" element={<PostListPage />} />
          <Route path="/posts/new" element={<PostEditorPage />} />
          <Route path="/posts/:id" element={<PostPage />} />
          <Route path="/posts/:id/edit" element={<PostEditorPage />} />
          <Route path="/projects" element={<ProjectListPage />} />
          <Route path="/projects/:id" element={<ProjectPage />} />
          <Route path="/search" element={<SearchPage />} />
          <Route path="*" element={<p className="muted">Page not found.</p>} />
        </Routes>
      </main>
    </div>
  );
}

function Header() {
  const { user, signOut } = useAuth();
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const [q, setQ] = useState(params.get('q') || '');

  const search = event => {
    event.preventDefault();
    if (q.trim()) navigate(`/search?q=${encodeURIComponent(q.trim())}`);
  };

  return (
    <header className="topbar">
      <span className="brand">TeamBoard</span>
      <nav>
        <NavLink to="/posts">Board</NavLink>
        <NavLink to="/projects">Projects</NavLink>
      </nav>
      <form className="search" onSubmit={search} role="search">
        <input type="search" value={q} onChange={e => setQ(e.target.value)} placeholder="Search posts, projects, tasks" aria-label="Search" />
      </form>
      <span className="who">{user.displayName}</span>
      <button className="ghost" onClick={signOut}>Log out</button>
    </header>
  );
}
