import axios from 'axios';

const apiClient = axios.create({
    baseURL: import.meta.env.VITE_API_URL || 'http://localhost:5005',
    headers: {
        'Content-Type': 'application/json'
    },
    timeout: 10000,
    paramsSerializer: (params) => {
        const sp = new URLSearchParams();
        for (const [key, val] of Object.entries(params)) {
            if (Array.isArray(val)) val.forEach(v => sp.append(key, String(v)));
            else if (val !== undefined && val !== null) sp.append(key, String(val));
        }
        return sp.toString();
    }
});

export default apiClient;
