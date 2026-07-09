<template>
  <el-container style="height: calc(100vh - 120px)">
    <!-- 左侧：会话列表 -->
    <el-aside width="260px" style="background: #fff; border-right: 1px solid #e6e6e6; overflow-y: auto">
      <div style="padding: 12px">
        <el-button type="primary" style="width: 100%" @click="createNewSession" :icon="Plus">
          新建会话
        </el-button>
      </div>
      <el-menu :default-active="activeSessionId" @select="switchSession">
        <el-menu-item
          v-for="s in sessions"
          :key="s.id"
          :index="s.id"
        >
          <span style="overflow: hidden; text-overflow: ellipsis; white-space: nowrap">
            {{ s.title || '新会话' }}
          </span>
          <template #title>
            <div style="display: flex; justify-content: space-between; align-items: center; width: 100%">
              <span style="overflow: hidden; text-overflow: ellipsis; white-space: nowrap; flex: 1">
                {{ s.title || '新会话' }}
              </span>
              <el-tag v-if="s.status === 'Active'" type="success" size="small">活跃</el-tag>
              <el-tag v-else-if="s.status === 'Completed'" type="info" size="small">完成</el-tag>
            </div>
          </template>
        </el-menu-item>
      </el-menu>
      <div v-if="sessions.length === 0" style="text-align: center; color: #999; padding: 20px">
        暂无会话
      </div>
    </el-aside>

    <!-- 右侧：对话区域 -->
    <el-container style="flex-direction: column">
      <!-- 消息列表 -->
      <el-main style="flex: 1; overflow-y: auto; background: #f5f5f5; padding: 20px">
        <div v-if="messages.length === 0" style="text-align: center; padding-top: 100px; color: #999">
          <el-icon :size="48"><ChatDotSquare /></el-icon>
          <p style="margin-top: 16px">开始与 Agent 对话</p>
        </div>

        <div v-for="(msg, idx) in messages" :key="idx" style="margin-bottom: 16px">
          <!-- 用户消息 -->
          <div v-if="msg.role === 'user'" style="display: flex; justify-content: flex-end">
            <div style="max-width: 70%; background: #409eff; color: #fff; padding: 10px 16px; border-radius: 12px 12px 0 12px; word-break: break-word">
              {{ msg.content }}
            </div>
          </div>

          <!-- 助手消息 -->
          <div v-else-if="msg.role === 'assistant'" style="display: flex; justify-content: flex-start; flex-direction: column">
            <div style="max-width: 85%; background: #fff; padding: 12px 16px; border-radius: 0 12px 12px 12px; word-break: break-word; box-shadow: 0 1px 2px rgba(0,0,0,0.05)">
              <div v-if="msg.isStreaming" style="font-style: italic; color: #999">正在输入...</div>
              <div v-else style="white-space: pre-wrap">{{ msg.content }}</div>
            </div>
            <span v-if="msg.tokenCount" style="font-size: 11px; color: #bbb; margin-top: 4px; margin-left: 4px">
              {{ msg.tokenCount }} tokens
            </span>
          </div>

          <!-- 工具调用消息 -->
          <div v-if="msg.toolCalls && msg.toolCalls.length > 0" style="display: flex; justify-content: flex-start">
            <div style="max-width: 85%">
              <el-collapse style="background: #fdf6ec; border: 1px solid #faecd8; border-radius: 8px">
                <el-collapse-item
                  v-for="(tc, tIdx) in msg.toolCalls"
                  :key="tIdx"
                >
                  <template #title>
                    <el-icon style="margin-right: 6px"><Tools /></el-icon>
                    <span style="font-weight: 500">🔧 {{ tc.name }}</span>
                    <el-tag size="small" type="success" style="margin-left: 8px">完成</el-tag>
                  </template>
                  <div style="padding: 8px">
                    <div style="font-size: 12px; color: #999; margin-bottom: 4px">参数：</div>
                    <pre style="background: #f8f8f8; padding: 8px; border-radius: 4px; font-size: 12px; overflow-x: auto">{{ JSON.stringify(tc.arguments, null, 2) }}</pre>
                    <div style="font-size: 12px; color: #999; margin: 8px 0 4px">结果：</div>
                    <pre style="background: #f8f8f8; padding: 8px; border-radius: 4px; font-size: 12px; overflow-x: auto; max-height: 120px">{{ tc.result }}</pre>
                  </div>
                </el-collapse-item>
              </el-collapse>
            </div>
          </div>
        </div>

        <!-- 加载指示器 -->
        <div v-if="isStreaming" style="text-align: center; padding: 12px">
          <el-icon class="is-loading" :size="20"><Loading /></el-icon>
          <span style="margin-left: 8px; color: #999">Agent 正在处理...</span>
        </div>
      </el-main>

      <!-- 输入区域 -->
      <div style="padding: 12px 20px; background: #fff; border-top: 1px solid #e6e6e6">
        <div style="display: flex; align-items: flex-end; gap: 8px">
          <!-- Agent 选择器 -->
          <div style="min-width: 160px">
            <el-select
              v-model="selectedAgentId"
              placeholder="选择 Agent"
              size="default"
              filterable
              @change="onAgentChange"
              :disabled="isStreaming"
              style="width: 100%"
            >
              <el-option
                v-for="a in agents"
                :key="a.id"
                :label="a.name"
                :value="a.id"
              >
                <span>{{ a.name }}</span>
              </el-option>
            </el-select>
          </div>
          <el-input
            v-model="inputMessage"
            type="textarea"
            :rows="2"
            placeholder="输入消息，Enter 发送，Shift+Enter 换行"
            resize="none"
            :disabled="isStreaming"
            @keydown.enter.exact.prevent="sendMessage"
          />
          <el-button
            type="primary"
            :icon="Promotion"
            :disabled="!inputMessage.trim() || isStreaming"
            @click="sendMessage"
          >
            发送
          </el-button>
        </div>
        <div v-if="memoryTokens > 0" style="margin-top: 4px; font-size: 11px; color: #bbb; text-align: right">
          上下文: {{ memoryTokens }} tokens
        </div>
      </div>
    </el-container>
  </el-container>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { Plus, Promotion, ChatDotSquare, Loading, Tools } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import http from '../api/http'

interface ChatMessage {
  role: 'user' | 'assistant' | 'system'
  content: string
  isStreaming?: boolean
  tokenCount?: number
  toolCalls?: ToolCall[]
}

interface ToolCall {
  name: string
  arguments: Record<string, unknown>
  result: string
}

interface SessionInfo {
  id: string
  title: string
  status: string
  createdAt: string
}

interface AgentInfo {
  id: string
  name: string
}

const sessions = ref<SessionInfo[]>([])
const activeSessionId = ref<string>('')
const messages = ref<ChatMessage[]>([])
const inputMessage = ref('')
const isStreaming = ref(false)
const memoryTokens = ref(0)
const agents = ref<AgentInfo[]>([])
const currentAgent = ref<AgentInfo | null>(null)
const selectedAgentId = ref<string>('')

// 新建会话
async function createNewSession() {
  if (agents.value.length === 0) {
    ElMessage.warning('请先在 Agent 管理页创建至少一个 Agent')
    return
  }
  if (!selectedAgentId.value) {
    ElMessage.warning('请先选择一个 Agent')
    return
  }
  const agent = agents.value.find(a => a.id === selectedAgentId.value)
  if (!agent) {
    ElMessage.warning('所选 Agent 不存在')
    return
  }
  currentAgent.value = agent
  activeSessionId.value = ''
  messages.value = []
  memoryTokens.value = 0
  ElMessage.success('新会话已创建，输入消息开始对话')
}

// 切换会话
async function switchSession(sessionId: string) {
  activeSessionId.value = sessionId
  // 加载会话消息历史
  loadSessionMessages(sessionId)
}

// 加载会话消息
async function loadSessionMessages(sessionId: string) {
  try {
    const res = await http.get(`/sessions/${sessionId}`)
    const session = res.data.data || res.data
    if (session && session.conversations) {
      messages.value = session.conversations.map((c: any) => ({
        role: c.role,
        content: c.content,
        tokenCount: c.tokenCount
      }))
    }
  } catch (e) {
    console.error('加载会话消息失败', e)
  }
}

// 加载会话列表
async function loadSessions() {
  try {
    const res = await http.get('/sessions?userId=anonymous')
    const data = res.data.data || res.data || []
    sessions.value = Array.isArray(data) ? data.map((s: any) => ({
      id: s.id,
      title: s.title,
      status: s.status,
      createdAt: s.createdAt
    })) : []
  } catch (e) {
    console.error('加载会话列表失败', e)
  }
}

// 发送消息（使用 SSE 流式端点）
async function sendMessage() {
  const content = inputMessage.value.trim()
  if (!content || isStreaming.value || !currentAgent.value) return

  inputMessage.value = ''

  // 添加用户消息
  messages.value.push({ role: 'user', content })
  isStreaming.value = true

  // 添加助手消息占位（流式填充）
  const assistantMsg: ChatMessage = {
    role: 'assistant',
    content: '',
    isStreaming: true,
    toolCalls: []
  }
  messages.value.push(assistantMsg)
  const assistantIdx = messages.value.length - 1

  try {
    const response = await fetch(`/api/v1/agents/${currentAgent.value.id}/chat/stream`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ message: content, userId: 'anonymous', sessionId: activeSessionId.value || undefined })
    })

    if (!response.ok) {
      throw new Error(`HTTP ${response.status}`)
    }

    const reader = response.body?.getReader()
    if (!reader) throw new Error('No response body')

    const decoder = new TextDecoder()
    let buffer = ''
    let fullContent = ''
    let finalToolCount = 0
    let finalMemTokens = 0

    while (true) {
      const { done, value } = await reader.read()
      if (done) break

      buffer += decoder.decode(value, { stream: true })
      const lines = buffer.split('\n')
      buffer = lines.pop() || ''

      for (const line of lines) {
        if (!line.startsWith('data: ')) continue
        const jsonStr = line.slice(6).trim()
        if (!jsonStr) continue

        try {
          const data = JSON.parse(jsonStr)

          switch (data.type) {
            case 'session':
              if (!activeSessionId.value && data.sessionId) {
                activeSessionId.value = data.sessionId
                loadSessions()
              }
              break

            case 'token':
              fullContent += data.content
              assistantMsg.content = fullContent
              assistantMsg.isStreaming = false
              break

            case 'tool_call':
              assistantMsg.toolCalls = assistantMsg.toolCalls || []
              assistantMsg.toolCalls.push({
                name: data.name,
                arguments: data.arguments || {},
                result: data.result || ''
              })
              break

            case 'done':
              finalToolCount = data.toolCallCount
              finalMemTokens = data.memoryTokens
              memoryTokens.value = finalMemTokens
              if (!assistantMsg.content && assistantMsg.toolCalls?.length) {
                assistantMsg.content = `完成 ${assistantMsg.toolCalls.length} 次工具调用`
              }
              break

            case 'error':
              assistantMsg.content = `发生错误: ${data.message}`
              assistantMsg.isStreaming = false
              break
          }
        } catch {
          // 忽略解析错误
        }
      }
    }

    assistantMsg.isStreaming = false
    if (!assistantMsg.content) {
      assistantMsg.content = '（无回复）'
    }
  } catch (e: any) {
    assistantMsg.content = `请求失败: ${e.message}`
    assistantMsg.isStreaming = false
  } finally {
    isStreaming.value = false
  }
}

function onAgentChange(agentId: string) {
  const agent = agents.value.find(a => a.id === agentId)
  if (agent) {
    currentAgent.value = agent
  }
}

async function loadAgents() {
  try {
    const res = await http.get('/agents')
    if (res.data.success || Array.isArray(res.data)) {
      agents.value = (res.data.data || res.data || []).map((a: any) => ({
        id: a.id,
        name: a.name
      }))
      // 默认选中第一个
      if (agents.value.length > 0 && !selectedAgentId.value) {
        selectedAgentId.value = agents.value[0].id
        currentAgent.value = agents.value[0]
      }
    }
  } catch (e) {
    console.error('加载 Agent 列表失败', e)
  }
}

onMounted(async () => {
  await loadAgents()
  await loadSessions()
})
</script>

<style scoped>
.el-menu {
  border-right: none;
}
.el-menu-item {
  height: 48px;
  line-height: 48px;
}
</style>
