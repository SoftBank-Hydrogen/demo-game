import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { HashRouter } from 'react-router-dom';
import { loadConfig } from './api.js';
import { AuthProvider } from './auth.jsx';
import App from './App.jsx';
import './styles.css';

const root = createRoot(document.getElementById('root'));

// Read the API address before the first screen, so every request goes to the right server.
loadConfig()
  .then(() => root.render(
    <StrictMode>
      <HashRouter>
        <AuthProvider>
          <App />
        </AuthProvider>
      </HashRouter>
    </StrictMode>,
  ))
  .catch(error => root.render(<p className="fatal">SkyBoard could not start: {error.message}</p>));
