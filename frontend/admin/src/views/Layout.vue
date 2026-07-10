<template>
  <el-container class="layout-container">
    <!-- 移动端 Drawer -->
    <el-drawer
      v-model="drawerVisible"
      direction="ltr"
      size="220px"
      :with-header="false"
      :z-index="2000"
    >
      <sidebar-content :collapsed="false" @navigate="drawerVisible = false" />
    </el-drawer>

    <!-- 桌面侧栏 -->
    <el-aside
      :width="collapsed ? '64px' : '220px'"
      class="layout-sidebar hidden-sm"
    >
      <sidebar-content :collapsed="collapsed" />
      <!-- 折叠按钮 -->
      <div class="collapse-btn" @click="collapsed = !collapsed">
        <el-icon :size="18">
          <Fold v-if="!collapsed" />
          <Expand v-else />
        </el-icon>
      </div>
    </el-aside>

    <el-container>
      <!-- Header -->
      <el-header class="layout-header">
        <div class="header-left">
          <!-- 移动端汉堡菜单 -->
          <el-button
            class="hidden-md-plus"
            size="small"
            :icon="Operation"
            text
            @click="drawerVisible = true"
          />
          <h3 class="header-title">{{ route.meta.title }}</h3>
        </div>

        <div class="header-right">
          <!-- SignalR 连接状态 -->
          <el-tooltip :content="signalRStatus.text" placement="bottom">
            <span class="signalr-status" @click="signalRStatus.toggle">
              <span class="signalr-dot" :style="{ background: signalRStatus.color }" />
              <span class="signalr-label">{{ signalRStatus.label }}</span>
            </span>
          </el-tooltip>

          <!-- 深色模式切换 -->
          <el-tooltip content="切换主题" placement="bottom">
            <el-button
              size="small"
              :icon="isDark ? Sunny : Moon"
              text
              @click="toggleDark"
            />
          </el-tooltip>

          <span class="user-info">
            <el-icon><User /></el-icon>
            {{ authStore.displayName }}
          </span>
          <el-button size="small" type="danger" plain @click="handleLogout">
            退出
          </el-button>
        </div>
      </el-header>

      <!-- Main -->
      <el-main class="layout-main">
        <router-view v-slot="{ Component, route }">
          <transition name="fade" mode="out-in">
            <component :is="Component" :key="route.path" />
          </transition>
        </router-view>
      </el-main>
    </el-container>
  </el-container>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  User, Fold, Expand, Operation,
  Sunny, Moon
} from '@element-plus/icons-vue'
import SidebarContent from '../components/SidebarContent.vue'
import { useAuthStore } from '../stores/auth'
import { ElMessageBox, ElMessage } from 'element-plus'

const route = useRoute()
const router = useRouter()
const authStore = useAuthStore()

const STORAGE_KEY = 'theme-mode'

// ─── 侧栏折叠 ──────────────────────────────────────
const collapsed = ref(false)
const drawerVisible = ref(false)

// ─── 深色模式 ──────────────────────────────────────
const isDark = ref(false)

function applyTheme(dark: boolean) {
  isDark.value = dark
  document.documentElement.classList.toggle('dark', dark)
  localStorage.setItem(STORAGE_KEY, dark ? 'dark' : 'light')
}

function toggleDark() {
  applyTheme(!isDark.value)
  ElMessage.info(isDark.value ? '已切换为深色模式' : '已切换为浅色模式')
}

// ─── SignalR 模拟状态 ──────────────────────────────
const signalRStatus = computed(() => {
  return {
    color: '#67c23a',
    label: '已连接',
    text: '实时通信正常',
    toggle: () => ElMessage.info('SignalR 连接状态正常')
  }
})

// ─── 响应式检测 ─────────────────────────────────────
const mediaQuery = window.matchMedia('(max-width: 768px)')
function handleScreenChange(e: MediaQueryListEvent | MediaQueryList) {
  if (e.matches) {
    collapsed.value = false
  }
}
onMounted(() => {
  // 恢复主题
  const saved = localStorage.getItem(STORAGE_KEY)
  if (saved === 'dark') {
    applyTheme(true)
  }

  mediaQuery.addEventListener('change', handleScreenChange)
  handleScreenChange(mediaQuery)
})
onUnmounted(() => {
  mediaQuery.removeEventListener('change', handleScreenChange)
})

// ─── 退出 ─────────────────────────────────────────
async function handleLogout() {
  try {
    await ElMessageBox.confirm('确认退出登录？', '提示', { type: 'warning' })
    authStore.logout()
    router.push('/login')
  } catch { /* cancelled */ }
}
</script>

<style scoped>
.layout-container {
  height: 100vh;
}

.layout-sidebar {
  position: relative;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  transition: width 0.25s ease;
}

.layout-header {
  background: var(--bg-header);
  border-bottom: 1px solid var(--border-color);
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 20px;
  height: var(--layout-header-height);
  transition: background var(--transition-normal),
              border-color var(--transition-normal);
}

.header-left {
  display: flex;
  align-items: center;
  gap: 12px;
}

.header-title {
  margin: 0;
  font-size: 18px;
  font-weight: 600;
  color: var(--text-primary);
  transition: color var(--transition-normal);
}

.header-right {
  display: flex;
  align-items: center;
  gap: 12px;
}

.signalr-status {
  display: flex;
  align-items: center;
  gap: 4px;
  font-size: 12px;
  cursor: pointer;
}

.signalr-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  display: inline-block;
  box-shadow: 0 0 6px currentColor;
}

.signalr-label {
  color: var(--text-muted);
  transition: color var(--transition-normal);
}

.user-info {
  font-size: 14px;
  color: var(--text-secondary);
  display: flex;
  align-items: center;
  gap: 4px;
  transition: color var(--transition-normal);
}

.collapse-btn {
  position: absolute;
  bottom: 0;
  width: 100%;
  border-top: 1px solid var(--sidebar-border);
  padding: 10px 0;
  text-align: center;
  cursor: pointer;
  color: var(--sidebar-collapse-btn-color);
  font-size: 18px;
  transition: background 0.2s, color var(--transition-normal);
}

.collapse-btn:hover {
  background: var(--sidebar-collapse-btn-hover-bg);
}

.layout-main {
  background: var(--bg-page);
  padding: var(--layout-content-padding);
  overflow-y: auto;
  transition: background var(--transition-normal);
}

/* 缩小 el-card 内边距，整体更紧凑 */
.layout-main :deep(.el-card__body) {
  padding: 16px;
}

:deep(.el-drawer__body) {
  padding: 0;
}
</style>
