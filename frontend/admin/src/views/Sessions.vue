<template>
  <div>
    <el-card>
      <template #header>
        <div style="display: flex; justify-content: space-between; align-items: center">
          <span>会话记录</span>
          <el-button type="primary" @click="loadSessions">刷新</el-button>
        </div>
      </template>

      <el-table :data="sessions" stripe style="width: 100%" @row-click="openDetail" v-loading="loading">
        <el-table-column prop="title" label="标题" min-width="200">
          <template #default="{ row }">
            <el-link type="primary" @click.stop="openDetail(row)">{{ row.title || '新会话' }}</el-link>
          </template>
        </el-table-column>
        <el-table-column prop="agentName" label="Agent" width="150" />
        <el-table-column prop="userId" label="用户" width="120" />
        <el-table-column prop="messageCount" label="消息数" width="100" align="center" />
        <el-table-column prop="status" label="状态" width="100">
          <template #default="{ row }">
            <el-tag v-if="row.status === 'Active'" type="success" size="small">活跃</el-tag>
            <el-tag v-else-if="row.status === 'Completed'" type="info" size="small">完成</el-tag>
            <el-tag v-else type="warning" size="small">{{ row.status }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="createdAt" label="创建时间" width="180">
          <template #default="{ row }">
            {{ new Date(row.createdAt).toLocaleString() }}
          </template>
        </el-table-column>
        <el-table-column label="操作" width="120" fixed="right">
          <template #default="{ row }">
            <el-button size="small" type="primary" @click.stop="openDetail(row)">查看</el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <!-- 消息追溯对话框 -->
    <el-dialog
      v-model="showDetail"
      :title="`会话详情 - ${selectedSession?.title || '新会话'}`"
      width="800px"
      destroy-on-close
    >
      <div v-if="sessionMessages.length > 0" style="max-height: 500px; overflow-y: auto">
        <div
          v-for="(msg, idx) in sessionMessages"
          :key="idx"
          style="margin-bottom: 12px"
        >
          <div style="display: flex; align-items: center; margin-bottom: 4px">
            <el-tag
              :type="msg.role === 'user' ? 'primary' : msg.role === 'assistant' ? 'success' : 'info'"
              size="small"
              style="margin-right: 8px"
            >
              {{ msg.role === 'user' ? '用户' : msg.role === 'assistant' ? '助手' : msg.role }}
            </el-tag>
            <span style="font-size: 12px; color: #999">
              {{ new Date(msg.createdAt).toLocaleTimeString() }}
            </span>
            <span v-if="msg.tokenCount" style="font-size: 11px; color: #bbb; margin-left: auto">
              {{ msg.tokenCount }} tokens
            </span>
          </div>
          <div
            :style="{
              padding: '10px 14px',
              borderRadius: '8px',
              background: msg.role === 'user' ? '#e6f4ff' : msg.role === 'assistant' ? '#f6ffed' : '#fff7e6',
              border: msg.role === 'user' ? '1px solid #91caff' : msg.role === 'assistant' ? '1px solid #b7eb8f' : '1px solid #ffd591',
              whiteSpace: 'pre-wrap',
              wordBreak: 'break-word',
              fontSize: '14px'
            }"
          >
            {{ msg.content }}
          </div>
        </div>
      </div>
      <div v-else style="text-align: center; padding: 40px; color: #999">
        暂无对话记录
      </div>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import http from '../api/http'

interface SessionItem {
  id: string
  title: string
  agentName: string
  userId: string
  messageCount: number
  status: string
  createdAt: string
}

interface ConversationMsg {
  role: string
  content: string
  tokenCount: number
  createdAt: string
}

const sessions = ref<SessionItem[]>([])
const loading = ref(false)
const showDetail = ref(false)
const selectedSession = ref<SessionItem | null>(null)
const sessionMessages = ref<ConversationMsg[]>([])

async function loadSessions() {
  loading.value = true
  try {
    const res = await http.get('/sessions?userId=anonymous')
    const data = res.data.data || res.data || []
    sessions.value = (Array.isArray(data) ? data : []).map((s: any) => ({
      id: s.id,
      title: s.title,
      agentName: s.agentName || 'N/A',
      userId: s.userId || 'anonymous',
      messageCount: s.conversations?.length || 0,
      status: s.status,
      createdAt: s.createdAt
    }))
  } catch (e) {
    ElMessage.error('加载会话列表失败')
    console.error(e)
  } finally {
    loading.value = false
  }
}

async function openDetail(session: SessionItem) {
  selectedSession.value = session
  showDetail.value = true
  try {
    const res = await http.get(`/sessions/${session.id}`)
    const data = res.data.data || res.data
    sessionMessages.value = (data.conversations || []).map((c: any) => ({
      role: c.role,
      content: c.content,
      tokenCount: c.tokenCount,
      createdAt: c.createdAt
    }))
    session.messageCount = sessionMessages.value.length
  } catch (e) {
    console.error('加载会话详情失败', e)
    sessionMessages.value = []
  }
}

onMounted(() => {
  loadSessions()
})
</script>
