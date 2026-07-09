import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import http from '../api/http'

export interface UserInfo {
  username: string
  displayName: string
  role: string
  token: string
}

export const useAuthStore = defineStore('auth', () => {
  const token = ref<string | null>(localStorage.getItem('auth_token'))
  const user = ref<UserInfo | null>(loadUserFromStorage())

  const isAuthenticated = computed(() => !!token.value)
  const displayName = computed(() => user.value?.displayName || '未登录')
  const userRole = computed(() => user.value?.role || '')

  function loadUserFromStorage(): UserInfo | null {
    try {
      const raw = localStorage.getItem('auth_user')
      return raw ? JSON.parse(raw) : null
    } catch {
      return null
    }
  }

  async function login(username: string, password: string) {
    const res = await http.post<{ data: UserInfo }>('/auth/login', { username, password })
    const userData = res.data.data

    token.value = userData.token
    user.value = userData

    localStorage.setItem('auth_token', userData.token)
    localStorage.setItem('auth_user', JSON.stringify(userData))

    return userData
  }

  function logout() {
    token.value = null
    user.value = null
    localStorage.removeItem('auth_token')
    localStorage.removeItem('auth_user')
  }

  return { token, user, isAuthenticated, displayName, userRole, login, logout }
})
