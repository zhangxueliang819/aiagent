import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import http from '../api/http'

export interface Agent {
  id: string
  name: string
  description: string
  systemPrompt: string
  modelId: string
  modelEndpointId: string | null
  modelEndpointName: string | null
  status: string
  version: string
  createdBy: string
  createdAt: string
  updatedAt: string
  configurations: { id: string; key: string; value: string; valueType: string }[]
  skills: { id: string; skillId: string; priority: number; isEnabled: boolean }[]
}

export interface AgentSkillBinding {
  bindingId: string
  targetId: string
  targetName: string
  priority: number
  isEnabled: boolean
}

export interface AgentMcpBinding {
  bindingId: string
  targetId: string
  targetName: string
  priority: number
  isEnabled: boolean
}

export const useAgentStore = defineStore('agent', () => {
  const agents = ref<Agent[]>([])
  const current = ref<Agent | null>(null)
  const loading = ref(false)

  const activeAgents = computed(() => agents.value.filter(a => a.status === 'Active'))

  async function fetchAll() {
    loading.value = true
    try {
      const res = await http.get<{ data: Agent[] }>('/agents')
      agents.value = res.data.data
    } finally {
      loading.value = false
    }
  }

  async function fetchById(id: string) {
    const res = await http.get<{ data: Agent }>(`/agents/${id}`)
    current.value = res.data.data
    return res.data.data
  }

  async function create(data: { name: string; description: string; systemPrompt: string; modelId: string; modelEndpointId?: string | null; createdBy: string }) {
    const res = await http.post<{ data: Agent }>('/agents', data)
    agents.value.unshift(res.data.data)
    return res.data.data
  }

  async function update(id: string, data: { name?: string; description?: string; systemPrompt?: string; modelId?: string; modelEndpointId?: string | null; status?: string }) {
    const res = await http.put<{ data: Agent }>(`/agents/${id}`, data)
    const idx = agents.value.findIndex(a => a.id === id)
    if (idx >= 0) agents.value[idx] = res.data.data
    return res.data.data
  }

  async function remove(id: string) {
    await http.delete(`/agents/${id}`)
    agents.value = agents.value.filter(a => a.id !== id)
  }

  // === Skill Bindings ===

  async function fetchSkills(agentId: string) {
    const res = await http.get<{ data: AgentSkillBinding[] }>(`/agents/${agentId}/bindings/skills`)
    return res.data.data
  }

  async function bindSkill(agentId: string, skillId: string, priority: number) {
    const res = await http.post<{ data: AgentSkillBinding }>(`/agents/${agentId}/bindings/skills`, { targetId: skillId, priority })
    return res.data.data
  }

  async function unbindSkill(agentId: string, bindingId: string) {
    await http.delete(`/agents/${agentId}/bindings/skills/${bindingId}`)
  }

  // === MCP Bindings ===

  async function fetchMcpEndpoints(agentId: string) {
    const res = await http.get<{ data: AgentMcpBinding[] }>(`/agents/${agentId}/bindings/mcp`)
    return res.data.data
  }

  async function bindMcp(agentId: string, mcpId: string, priority: number) {
    const res = await http.post<{ data: AgentMcpBinding }>(`/agents/${agentId}/bindings/mcp`, { targetId: mcpId, priority })
    return res.data.data
  }

  async function unbindMcp(agentId: string, bindingId: string) {
    await http.delete(`/agents/${agentId}/bindings/mcp/${bindingId}`)
  }

  return { agents, current, loading, activeAgents,
    fetchAll, fetchById, create, update, remove,
    fetchSkills, bindSkill, unbindSkill,
    fetchMcpEndpoints, bindMcp, unbindMcp }
})
