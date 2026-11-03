// Centralized API client configuration
export const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL as string | undefined)?.replace(/\/+$/, '') || 'http://localhost:5000'

export async function apiFetch<T>(endpoint: string, options?: RequestInit): Promise<T> {
  const url = endpoint.startsWith('http') ? endpoint : `${API_BASE_URL}${endpoint.startsWith('/') ? '' : '/'}${endpoint}`
  const res = await fetch(url, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...options?.headers,
    },
    credentials: 'include',
  })

  if (!res.ok) {
    let errorDetail = `Lỗi HTTP: ${res.status}`
    try {
      const json = await res.json()
      if (json && (json.detail || json.message)) {
        errorDetail = json.detail || json.message
      }
    } catch {
      // ignore
    }
    throw new Error(errorDetail)
  }

  return res.json() as Promise<T>
}

