<template>
  <div>
    <el-row :gutter="20" style="margin-bottom: 20px">
      <el-col :span="6">
        <el-card shadow="hover"><div style="text-align:center"><h2>{{ stats.totalAgents }}</h2><p>Agent 总数</p></div></el-card>
      </el-col>
      <el-col :span="6">
        <el-card shadow="hover"><div style="text-align:center"><h2>{{ stats.activeAgents }}</h2><p>活跃 Agent</p></div></el-card>
      </el-col>
      <el-col :span="6">
        <el-card shadow="hover"><div style="text-align:center"><h2>{{ stats.totalModels }}</h2><p>模型数量</p></div></el-card>
      </el-col>
      <el-col :span="6">
        <el-card shadow="hover"><div style="text-align:center"><h2>{{ stats.totalSkills }}</h2><p>技能数量</p></div></el-card>
      </el-col>
    </el-row>
    <el-card>
      <template #header>系统状态</template>
      <el-descriptions :column="2" border>
        <el-descriptions-item label="API 版本">1.0.0</el-descriptions-item>
        <el-descriptions-item label="数据库">InMemory (Development)</el-descriptions-item>
        <el-descriptions-item label="运行状态"><el-tag type="success">健康</el-tag></el-descriptions-item>
        <el-descriptions-item label="启动时间">{{ startTime }}</el-descriptions-item>
      </el-descriptions>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { reactive, ref, onMounted } from 'vue'
import { useAgentStore } from '../stores/agent'
import { useModelStore } from '../stores/model'
import { useSkillStore } from '../stores/skill'

const agentStore = useAgentStore()
const modelStore = useModelStore()
const skillStore = useSkillStore()
const startTime = ref(new Date().toLocaleString())

const stats = reactive({ totalAgents: 0, activeAgents: 0, totalModels: 0, totalSkills: 0 })

onMounted(async () => {
  await Promise.all([agentStore.fetchAll(), modelStore.fetchAll(), skillStore.fetchAll()])
  stats.totalAgents = agentStore.agents.length
  stats.activeAgents = agentStore.activeAgents.length
  stats.totalModels = modelStore.providers.length
  stats.totalSkills = skillStore.skills.length
})
</script>
