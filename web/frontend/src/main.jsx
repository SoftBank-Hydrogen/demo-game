import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';
import { loadConfig } from './api.js';
import { AuthProvider } from './auth.jsx';
import App from './App.jsx';
import './styles.css';

const root = createRoot(document.getElementById('root'));

// Read the API address before the first screen, so every request goes to the right server.
loadConfig()
  .then(() => root.render(
    <StrictMode>
      <BrowserRouter>
        <AuthProvider>
          <App />
        </AuthProvider>
      </BrowserRouter>
    </StrictMode>,
  ))
  .catch(error => root.render(<p className="fatal">TeamBoard could not start: {error.message}</p>));
