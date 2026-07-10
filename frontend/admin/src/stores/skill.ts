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
  storageType: string
  storagePath: string | null
  originalFileName: string | null
  fileManifest: string | null
  createdAt: string
  updatedAt: string
}

export interface SkillFileItem {
  path: string
  size: number
  lastModified: string
}

export interface SkillUploadResponse {
  skill: Skill
  files: SkillFileItem[]
}

// 技能类型显示映射
export const SkillTypeLabels: Record<string, string> = {
  FunctionTool: '函数工具',
  AgentSkill: '知识技能',
  McpTool: 'MCP 工具',
  Tool: '工具(旧)',
  Api: 'API(旧)',
  Script: '脚本(旧)',
  Composite: '组合(旧)'
}

// 存储类型显示映射
export const StorageTypeLabels: Record<string, string> = {
  Inline: '内联',
  File: '文件',
  Directory: '目录'
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

  async function create(data: { name: string; description: string; type: string; implementation: string; inputSchema: string; storageType?: string }) {
    const res = await http.post<{ data: Skill }>('/skills', data)
    skills.value.unshift(res.data.data)
    return res.data.data
  }

  async function update(id: string, data: { name?: string; description?: string; type?: string; implementation?: string; inputSchema?: string; isEnabled?: boolean }) {
    const res = await http.put<{ data: Skill }>(`/skills/${id}`, data)
    const idx = skills.value.findIndex(s => s.id === id)
    if (idx >= 0) skills.value[idx] = res.data.data
    return res.data.data
  }

  async function upload(file: File) {
    const formData = new FormData()
    formData.append('file', file)
    const res = await http.post<{ data: SkillUploadResponse }>('/skills/upload', formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
      timeout: 120000
    })
    skills.value.unshift(res.data.data.skill)
    return res.data.data
  }

  async function getFiles(skillId: string) {
    const res = await http.get<{ data: SkillFileItem[] }>(`/skills/${skillId}/files`)
    return res.data.data
  }

  async function remove(id: string) {
    await http.delete(`/skills/${id}`)
    skills.value = skills.value.filter(s => s.id !== id)
  }

  return { skills, loading, fetchAll, create, update, upload, getFiles, remove }
})