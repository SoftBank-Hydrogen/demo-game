import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { api, imageUrl } from '../api.js';

const MAX_IMAGES = 4;

// New post (/posts/new) or edit (/posts/:id/edit). Images upload as soon as they are picked;
// the post then stores their ids in the chosen order.
export default function PostEditorPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [title, setTitle] = useState('');
  const [body, setBody] = useState('');
  const [images, setImages] = useState([]);
  const [uploading, setUploading] = useState(0);
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);
  const [uploadsOn, setUploadsOn] = useState(false);

  useEffect(() => {
    api('/api/features').then(f => setUploadsOn(Boolean(f?.imageUploads))).catch(() => setUploadsOn(false));
  }, []);

  useEffect(() => {
    if (!id) return;
    api(`/api/posts/${id}`).then(post => { setTitle(post.title); setBody(post.body); setImages(post.images); })
      .catch(err => setError(err.message));
  }, [id]);

  const pick = async event => {
    const files = [...event.target.files].slice(0, MAX_IMAGES - images.length);
    event.target.value = '';
    setError('');
    for (const file of files) {
      const form = new FormData();
      form.append('file', file);
      setUploading(n => n + 1);
      try {
        const image = await api('/api/images', { method: 'POST', form });
        setImages(list => [...list, image]);
      } catch (err) {
        setError(`${file.name}: ${err.message}`);
      } finally {
        setUploading(n => n - 1);
      }
    }
  };

  const save = async event => {
    event.preventDefault();
    setSaving(true);
    setError('');
    try {
      const payload = { title, body, imageIds: images.map(i => i.id) };
      const post = id ? await api(`/api/posts/${id}`, { method: 'PATCH', body: payload })
                      : await api('/api/posts', { method: 'POST', body: payload });
      navigate(`/posts/${post.id}`);
    } catch (err) {
      setError(err.message);
      setSaving(false);
    }
  };

  return (
    <form className="card editor" onSubmit={save}>
      <h1>{id ? 'Edit post' : 'New post'}</h1>
      <label>Title<input value={title} onChange={e => setTitle(e.target.value)} required maxLength={200} /></label>
      <label>Text<textarea value={body} onChange={e => setBody(e.target.value)} required rows={10} maxLength={20000} /></label>

      <div className="images-field">
        <span>Images ({images.length}/{MAX_IMAGES})</span>
        <div className="gallery small">
          {images.map(image => (
            <figure key={image.id}>
              <img src={imageUrl(image.url)} alt="" />
              <button type="button" className="remove" aria-label="Remove image"
                onClick={() => setImages(list => list.filter(i => i.id !== image.id))}>×</button>
            </figure>
          ))}
        </div>
        {uploadsOn && images.length < MAX_IMAGES && (
          <label className="file-pick">
            <input type="file" accept="image/png,image/jpeg,image/gif,image/webp" multiple onChange={pick} />
            {uploading ? 'Uploading…' : 'Add images'}
          </label>
        )}
        <p className="hint">{uploadsOn ? 'PNG, JPEG, GIF or WebP, up to 5 MB each.'
                                       : 'Image uploads are turned off on this server.'}</p>
      </div>

      {error && <p className="error" role="alert">{error}</p>}
      <div className="actions">
        <button disabled={saving || uploading > 0}>{id ? 'Save changes' : 'Publish'}</button>
        <button type="button" className="secondary" onClick={() => navigate(-1)}>Cancel</button>
      </div>
    </form>
  );
}
