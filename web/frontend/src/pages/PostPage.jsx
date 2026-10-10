import { Link, useNavigate, useParams } from 'react-router-dom';
import { api, imageUrl } from '../api.js';
import { useAuth } from '../auth.jsx';
import { formatDate, useLoad } from '../util.js';

export default function PostPage() {
  const { id } = useParams();
  const { user } = useAuth();
  const navigate = useNavigate();
  const { data: post, error } = useLoad(() => api(`/api/posts/${id}`), [id]);

  const remove = async () => {
    if (!window.confirm('Delete this post and its images?')) return;
    try {
      await api(`/api/posts/${id}`, { method: 'DELETE' });
      navigate('/posts');
    } catch (err) { window.alert(err.message); }
  };

  if (error) return <p className="error">{error.message}</p>;
  if (!post) return <p className="muted">Loading…</p>;
  const mine = post.author.id === user.id;

  return (
    <article className="card post">
      <Link to="/posts" className="back">← Board</Link>
      <h1>{post.title}</h1>
      <p className="meta">{post.author.displayName} · {formatDate(post.createdAt)}
        {post.updatedAt !== post.createdAt && ` · edited ${formatDate(post.updatedAt)}`}</p>
      <div className="body">{post.body}</div>
      {post.images.length > 0 && (
        <div className="gallery">
          {post.images.map(image => (
            <a key={image.id} href={imageUrl(image.url)} target="_blank" rel="noreferrer">
              <img src={imageUrl(image.url)} alt="" />
            </a>
          ))}
        </div>
      )}
      {mine && (
        <div className="actions">
          <Link className="button secondary" to={`/posts/${id}/edit`}>Edit</Link>
          <button className="danger" onClick={remove}>Delete</button>
        </div>
      )}
    </article>
  );
}
