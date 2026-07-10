<template>
  <div class="sidebar-wrapper">
    <!-- Logo -->
    <div class="sidebar-logo" :class="{ collapsed }">
      <template v-if="collapsed">🤖</template>
      <template v-else>🤖 Agent Platform</template>
    </div>

    <!-- 导航菜单 -->
    <el-menu
      :default-active="route.path"
      :collapse="collapsed"
      router
      @select="onNavigate"
    >
      <el-menu-item index="/dashboard">
        <el-icon><DataAnalysis /></el-icon>
        <template #title>概览</template>
      </el-menu-item>
      <el-menu-item index="/agents">
        <el-icon><Aim /></el-icon>
        <template #title>Agent 管理</template>
      </el-menu-item>
      <el-menu-item index="/models">
        <el-icon><Connection /></el-icon>
        <template #title>模型管理</template>
      </el-menu-item>
      <el-menu-item index="/skills">
        <el-icon><MagicStick /></el-icon>
        <template #title>技能管理</template>
      </el-menu-item>
      <el-menu-item index="/chat">
        <el-icon><ChatDotRound /></el-icon>
        <template #title>对话测试</template>
      </el-menu-item>
      <el-menu-item index="/sessions">
        <el-icon><ChatDotSquare /></el-icon>
        <template #title>会话记录</template>
      </el-menu-item>
      <el-menu-item index="/usage">
        <el-icon><TrendCharts /></el-icon>
        <template #title>用量统计</template>
      </el-menu-item>
      <el-menu-item index="/mcps">
        <el-icon><Link /></el-icon>
        <template #title>MCP 管理</template>
      </el-menu-item>
    </el-menu>
  </div>
</template>

<script setup lang="ts">
import { useRoute } from 'vue-router'
import {
  DataAnalysis, Aim, Connection, MagicStick,
  ChatDotRound, ChatDotSquare, TrendCharts, Link
} from '@element-plus/icons-vue'

defineProps<{ collapsed: boolean }>()
const emit = defineEmits<{ navigate: [] }>()

const route = useRoute()

function onNavigate() {
  emit('navigate')
}
</script>

<style scoped>
.sidebar-wrapper {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: var(--sidebar-bg);
  border-right: 1px solid var(--sidebar-border);
  transition: background var(--transition-normal),
              border-color var(--transition-normal);
}

.sidebar-logo {
  padding: 18px 16px;
  color: var(--sidebar-logo-color);
  font-size: 18px;
  font-weight: 700;
  text-align: center;
  white-space: nowrap;
  overflow: hidden;
  transition: all 0.25s ease, color var(--transition-normal);
  letter-spacing: 0.5px;
  border-bottom: 1px solid var(--sidebar-border);
}

.sidebar-logo.collapsed {
  padding: 18px 0;
  font-size: 22px;
}

/* el-menu overrides */
:deep(.el-menu) {
  border-right: none !important;
  background: transparent !important;
}

:deep(.el-menu-item) {
  height: 44px;
  line-height: 44px;
  color: var(--sidebar-text) !important;
  background: transparent !important;
  margin: 2px 8px;
  border-radius: 8px;
  width: auto !important;
  transition: all 0.2s ease !important;
}

:deep(.el-menu-item:hover) {
  background: var(--sidebar-hover-bg) !important;
  color: var(--sidebar-logo-color) !important;
}

:deep(.el-menu-item.is-active) {
  color: var(--sidebar-active-text) !important;
  background: var(--sidebar-active-bg) !important;
  font-weight: 600;
}

:deep(.el-menu-item .el-icon) {
  color: inherit !important;
  font-size: 18px;
}

/* 折叠状态下菜单项居中 */
:deep(.el-menu--collapse .el-menu-item) {
  padding: 0 !important;
  justify-content: center;
  margin: 2px 6px;
}
</style>
