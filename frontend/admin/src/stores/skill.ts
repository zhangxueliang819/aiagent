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

/** 已注册的 FunctionTool 执行器类型 */
export interface ExecutorType {
  name: string
  description: string
  inputSchema: string
}

// 技能类型显示映射
export const SkillTypeLabels: Record<string, string> = {
  FunctionTool: '函数工具',
  AgentSkill: '知识技能',
  McpTool: 'MCP 工具',
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
  const executorTypes = ref<ExecutorType[]>([])

  async function fetchAll() {
    loading.value = true
    try {
      const res = await http.get<{ data: Skill[] }>('/skills')
      skills.value = res.data.data
    } finally {
      loading.value = false
    }
  }

  /** 获取已注册的 FunctionTool 执行器列表 */
  async function fetchExecutorTypes() {
    try {
      const res = await http.get<{ data: ExecutorType[] }>('/skills/executor-types')
      executorTypes.value = res.data.data
    } catch {
      executorTypes.value = []
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

  /** 获取技能包内单个文件文本内容 */
  async function getFileContent(skillId: string, filePath: string) {
    const encoded = encodeURIComponent(filePath)
    const res = await http.get<{ data: { content: string; fileName: string } }>(`/skills/${skillId}/files/text/${encoded}`)
    return res.data.data
  }

  /** 更新技能包内单个文件内容 */
  async function updateFileContent(skillId: string, filePath: string, content: string) {
    await http.put(`/skills/${skillId}/files/${encodeURIComponent(filePath)}`, { content })
  }

  return { skills, loading, executorTypes, fetchAll, fetchExecutorTypes, create, update, upload, getFiles, remove, getFileContent, updateFileContent }
})