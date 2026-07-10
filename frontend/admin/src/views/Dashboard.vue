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

    <el-row :gutter="16" class="mb-lg equal-height-row">
      <!-- 系统状态 -->
      <el-col :xs="24" :md="12">
        <el-card shadow="hover" class="equal-height-card">
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

      <!-- 用量概览 -->
      <el-col :xs="24" :md="12">
        <el-card shadow="hover" class="equal-height-card">
          <template #header>
            <div class="flex items-center justify-between">
              <span style="font-weight: 600">用量概览</span>
              <el-button size="small" text @click="$router.push('/usage')">查看更多 →</el-button>
            </div>
          </template>
          <el-table :data="usageRecords" size="small" v-loading="usageLoading">
            <el-table-column prop="modelId" label="模型" min-width="100" show-overflow-tooltip />
            <el-table-column label="输入 Token" width="90" align="right">
              <template #default="{ row }">{{ row.inputTokens.toLocaleString() }}</template>
            </el-table-column>
            <el-table-column label="输出 Token" width="90" align="right">
              <template #default="{ row }">{{ row.outputTokens.toLocaleString() }}</template>
            </el-table-column>
            <el-table-column label="费用" width="80" align="right">
              <template #default="{ row }">${{ row.cost.toFixed(4) }}</template>
            </el-table-column>
          </el-table>
          <el-empty v-if="!usageLoading && usageRecords.length === 0" description="暂无用量数据" :image-size="40" />
        </el-card>
      </el-col>
    </el-row>

    <!-- 最近会话（独占一行） -->
    <el-row :gutter="16" class="mb-lg">
      <el-col :span="24">
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
          <div v-else style="display: grid; grid-template-columns: repeat(auto-fill, minmax(320px, 1fr)); gap: 12px">
            <div v-for="s in recentSessions" :key="s.id" class="recent-session-item" style="cursor: pointer" @click="$router.push('/chat')">
              <div class="flex items-center justify-between">
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
const usageRecords = ref<any[]>([])
const usageLoading = ref(false)

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

async function loadUsage() {
  usageLoading.value = true
  try {
    const from = new Date()
    from.setDate(from.getDate() - 7)
    const params = { from: from.toISOString().slice(0, 10), to: new Date().toISOString().slice(0, 10) }
    const res = await http.get('/usage', { params })
    const data = res.data.data || res.data || []
    usageRecords.value = (Array.isArray(data) ? data : []).slice(0, 1)
  } catch { /* ignore */ }
  finally { usageLoading.value = false }
}

function refreshStats() {
  loadStats()
  loadRecentSessions()
  loadUsage()
}

onMounted(() => {
  loadStats()
  loadRecentSessions()
  loadUsage()
})
</script>

<style scoped>
.equal-height-row {
  display: flex;
  flex-wrap: wrap;
}
.equal-height-row > .el-col {
  display: flex;
}
.equal-height-card {
  flex: 1;
  display: flex;
  flex-direction: column;
}
.equal-height-card :deep(.el-card__body) {
  flex: 1;
  display: flex;
  flex-direction: column;
}
.recent-session-item {
  padding: 10px 12px;
  border: 1px solid var(--border-light);
  border-radius: 8px;
  transition: box-shadow 0.2s, border-color var(--transition-normal);
}
.recent-session-item:hover {
  box-shadow: var(--shadow-md);
  border-color: var(--color-primary-300);
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

