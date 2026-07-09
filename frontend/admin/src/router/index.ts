import { createRouter, createWebHistory } from 'vue-router'
import Layout from '../views/Layout.vue'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', redirect: '/dashboard' },
    {
      path: '/login',
      component: () => import('../views/Login.vue'),
      meta: { title: '登录', noAuth: true }
    },
    {
      path: '/',
      component: Layout,
      meta: { requiresAuth: true },
      children: [
        { path: 'dashboard', component: () => import('../views/Dashboard.vue'), meta: { title: '概览' } },
        { path: 'agents', component: () => import('../views/Agents.vue'), meta: { title: 'Agent 管理' } },
        { path: 'models', component: () => import('../views/Models.vue'), meta: { title: '模型管理' } },
        { path: 'skills', component: () => import('../views/Skills.vue'), meta: { title: '技能管理' } },
        { path: 'chat', component: () => import('../views/Chat.vue'), meta: { title: '对话测试' } },
        { path: 'sessions', component: () => import('../views/Sessions.vue'), meta: { title: '会话记录' } },
        { path: 'usage', component: () => import('../views/Usage.vue'), meta: { title: '用量统计' } },
        { path: 'mcps', component: () => import('../views/McpEndpoints.vue'), meta: { title: 'MCP 管理' } },
        { path: 'mcps/:id', component: () => import('../views/McpEndpointDetail.vue'), meta: { title: 'MCP 详情' } },
      ]
    }
  ]
})

// 路由守卫：未登录时跳转登录页
router.beforeEach((to, _from, next) => {
  const token = localStorage.getItem('auth_token')
  if (to.meta.requiresAuth && !token) {
    next('/login')
  } else if (to.path === '/login' && token) {
    next('/dashboard')
  } else {
    next()
  }
})

export default router
