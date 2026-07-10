<template>
  <div>
    <!-- 顶部筛选栏 -->
    <el-card shadow="never" class="filter-card">
      <el-row :gutter="16" align="middle">
        <el-col :xs="12" :sm="8" :md="6">
          <el-date-picker
            v-model="dateRange"
            type="daterange"
            range-separator="至"
            start-placeholder="开始日期"
            end-placeholder="结束日期"
            value-format="YYYY-MM-DD"
            @change="loadData"
            style="width: 100%"
          />
        </el-col>
        <el-col :xs="12" :sm="5" :md="4">
          <el-select v-model="agentFilter" placeholder="全部 Agent" clearable style="width: 100%" @change="loadData">
            <el-option
              v-for="a in agentOptions"
              :key="a.id"
              :label="a.name"
              :value="a.id"
            />
          </el-select>
        </el-col>
        <el-col :xs="12" :sm="5" :md="4">
          <div style="display: flex; gap: 8px">
            <el-button type="primary" @click="loadData" :loading="loading">刷新</el-button>
            <el-button @click="handleExportCSV">导出 CSV</el-button>
          </div>
        </el-col>
      </el-row>
    </el-card>

    <!-- 汇总卡片 -->
    <el-row :gutter="16" class="summary-row">
      <el-col :span="6">
        <el-card shadow="never">
          <div class="stat-card">
            <div class="stat-label">总请求数</div>
            <div class="stat-value">{{ summary.totalRequests }}</div>
          </div>
        </el-card>
      </el-col>
      <el-col :span="6">
        <el-card shadow="never">
          <div class="stat-card">
            <div class="stat-label">总输入 Token</div>
            <div class="stat-value">{{ summary.totalInputTokens.toLocaleString() }}</div>
          </div>
        </el-card>
      </el-col>
      <el-col :span="6">
        <el-card shadow="never">
          <div class="stat-card">
            <div class="stat-label">总输出 Token</div>
            <div class="stat-value">{{ summary.totalOutputTokens.toLocaleString() }}</div>
          </div>
        </el-card>
      </el-col>
      <el-col :span="6">
        <el-card shadow="never">
          <div class="stat-card">
            <div class="stat-label">总费用</div>
            <div class="stat-value">${{ summary.totalCost.toFixed(4) }}</div>
          </div>
        </el-card>
      </el-col>
    </el-row>

    <!-- 图表行 -->
    <el-row :gutter="16" class="chart-row">
      <el-col :span="16">
        <el-card shadow="never">
          <template #header>每日 Token 用量趋势</template>
          <v-chart :option="dailyChartOption" autoresize style="height: 320px" />
        </el-card>
      </el-col>
      <el-col :span="8">
        <el-card shadow="never">
          <template #header>Agent 费用占比</template>
          <v-chart :option="pieChartOption" autoresize style="height: 320px" />
        </el-card>
      </el-col>
    </el-row>

    <!-- 用量明细表格 -->
    <el-card shadow="never">
      <template #header>
        <div class="flex items-center justify-between">
          <span>用量明细</span>
          <el-tag type="info" size="small">{{ records.length }} 条记录</el-tag>
        </div>
      </template>
      <el-table :data="paginatedRecords" v-loading="loading" stripe style="width: 100%">
        <el-table-column prop="modelId" label="模型" width="140" />
        <el-table-column label="输入 Token" width="120">
          <template #default="{ row }">{{ row.inputTokens.toLocaleString() }}</template>
        </el-table-column>
        <el-table-column label="输出 Token" width="120">
          <template #default="{ row }">{{ row.outputTokens.toLocaleString() }}</template>
        </el-table-column>
        <el-table-column label="费用" width="100">
          <template #default="{ row }">${{ row.cost.toFixed(4) }}</template>
        </el-table-column>
        <el-table-column label="时间" width="180">
          <template #default="{ row }">{{ new Date(row.createdAt).toLocaleString() }}</template>
        </el-table-column>
      </el-table>
      <div style="display: flex; justify-content: flex-end; margin-top: 16px">
        <el-pagination
          v-model:current-page="page"
          v-model:page-size="pageSize"
          :total="records.length"
          :page-sizes="[10, 20, 50, 100]"
          layout="total, sizes, prev, pager, next"
          small
        />
      </div>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import http from '../api/http'
import VChart from 'vue-echarts'
import 'echarts'
import { ElMessage } from 'element-plus'

interface UsageDailyDto {
  date: string
  requestCount: number
  inputTokens: number
  outputTokens: number
  cost: number
}

interface UsageSummaryDto {
  totalRequests: number
  totalInputTokens: number
  totalOutputTokens: number
  totalCost: number
  agentCount: number
}

interface UsageRecordDto {
  id: string
  modelId: string
  inputTokens: number
  outputTokens: number
  cost: number
  createdAt: string
}

interface ApiResponse<T> {
  success: boolean
  message: string
  data: T
}

const loading = ref(false)
const dateRange = ref<[string, string]>([getDefaultFrom(), getDefaultTo()])
const summary = ref<UsageSummaryDto>({
  totalRequests: 0, totalInputTokens: 0, totalOutputTokens: 0, totalCost: 0, agentCount: 0
})
const dailyData = ref<UsageDailyDto[]>([])
const records = ref<UsageRecordDto[]>([])

// Agent 筛选
const agentFilter = ref('')
const agentOptions = ref<{ id: string; name: string }[]>([])

// 分页
const page = ref(1)
const pageSize = ref(20)

const paginatedRecords = computed(() => {
  const start = (page.value - 1) * pageSize.value
  return records.value.slice(start, start + pageSize.value)
})

function getDefaultFrom(): string {
  const d = new Date()
  d.setDate(d.getDate() - 30)
  return d.toISOString().slice(0, 10)
}
function getDefaultTo(): string {
  return new Date().toISOString().slice(0, 10)
}

async function loadData() {
  loading.value = true
  try {
    const [from, to] = dateRange.value
    const params: any = { from, to }
    if (agentFilter.value) params.agentId = agentFilter.value

    const [summaryRes, dailyRes, recordsRes, agentRes] = await Promise.all([
      http.get<ApiResponse<UsageSummaryDto>>('/usage/summary', { params }),
      http.get<ApiResponse<UsageDailyDto[]>>('/usage/daily', { params }),
      http.get<ApiResponse<UsageRecordDto[]>>('/usage', { params }),
      http.get<{ data: { id: string; name: string }[] }>('/agents').catch(() => null),
    ])
    if (summaryRes.data.success) summary.value = summaryRes.data.data!
    if (dailyRes.data.success) dailyData.value = dailyRes.data.data!
    if (recordsRes.data.success) records.value = recordsRes.data.data!
    if (agentRes?.data?.data) agentOptions.value = agentRes.data.data.map((a: any) => ({ id: a.id, name: a.name }))
  } finally {
    loading.value = false
  }
}

// ─── ECharts 配置 ──────────────────────────────────────────────

const dailyChartOption = computed(() => ({
  tooltip: { trigger: 'axis' as const },
  legend: { data: ['输入 Token', '输出 Token'] },
  grid: { left: '3%', right: '4%', bottom: '3%', containLabel: true },
  xAxis: {
    type: 'category' as const,
    data: dailyData.value.map(d => d.date.slice(5)),
    axisLabel: { rotate: 45 }
  },
  yAxis: { type: 'value' as const },
  series: [
    {
      name: '输入 Token',
      type: 'line',
      smooth: true,
      data: dailyData.value.map(d => d.inputTokens),
      areaStyle: { opacity: 0.3 }
    },
    {
      name: '输出 Token',
      type: 'line',
      smooth: true,
      data: dailyData.value.map(d => d.outputTokens),
      areaStyle: { opacity: 0.3 }
    }
  ]
}))

const pieChartOption = computed(() => {
  // 暂时显示请求类型占比作为示例
  // 实际应加载 Agent 名称
  const total = summary.value.totalOutputTokens + summary.value.totalInputTokens
  const inputPct = total > 0 ? ((summary.value.totalInputTokens / total) * 100).toFixed(1) : '0'
  const outputPct = total > 0 ? ((summary.value.totalOutputTokens / total) * 100).toFixed(1) : '0'
  return {
    tooltip: { trigger: 'item' as const, formatter: '{b}: {c} ({d}%)' },
    series: [{
      type: 'pie',
      radius: ['40%', '70%'],
      center: ['50%', '50%'],
      data: [
        { name: `输入 Token (${inputPct}%)`, value: summary.value.totalInputTokens },
        { name: `输出 Token (${outputPct}%)`, value: summary.value.totalOutputTokens },
      ],
      emphasis: {
        itemStyle: { shadowBlur: 10, shadowOffsetX: 0, shadowColor: 'rgba(0, 0, 0, 0.5)' }
      }
    }]
  }
})

function handleExportCSV() {
  if (records.value.length === 0) {
    ElMessage.warning('暂无数据可导出')
    return
  }
  const headers = ['模型', '输入 Token', '输出 Token', '费用', '时间']
  const rows = records.value.map(r => [
    r.modelId,
    r.inputTokens.toString(),
    r.outputTokens.toString(),
    r.cost.toFixed(4),
    new Date(r.createdAt).toISOString()
  ])
  const csv = [headers.join(','), ...rows.map(r => r.join(','))].join('\n')
  const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `usage-export-${new Date().toISOString().slice(0, 10)}.csv`
  a.click()
  URL.revokeObjectURL(url)
  ElMessage.success('CSV 已导出')
}

onMounted(loadData)
</script>

<style scoped>
.filter-card {
  margin-bottom: 16px;
}
.summary-row {
  margin-bottom: 16px;
}
.chart-row {
  margin-bottom: 16px;
}
.stat-card {
  text-align: center;
  padding: 8px 0;
}
.stat-label {
  font-size: 14px;
  color: #909399;
  margin-bottom: 8px;
}
.stat-value {
  font-size: 28px;
  font-weight: bold;
  color: #303133;
}
</style>
