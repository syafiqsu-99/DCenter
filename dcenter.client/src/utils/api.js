import axios from 'axios';

// Relative baseURL relies on the Vite dev proxy (and same-origin in production).
const api = axios.create({ baseURL: '/api' });

export default api;
