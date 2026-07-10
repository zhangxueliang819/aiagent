<template>
  <el-container class="chat-container">
    <!-- 左侧：会话列表 -->
    <el-aside width="260px" class="chat-sidebar">
      <div style="padding: 12px">
        <el-button type="primary" style="width: 100%" @click="createNewSession" :icon="Plus">
          新建会话
        </el-button>
      </div>
      <div class="session-header">
        <span style="font-size: 13px; font-weight: 600; color: var(--text-primary)">会话列表</span>
        <el-button text size="small" :icon="Delete" @click="handleClearMessages" :disabled="messages.length === 0" style="color: var(--text-muted)">
          清空消息
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
      <div v-if="sessions.length === 0" style="text-align: center; color: var(--text-muted); padding: 20px">
        暂无会话
      </div>
    </el-aside>

    <!-- 右侧：对话区域 -->
    <el-container style="flex-direction: column">
      <!-- 顶部：Agent 选择栏 -->
      <div class="agent-bar">
        <span style="font-size: 14px; color: var(--text-secondary); white-space: nowrap">当前 Agent：</span>
        <el-select
          v-model="selectedAgentId"
          placeholder="选择 Agent"
          size="default"
          filterable
          @change="onAgentChange"
          :disabled="isStreaming"
          style="width: 220px"
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

      <!-- 消息列表 -->
      <el-main class="chat-messages">
        <div v-if="messages.length === 0" style="text-align: center; padding-top: 100px; color: var(--text-muted)">
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
            <div class="assistant-bubble">
              <!-- 思考过程折叠面板 -->
              <el-collapse v-if="msg.thinking" class="thinking-panel" :model-value="['thinking']">
                <el-collapse-item title="🤔 思考过程" name="thinking">
                  <pre style="white-space: pre-wrap; font-size: 12px; color: var(--text-secondary); line-height: 1.6; max-height: 300px; overflow-y: auto; margin: 0">{{ msg.thinking }}</pre>
                </el-collapse-item>
              </el-collapse>
              <div v-if="msg.isStreaming" style="font-style: italic; color: var(--text-muted)">正在输入...</div>
              <div v-else style="white-space: pre-wrap">{{ msg.content }}</div>
              <!-- 模型元信息 -->
              <div v-if="!msg.isStreaming && msg.modelName" style="margin-top: 10px; padding-top: 8px; border-top: 1px solid var(--border-light); font-size: 11px; color: var(--text-muted); display: flex; gap: 16px; flex-wrap: wrap">
                <span>模型: {{ msg.modelName }}</span>
                <span v-if="msg.inputTokens != null">输入: {{ msg.inputTokens }} tokens</span>
                <span v-if="msg.outputTokens != null">输出: {{ msg.outputTokens }} tokens</span>
              </div>
            </div>
            <div style="display: flex; align-items: center; gap: 8px; margin-top: 4px; margin-left: 4px">
              <span v-if="msg.tokenCount" style="font-size: 11px; color: var(--text-muted)">
                {{ msg.tokenCount }} tokens
              </span>
              <template v-if="!msg.isStreaming && msg.content">
                <el-button size="small" text :icon="CopyDocument" @click="copyText(msg.content)">复制</el-button>
                <el-button
                  v-if="idx === messages.length - 1"
                  size="small" text :icon="Refresh" @click="regenerateLast"
                >重新生成</el-button>
                <el-button v-if="msg.rawResponse" size="small" text :icon="View" @click="showRawResponse(msg)">完整响应</el-button>
              </template>
            </div>
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
                    <div style="font-size: 12px; color: var(--text-muted); margin-bottom: 4px">参数：</div>
                    <pre style="background: #f8f8f8; padding: 8px; border-radius: 4px; font-size: 12px; overflow-x: auto">{{ JSON.stringify(tc.arguments, null, 2) }}</pre>
                    <div style="font-size: 12px; color: var(--text-muted); margin: 8px 0 4px">结果：</div>
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
          <span style="margin-left: 8px; color: var(--text-muted)">Agent 正在处理...</span>
        </div>
      </el-main>

      <!-- 输入区域 -->
      <div class="input-area">
        <div style="display: flex; align-items: flex-end; gap: 8px">
          <div style="flex: 1; position: relative">
            <el-input
              v-model="inputMessage"
              type="textarea"
              :rows="2"
              placeholder="输入消息，Enter 发送，Shift+Enter 换行"
              resize="none"
              :disabled="isStreaming"
              :maxlength="4000"
              @keydown.enter.exact.prevent="sendMessage"
            />
            <div style="position: absolute; right: 10px; bottom: 4px; font-size: 11px; color: var(--text-muted); pointer-events: none">
              {{ inputMessage.length }}/4000
            </div>
          </div>
          <el-button
            type="primary"
            :icon="Promotion"
            :disabled="!inputMessage.trim() || isStreaming"
            @click="sendMessage"
          >
            发送
          </el-button>
        </div>
        <div v-if="memoryTokens > 0" style="margin-top: 4px; font-size: 11px; color: var(--text-muted); text-align: right">
          上下文: {{ memoryTokens }} tokens
        </div>
      </div>
    </el-container>
  </el-container>

  <!-- 完整响应抽屉 -->
  <el-drawer v-model="drawerVisible" title="完整响应" size="50%">
    <div style="font-size: 12px; color: var(--text-muted); margin-bottom: 12px">模型返回的原始 JSON 响应</div>
    <pre class="raw-response-pre">{{ drawerRawResponse }}</pre>
  </el-drawer>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { Plus, Promotion, ChatDotSquare, Loading, Tools, Delete, CopyDocument, Refresh, View } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import http from '../api/http'

interface ChatMessage {
  role: 'user' | 'assistant' | 'system'
  content: string
  isStreaming?: boolean
  tokenCount?: number
  toolCalls?: ToolCall[]
  thinking?: string
  modelName?: string
  inputTokens?: number
  outputTokens?: number
  rawResponse?: string
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
const drawerVisible = ref(false)
const drawerRawResponse = ref('')

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

function copyText(text: string) {
  navigator.clipboard.writeText(text).then(() => {
    ElMessage.success('已复制')
  }).catch(() => {
    ElMessage.warning('复制失败')
  })
}

function handleClearMessages() {
  messages.value = []
  memoryTokens.value = 0
  ElMessage.success('消息已清空')
}

function regenerateLast() {
  // 找到最后一条用户消息重新发送
  const lastUserMsg = [...messages.value].reverse().find(m => m.role === 'user')
  if (lastUserMsg) {
    messages.value = messages.value.slice(0, messages.value.length - 1)
    inputMessage.value = lastUserMsg.content
    sendMessage()
  }
}

function showRawResponse(msg: ChatMessage) {
  drawerRawResponse.value = msg.rawResponse || ''
  drawerVisible.value = true
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

            case 'thinking':
              assistantMsg.thinking = data.content
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
              assistantMsg.modelName = data.modelName
              assistantMsg.inputTokens = data.inputTokens
              assistantMsg.outputTokens = data.outputTokens
              assistantMsg.rawResponse = data.rawResponse
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

/* ── 深色模式兼容 ── */
.chat-container {
  height: calc(100vh - var(--layout-header-height) - 2 * var(--layout-content-padding));
}
.chat-sidebar {
  background: var(--bg-card);
  border-right: 1px solid var(--border-color);
  overflow-y: auto;
}
.session-header {
  padding: 6px 12px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  border-bottom: 1px solid var(--border-light);
}
.agent-bar {
  padding: 12px 20px;
  background: var(--bg-card);
  border-bottom: 1px solid var(--border-color);
  display: flex;
  align-items: center;
  gap: 12px;
}
.chat-messages {
  flex: 1;
  overflow-y: auto;
  background: var(--bg-page);
  padding: 20px;
}
.assistant-bubble {
  max-width: 85%;
  background: var(--bg-card);
  padding: 12px 16px;
  border-radius: 0 12px 12px 12px;
  word-break: break-word;
  box-shadow: var(--shadow-sm);
}
.thinking-panel {
  margin-bottom: 8px;
  background: var(--color-primary-50);
  border-radius: 6px;
}
.input-area {
  padding: 12px 20px 8px;
  background: var(--bg-card);
  border-top: 1px solid var(--border-color);
}
.raw-response-pre {
  background: var(--bg-page);
  padding: 16px;
  border-radius: 6px;
  font-size: 12px;
  line-height: 1.6;
  overflow: auto;
  white-space: pre-wrap;
  word-break: break-all;
  max-height: calc(100vh - 200px);
}
</style>
