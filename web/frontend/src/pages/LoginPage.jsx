import { useState } from 'react';
import { useAuth } from '../auth.jsx';

export default function LoginPage() {
  const { signIn } = useAuth();
  const [mode, setMode] = useState('login');
  const [form, setForm] = useState({ username: '', password: '', displayName: '' });
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const isRegister = mode === 'register';
  const field = name => ({ value: form[name], onChange: e => setForm({ ...form, [name]: e.target.value }) });

  const submit = async event => {
    event.preventDefault();
    setBusy(true);
    setError('');
    try {
      if (isRegister) await signIn('/api/auth/register', form);
      else await signIn('/api/auth/login', { username: form.username, password: form.password });
    } catch (err) {
      setError(err.message);
      setBusy(false);
    }
  };

  return (
    <div className="auth">
      <form className="card auth-card" onSubmit={submit}>
        <h1>TeamBoard</h1>
        <p className="muted">Team posts, projects and tasks in one place.</p>
        <label>Username<input {...field('username')} autoComplete="username" required /></label>
        {isRegister && <label>Display name<input {...field('displayName')} required maxLength={40} /></label>}
        <label>Password<input {...field('password')} type="password" required
          autoComplete={isRegister ? 'new-password' : 'current-password'} minLength={isRegister ? 8 : undefined} /></label>
        {isRegister && <p className="hint">Username: 3–30 letters, numbers or _. Password: at least 8 characters.</p>}
        {error && <p className="error" role="alert">{error}</p>}
        <button disabled={busy}>{isRegister ? 'Create account' : 'Log in'}</button>
        <button type="button" className="link" onClick={() => { setMode(isRegister ? 'login' : 'register'); setError(''); }}>
          {isRegister ? 'I already have an account' : 'Create an account'}
        </button>
      </form>
    </div>
  );
}
