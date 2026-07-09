import { defineStore } from 'pinia'
import { ref } from 'vue'
import http from '../api/http'

export interface Skill {
  id: string
  name: string
  description: string
  type: string
  implementation: string
  inputSchema: string
  isEnabled: boolean
  createdAt: string
}

export const useSkillStore = defineStore('skill', () => {
  const skills = ref<Skill[]>([])
  const loading = ref(false)

  async function fetchAll() {
    loading.value = true
    try {
      const res = await http.get<{ data: Skill[] }>('/skills')
      skills.value = res.data.data
    } finally {
      loading.value = false
    }
  }

  async function create(data: { name: string; description: string; type: string; implementation: string; inputSchema: string }) {
    const res = await http.post<{ data: Skill }>('/skills', data)
    skills.value.unshift(res.data.data)
    return res.data.data
  }

  async function update(id: string, data: { name?: string; description?: string; type?: string; implementation?: string; inputSchema?: string }) {
    const res = await http.put<{ data: Skill }>(`/skills/${id}`, data)
    const idx = skills.value.findIndex(s => s.id === id)
    if (idx >= 0) skills.value[idx] = res.data.data
    return res.data.data
  }

  async function remove(id: string) {
    await http.delete(`/skills/${id}`)
    skills.value = skills.value.filter(s => s.id !== id)
  }

  return { skills, loading, fetchAll, create, update, remove }
})