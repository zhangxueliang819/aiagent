<template>
  <el-container style="height: 100vh">
    <el-aside width="220px" style="background: #304156">
      <div style="padding: 16px; color: #fff; font-size: 18px; font-weight: bold; text-align: center">
        🤖 Agent Platform
      </div>
      <el-menu
        :default-active="route.path"
        background-color="#304156"
        text-color="#bfcbd9"
        active-text-color="#409eff"
        router
      >
        <el-menu-item index="/dashboard">
          <el-icon><DataAnalysis /></el-icon><span>概览</span>
        </el-menu-item>
        <el-menu-item index="/agents">
          <el-icon><Robot /></el-icon><span>Agent 管理</span>
        </el-menu-item>
        <el-menu-item index="/models">
          <el-icon><Connection /></el-icon><span>模型管理</span>
        </el-menu-item>
        <el-menu-item index="/skills">
          <el-icon><MagicStick /></el-icon><span>技能管理</span>
        </el-menu-item>
        <el-menu-item index="/chat">
          <el-icon><ChatDotRound /></el-icon><span>对话测试</span>
        </el-menu-item>
        <el-menu-item index="/sessions">
          <el-icon><ChatDotSquare /></el-icon><span>会话记录</span>
        </el-menu-item>
        <el-menu-item index="/usage">
          <el-icon><TrendCharts /></el-icon><span>用量统计</span>
        </el-menu-item>
        <el-menu-item index="/mcps">
          <el-icon><Connection /></el-icon><span>MCP 管理</span>
        </el-menu-item>
      </el-menu>
    </el-aside>
    <el-container>
      <el-header style="background: #fff; border-bottom: 1px solid #e6e6e6; display: flex; align-items: center; justify-content: space-between; padding: 0 20px">
        <h3 style="margin: 0">{{ route.meta.title }}</h3>
        <div style="display: flex; align-items: center; gap: 12px">
          <span style="font-size: 14px; color: #666">
            <el-icon><User /></el-icon>
            {{ authStore.displayName }}
          </span>
          <el-button size="small" type="danger" plain @click="handleLogout">退出登录</el-button>
        </div>
      </el-header>
      <el-main style="background: #f0f2f5">
        <router-view />
      </el-main>
    </el-container>
  </el-container>
</template>

<script setup lang="ts">
import { useRoute, useRouter } from 'vue-router'
import { User } from '@element-plus/icons-vue'
import { useAuthStore } from '../stores/auth'
import { ElMessageBox } from 'element-plus'

const route = useRoute()
const router = useRouter()
const authStore = useAuthStore()

async function handleLogout() {
  try {
    await ElMessageBox.confirm('确认退出登录？', '提示', { type: 'warning' })
    authStore.logout()
    router.push('/login')
  } catch { /* cancelled */ }
}
</script>
