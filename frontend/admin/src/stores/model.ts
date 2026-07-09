import { defineStore } from 'pinia'
import { ref } from 'vue'
import http from '../api/http'

export interface ModelEndpoint {
  id: string
  modelId: string
  modelName: string
  maxTokens: number
  isEnabled: boolean
}

export interface ModelProvider {
  id: string
  name: string
  providerType: string
  apiBaseUrl: string
  isEnabled: boolean
  createdAt: string
  endpoints: ModelEndpoint[]
}

export const useModelStore = defineStore('model', () => {
  const providers = ref<ModelProvider[]>([])
  const loading = ref(false)

  async function fetchAll() {
    loading.value = true
    try {
      const res = await http.get<{ data: ModelProvider[] }>('/models')
      providers.value = res.data.data
    } finally {
      loading.value = false
    }
  }

  async function create(data: { name: string; providerType: string; apiBaseUrl: string; apiKey: string }) {
    const res = await http.post<{ data: ModelProvider }>('/models', data)
    providers.value.unshift(res.data.data)
    return res.data.data
  }

  async function update(id: string, data: { name: string; providerType: string; apiBaseUrl: string; apiKey: string }) {
    const res = await http.put<{ data: ModelProvider }>(`/models/${id}`, data)
    const idx = providers.value.findIndex(p => p.id === id)
    if (idx !== -1) providers.value[idx] = res.data.data
    return res.data.data
  }

  async function remove(id: string) {
    await http.delete(`/models/${id}`)
    providers.value = providers.value.filter(p => p.id !== id)
  }

  // ─── Endpoint CRUD ──────────────────────────────────────────

  async function addEndpoint(providerId: string, data: { modelId: string; modelName: string; maxTokens: number }) {
    const res = await http.post<{ data: ModelEndpoint }>(`/models/${providerId}/endpoints`, data)
    const provider = providers.value.find(p => p.id === providerId)
    if (provider) provider.endpoints.push(res.data.data)
    return res.data.data
  }

  async function removeEndpoint(providerId: string, endpointId: string) {
    await http.delete(`/models/${providerId}/endpoints/${endpointId}`)
    const provider = providers.value.find(p => p.id === providerId)
    if (provider) provider.endpoints = provider.endpoints.filter(e => e.id !== endpointId)
  }

  return { providers, loading, fetchAll, create, update, remove, addEndpoint, removeEndpoint }
})
