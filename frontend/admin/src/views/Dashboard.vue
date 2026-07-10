<template>
  <div>
    <!-- 统计卡片 -->
    <el-row :gutter="16" class="mb-lg">
      <el-col :xs="12" :sm="12" :md="6" v-for="card in statCards" :key="card.label">
        <el-card shadow="hover" class="stat-card" :body-style="{ padding: '20px' }">
          <div class="stat-accent" :style="{ background: card.color }" />
          <div class="stat-content">
            <div class="stat-value" :style="{ color: card.color }">
              {{ card.value }}
            </div>
            <div class="stat-label">{{ card.label }}</div>
          </div>
          <el-icon class="stat-icon" :style="{ color: card.color }">
            <component :is="card.icon" />
          </el-icon>
        </el-card>
      </el-col>
    </el-row>

    <el-row :gutter="16" class="mb-lg">
      <!-- 系统状态 -->
      <el-col :xs="24" :md="12" class="mb-md">
        <el-card shadow="hover">
          <template #header>
            <div class="flex items-center justify-between">
              <span style="font-weight: 600">系统状态</span>
              <el-button size="small" text @click="refreshStats">刷新</el-button>
            </div>
          </template>
          <el-descriptions :column="2" border>
            <el-descriptions-item label="API 版本">1.0.0</el-descriptions-item>
            <el-descriptions-item label="数据库">InMemory (Dev)</el-descriptions-item>
            <el-descriptions-item label="运行状态">
              <el-tag type="success" size="small">健康</el-tag>
            </el-descriptions-item>
            <el-descriptions-item label="启动时间">{{ startTime }}</el-descriptions-item>
          </el-descriptions>
        </el-card>
      </el-col>

      <!-- 最近会话 -->
      <el-col :xs="24" :md="12" class="mb-md">
        <el-card shadow="hover">
          <template #header>
            <div class="flex items-center justify-between">
              <span style="font-weight: 600">最近会话</span>
              <el-button size="small" text @click="$router.push('/sessions')">查看更多 →</el-button>
            </div>
          </template>
          <div v-if="recentSessions.length === 0" class="text-center text-secondary" style="padding: 24px 0">
            <el-empty description="暂无会话" :image-size="48" />
          </div>
          <div v-else v-for="s in recentSessions" :key="s.id" class="recent-session-item">
            <div class="flex items-center justify-between" style="cursor: pointer" @click="$router.push('/chat')">
              <div class="flex items-center gap-sm" style="flex: 1; min-width: 0">
                <el-icon :size="16" style="color: #909399"><ChatDotSquare /></el-icon>
                <span class="text-ellipsis">{{ s.title || '新会话' }}</span>
              </div>
              <el-tag
                :type="s.status === 'Active' ? 'success' : 'info'"
                size="small"
              >
                {{ s.status === 'Active' ? '活跃' : '完成' }}
              </el-tag>
            </div>
          </div>
        </el-card>
      </el-col>
    </el-row>

    <!-- Agent 活跃度 -->
    <el-col :span="24">
      <el-card shadow="hover">
        <template #header>
          <span style="font-weight: 600">Agent 活跃度</span>
        </template>
        <div v-if="agents.length === 0" class="text-center text-secondary" style="padding: 40px 0">
          暂无 Agent 数据
        </div>
        <div v-else style="display: grid; grid-template-columns: repeat(auto-fill, minmax(280px, 1fr)); gap: 16px">
          <div v-for="agent in agents" :key="agent.id" class="agent-activity-card">
            <div class="flex items-center justify-between mb-sm">
              <div class="flex items-center gap-sm">
                <el-icon :size="18" style="color: #409eff"><Robot /></el-icon>
                <span style="font-weight: 500">{{ agent.name }}</span>
              </div>
              <el-tag
                :type="agent.status === 'Active' ? 'success' : 'info'"
                size="small"
              >
                {{ agent.status }}
              </el-tag>
            </div>
            <div class="text-caption">{{ agent.description || '暂无描述' }}</div>
          </div>
        </div>
      </el-card>
    </el-col>
  </div>
</template>

<script setup lang="ts">
import { reactive, ref, onMounted, type Component } from 'vue'
import { useRouter } from 'vue-router'
import { useAgentStore } from '../stores/agent'
import { useModelStore } from '../stores/model'
import { useSkillStore } from '../stores/skill'
import http from '../api/http'
import { Aim } from '@element-plus/icons-vue'
import {
  DataAnalysis, Connection, MagicStick, ChatDotSquare
} from '@element-plus/icons-vue'

const router = useRouter()
const agentStore = useAgentStore()
const modelStore = useModelStore()
const skillStore = useSkillStore()

const startTime = ref(new Date().toLocaleString())
const recentSessions = ref<any[]>([])

interface StatCard {
  label: string
  value: number | string
  color: string
  icon: Component
}

const statCards = reactive<StatCard[]>([
  { label: 'Agent 总数', value: 0, color: '#409eff', icon: Aim },
  { label: '活跃 Agent', value: 0, color: '#67c23a', icon: DataAnalysis },
  { label: '模型数量', value: 0, color: '#e6a23c', icon: Connection },
  { label: '技能数量', value: 0, color: '#764ba2', icon: MagicStick },
])

const agents = ref<any[]>([])

async function loadStats() {
  await Promise.all([agentStore.fetchAll(), modelStore.fetchAll(), skillStore.fetchAll()])
  statCards[0].value = agentStore.agents.length
  statCards[1].value = agentStore.activeAgents.length
  statCards[2].value = modelStore.providers.length
  statCards[3].value = skillStore.skills.length
  agents.value = agentStore.agents.slice(0, 6)
}

async function loadRecentSessions() {
  try {
    const res = await http.get('/sessions?userId=anonymous')
    const data = res.data.data || res.data || []
    recentSessions.value = (Array.isArray(data) ? data : []).slice(0, 5)
  } catch { /* ignore */ }
}

function refreshStats() {
  loadStats()
  loadRecentSessions()
}

onMounted(() => {
  loadStats()
  loadRecentSessions()
})
</script>

<style scoped>
.recent-session-item {
  padding: 8px 0;
  border-bottom: 1px solid var(--border-light);
  transition: border-color var(--transition-normal);
}
.recent-session-item:last-child {
  border-bottom: none;
}
.recent-session-item:hover {
  background: var(--sidebar-hover-bg);
  margin: 0 -8px;
  padding: 8px;
  border-radius: 6px;
}
.text-ellipsis {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 14px;
}
.agent-activity-card {
  padding: 16px;
  border: 1px solid var(--border-light);
  border-radius: 8px;
  transition: box-shadow 0.2s, border-color var(--transition-normal);
}
.agent-activity-card:hover {
  box-shadow: var(--shadow-md);
}
</style>

