import axios, { AxiosError, InternalAxiosRequestConfig } from 'axios'
import { ElMessage } from 'element-plus'

const http = axios.create({
  baseURL: '/api/v1',
  timeout: 30000,
  headers: { 'Content-Type': 'application/json' }
})

/** 不需要 Token 的白名单路径前缀 */
const noAuthPaths = ['/auth/login', '/health']

// 请求拦截器：自动附加 Token
http.interceptors.request.use(
  (config: InternalAxiosRequestConfig) => {
    const skipAuth = noAuthPaths.some(p => config.url?.startsWith(p))
    if (!skipAuth) {
      // 延迟导入 auth store 避免循环依赖
      const token = tryGetToken()
      if (token) {
        config.headers.Authorization = `Bearer ${token}`
      }
    }
    return config
  },
  error => Promise.reject(error)
)

// 响应拦截器：统一错误处理 + 401 跳转登录
http.interceptors.response.use(
  response => response,
  (error: AxiosError<{ message?: string }>) => {
    if (!error.response) {
      // 网络错误
      ElMessage.error('网络连接失败，请检查网络')
      return Promise.reject(error)
    }

    const { status, data } = error.response
    const msg = data?.message || getDefaultMessage(status)
    ElMessage.error(msg)

    if (status === 401) {
      // Token 过期或未登录，清除 Token 并跳转登录页
      clearToken()
      // 避免在登录页重复跳转
      if (window.location.pathname !== '/login') {
        window.location.href = '/login'
      }
    }

    return Promise.reject(error)
  }
)

/** 从 localStorage 获取 Token */
function tryGetToken(): string | null {
  try {
    return localStorage.getItem('auth_token')
  } catch {
    return null
  }
}

/** 清除认证信息 */
function clearToken() {
  try {
    localStorage.removeItem('auth_token')
    localStorage.removeItem('auth_user')
  } catch { /* ignore */ }
}

function getDefaultMessage(status: number): string {
  const map: Record<number, string> = {
    400: '请求参数错误',
    401: '未授权，请重新登录',
    403: '权限不足',
    404: '请求的资源不存在',
    409: '资源冲突',
    422: '请求格式错误',
    429: '请求过于频繁',
    500: '服务器内部错误',
    502: '网关错误',
    503: '服务暂不可用'
  }
  return map[status] || `请求失败 (${status})`
}

export default http
