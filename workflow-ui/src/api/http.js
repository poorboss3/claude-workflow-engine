import axios from 'axios'
import { useUserStore } from '../stores/userStore.js'

const http = axios.create({
  baseURL: '/api/v1',
  timeout: 15000,
  headers: {
    'Content-Type': 'application/json'
  }
})

http.interceptors.request.use(
  (config) => {
    try {
      const userStore = useUserStore()
      if (userStore.userId) {
        config.headers['X-User-Id'] = userStore.userId
      }
    } catch (e) {
      // store not ready
    }
    return config
  },
  (error) => Promise.reject(error)
)

http.interceptors.response.use(
  (response) => {
    // Unwrap ApiResponse<T> envelope: { success, data, error }
    const body = response.data
    if (body && typeof body === 'object' && 'success' in body) {
      if (!body.success) {
        const msg = body.error?.message || '请求失败'
        return Promise.reject(new Error(msg))
      }
      response.data = body.data
    }
    return response
  },
  (error) => {
    const msg = error.response?.data?.error?.message
      || error.response?.data?.message
      || error.response?.data?.title
      || error.message
      || '请求失败'
    return Promise.reject(new Error(msg))
  }
)

export default http
