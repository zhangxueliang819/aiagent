<template>
  <div class="mcp-detail-page">
    <el-card shadow="never" class="header-card">
      <el-row :gutter="16" align="middle">
        <el-col :span="1">
          <el-button link @click="$router.push('/mcps')">
            <el-icon><ArrowLeft /></el-icon>
          </el-button>
        </el-col>
        <el-col :span="8">
          <h3 style="margin:0">{{ endpoint?.name || '加载中…' }}</h3>
        </el-col>
        <el-col :span="6">
          <el-tag :type="endpoint?.isEnabled ? 'success' : 'danger'" size="small">
            {{ endpoint?.isEnabled ? '已启用' : '已禁用' }}
          </el-tag>
          <el-tag type="info" size="small" style="margin-left: 8px">{{ endpoint?.protocol }}</el-tag>
        </el-col>
        <el-col :span="9" style="text-align: right">
          <el-button size="small" @click="handleDiscover" :loading="discovering">重新发现工具</el-button>
          <el-button size="small" type="primary" @click="showEdit = true" v-if="endpoint">编辑</el-button>
        </el-col>
      </el-row>
      <el-row style="margin-top: 12px">
        <el-col>
          <span class="detail-label">端点 URL：</span>
          <code>{{ endpoint?.endpointUrl }}</code>
        </el-col>
      </el-row>
    </el-card>

    <el-row :gutter="16" style="margin-top: 16px">
      <!-- 工具树 -->
      <el-col :span="8">
        <el-card shadow="never">
          <template #header>
            工具列表 ({{ tools.length }})
          </template>
          <div v-if="tools.length === 0" style="text-align: center; padding: 40px 0">
            <el-empty description="暂无工具" :image-size="60" />
          </div>
          <el-tree
            v-else
            :data="treeData"
            :props="treeProps"
            node-key="id"
            default-expand-all
            highlight-current
            @node-click="handleNodeClick"
          />
        </el-card>
      </el-col>

      <!-- 工具详情 -->
      <el-col :span="16">
        <el-card shadow="never" v-if="selectedTool">
          <template #header>
            <div style="display: flex; justify-content: space-between; align-items: center">
              <span>{{ selectedTool.toolName }}</span>
              <el-tag :type="selectedTool.isEnabled ? 'success' : 'info'" size="small">
                {{ selectedTool.isEnabled ? '启用' : '禁用' }}
              </el-tag>
            </div>
          </template>
          <el-descriptions :column="1" border>
            <el-descriptions-item label="名称">{{ selectedTool.toolName }}</el-descriptions-item>
            <el-descriptions-item label="描述">{{ selectedTool.description || '无描述' }}</el-descriptions-item>
          </el-descriptions>
          <h4 style="margin: 16px 0 8px">输入 Schema</h4>
          <el-input
            type="textarea"
            :model-value="formatJson(selectedTool.inputSchema)"
            :rows="12"
            readonly
            style="font-family: monospace; font-size: 13px"
          />
        </el-card>
        <el-card shadow="never" v-else>
          <el-empty description="从左侧选择一个工具查看详情" :image-size="80" />
        </el-card>
      </el-col>
    </el-row>

    <!-- 编辑对话框 -->
    <el-dialog v-model="showEdit" title="编辑 MCP 端点" width="500px" @closed="showEdit = false">
      <el-form :model="editForm" label-width="100px">
        <el-form-item label="名称"><el-input v-model="editForm.name" /></el-form-item>
        <el-form-item label="端点 URL"><el-input v-model="editForm.endpointUrl" /></el-form-item>
        <el-form-item label="协议">
          <el-select v-model="editForm.protocol" style="width:100%">
            <el-option label="SSE" value="sse" />
            <el-option label="STDIO" value="stdio" />
          </el-select>
        </el-form-item>
        <el-form-item label="状态">
          <el-switch v-model="editForm.isEnabled" active-text="启用" inactive-text="禁用" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showEdit = false">取消</el-button>
        <el-button type="primary" @click="handleSave" :loading="saving">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import http from '../api/http'
import { ElMessage, ElMessageBox } from 'element-plus'
import { ArrowLeft } from '@element-plus/icons-vue'

interface McpTool {
  id: string
  toolName: string
  description: string
  inputSchema: string
  isEnabled: boolean
}

interface McpEndpoint {
  id: string
  name: string
  endpointUrl: string
  protocol: string
  isEnabled: boolean
  createdAt: string
  tools: McpTool[]
}

interface TreeNode {
  id: string
  label: string
  isTool?: boolean
  children?: TreeNode[]
}

const route = useRoute()
const router = useRouter()
const endpointId = route.params.id as string

const endpoint = ref<McpEndpoint | null>(null)
const tools = ref<McpTool[]>([])
const selectedTool = ref<McpTool | null>(null)
const discovering = ref(false)
const showEdit = ref(false)
const saving = ref(false)

const editForm = ref({
  name: '',
  endpointUrl: '',
  protocol: 'sse',
  isEnabled: true
})

const treeData = computed<TreeNode[]>(() => {
  // 按命名空间分组（工具名按 . 分割）
  const groups = new Map<string, McpTool[]>()
  for (const tool of tools.value) {
    const parts = tool.toolName.split('.')
    const ns = parts.length > 1 ? parts.slice(0, -1).join('.') : '未分组'
    if (!groups.has(ns)) groups.set(ns, [])
    groups.get(ns)!.push(tool)
  }

  return Array.from(groups.entries()).map(([ns, groupTools]) => ({
    id: `ns-${ns}`,
    label: ns,
    children: groupTools.map(t => ({
      id: t.id,
      label: t.toolName,
      isTool: true
    }))
  }))
})

const treeProps = { children: 'children', label: 'label' }

function handleNodeClick(node: TreeNode) {
  if (!node.isTool) return
  selectedTool.value = tools.value.find(t => t.id === node.id) || null
}

function formatJson(s: string): string {
  try { return JSON.stringify(JSON.parse(s), null, 2) } catch { return s }
}

async function fetchDetail() {
  try {
    const res = await http.get<{ data: McpEndpoint }>(`/McpEndpoints/${endpointId}`)
    endpoint.value = res.data.data
    tools.value = res.data.data.tools || []
    editForm.value = {
      name: res.data.data.name,
      endpointUrl: res.data.data.endpointUrl,
      protocol: res.data.data.protocol,
      isEnabled: res.data.data.isEnabled
    }
  } catch {
    ElMessage.error('获取端点详情失败')
  }
}

async function handleDiscover() {
  discovering.value = true
  try {
    await http.post(`/McpEndpoints/${endpointId}/discover`)
    ElMessage.success('工具发现完成')
    await fetchDetail()
  } catch {
    ElMessage.error('工具发现失败')
  } finally {
    discovering.value = false
  }
}

async function handleSave() {
  saving.value = true
  try {
    await http.put(`/McpEndpoints/${endpointId}`, editForm.value)
    ElMessage.success('保存成功')
    showEdit.value = false
    await fetchDetail()
  } catch {
    ElMessage.error('保存失败')
  } finally {
    saving.value = false
  }
}

onMounted(fetchDetail)
</script>

<style scoped>
.mcp-detail-page {
  padding: 16px;
}
.header-card {
  margin-bottom: 0;
}
.detail-label {
  font-size: 14px;
  color: #909399;
  margin-right: 8px;
}
code {
  background: #f5f7fa;
  padding: 2px 8px;
  border-radius: 4px;
  font-size: 13px;
}
</style>
