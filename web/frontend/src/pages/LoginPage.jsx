import { useState } from 'react';
import { useAuth } from '../auth.jsx';

// One form for everyone: a new name creates the account, a known name logs in.
export default function LoginPage() {
  const { signIn } = useAuth();
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  const submit = async event => {
    event.preventDefault();
    setBusy(true);
    setError('');
    try {
      await signIn('/api/auth/login', { username, password });
    } catch (err) {
      setError(err.message);
      setBusy(false);
    }
  };

  return (
    <div className="auth">
      <form className="card auth-card" onSubmit={submit}>
        <h1>SkyBoard</h1>
        <p className="muted">Team posts, projects and tasks in one place.</p>
        <label>Name<input value={username} onChange={e => setUsername(e.target.value)} autoComplete="username" required maxLength={30} /></label>
        <label>Password<input value={password} onChange={e => setPassword(e.target.value)} type="password" autoComplete="current-password" required /></label>
        <p className="hint">New name? It is created right away with this password.</p>
        {error && <p className="error" role="alert">{error}</p>}
        <button disabled={busy}>Enter</button>
      </form>
    </div>
  );
}
