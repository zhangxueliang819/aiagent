<template>
  <div>
    <el-card>
      <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px">
        <h3 style="margin:0">MCP 端点管理</h3>
        <el-button type="primary" @click="openDialog()">添加端点</el-button>
      </div>
      <el-table :data="endpoints" v-loading="loading" stripe row-key="id">
        <el-table-column type="expand">
          <template #default="{ row }">
            <div style="padding: 0 20px 12px">
              <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 8px">
                <h4 style="margin:0">已发现工具 ({{ row.tools?.length || 0 }})</h4>
                <el-button size="small" type="primary" @click="handleDiscover(row.id)" :loading="discoveringId === row.id">
                  {{ discoveringId === row.id ? '发现中…' : '重新发现' }}
                </el-button>
              </div>
              <el-table :data="row.tools || []" size="small" v-if="row.tools?.length">
                <el-table-column prop="toolName" label="工具名称" min-width="160" />
                <el-table-column prop="description" label="描述" min-width="200" show-overflow-tooltip />
                <el-table-column label="Schema" min-width="180" show-overflow-tooltip>
                  <template #default="{ row: tool }">{{ tool.inputSchema?.substring(0, 80) }}</template>
                </el-table-column>
                <el-table-column label="状态" width="80">
                  <template #default="{ row: tool }">
                    <el-switch
                      v-model="tool.isEnabled"
                      size="small"
                      @change="(val: boolean) => handleToggleTool(row.id, tool.id, val)"
                    />
                  </template>
                </el-table-column>
              </el-table>
              <el-empty v-else description="暂未发现工具，点击「重新发现」从 MCP 端点获取" :image-size="40" />
            </div>
          </template>
        </el-table-column>
        <el-table-column prop="name" label="名称" min-width="140" />
        <el-table-column prop="endpointUrl" label="端点 URL" min-width="250" show-overflow-tooltip>
          <template #default="{ row }">
            <div style="display: flex; align-items: center; gap: 4px">
              <code style="flex: 1; overflow: hidden; text-overflow: ellipsis">{{ row.endpointUrl }}</code>
              <el-tooltip content="复制 URL">
                <el-button size="small" link :icon="CopyDocument" @click.stop="copyUrl(row.endpointUrl)" />
              </el-tooltip>
            </div>
          </template>
        </el-table-column>
        <el-table-column prop="protocol" label="协议" width="80">
          <template #default="{ row }">
            <el-tag
              :type="row.protocol === 'sse' ? 'primary' : 'info'"
              size="small"
              effect="plain"
            >
              {{ row.protocol?.toUpperCase() }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column label="工具数" width="80">
          <template #default="{ row }">{{ row.tools?.length || 0 }}</template>
        </el-table-column>
        <el-table-column label="状态" width="80">
          <template #default="{ row }">
            <el-tag :type="row.isEnabled ? 'success' : 'danger'" size="small">{{ row.isEnabled ? '启用' : '禁用' }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="createdAt" label="创建时间" width="170">
          <template #default="{ row }">{{ new Date(row.createdAt).toLocaleString() }}</template>
        </el-table-column>
        <el-table-column label="操作" width="220">
          <template #default="{ row }">
            <el-button size="small" link type="primary" @click="$router.push(`/mcps/${row.id}`)">详情</el-button>
            <el-button size="small" link type="primary" @click="openDialog(row)">编辑</el-button>
            <el-button size="small" link type="danger" @click="handleDelete(row.id)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <el-dialog v-model="showDialog" :title="isEditing ? '编辑 MCP 端点' : '添加 MCP 端点'" width="500px" @closed="resetForm">
      <el-form :model="form" label-width="100px">
        <el-form-item label="名称" required><el-input v-model="form.name" placeholder="如 My Database MCP" /></el-form-item>
        <el-form-item label="端点 URL" required>
          <el-input v-model="form.endpointUrl" placeholder="如 http://localhost:8080/mcp" />
        </el-form-item>
        <el-form-item label="协议">
          <el-select v-model="form.protocol" style="width:100%">
            <el-option label="SSE" value="sse" />
            <el-option label="STDIO" value="stdio" />
          </el-select>
        </el-form-item>
        <el-form-item v-if="isEditing" label="状态">
          <el-switch v-model="form.isEnabled" active-text="启用" inactive-text="禁用" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showDialog = false">取消</el-button>
        <el-button type="primary" @click="handleSubmit" :loading="saving">{{ isEditing ? '保存' : '添加' }}</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import http from '../api/http'
import { ElMessage, ElMessageBox } from 'element-plus'
import { CopyDocument } from '@element-plus/icons-vue'

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

const endpoints = ref<McpEndpoint[]>([])
const loading = ref(false)
const showDialog = ref(false)
const isEditing = ref(false)
const editingId = ref('')
const saving = ref(false)
const discoveringId = ref('')

const form = reactive({
  name: '',
  endpointUrl: '',
  protocol: 'sse',
  isEnabled: true
})

const defaultForm = () => ({ name: '', endpointUrl: '', protocol: 'sse' as string, isEnabled: true })

onMounted(() => fetchAll())

async function fetchAll() {
  loading.value = true
  try {
    const res = await http.get<{ data: McpEndpoint[] }>('/McpEndpoints')
    endpoints.value = res.data.data
  } catch {
    ElMessage.error('获取 MCP 端点列表失败')
  } finally {
    loading.value = false
  }
}

function openDialog(endpoint?: McpEndpoint) {
  if (endpoint) {
    isEditing.value = true
    editingId.value = endpoint.id
    Object.assign(form, {
      name: endpoint.name,
      endpointUrl: endpoint.endpointUrl,
      protocol: endpoint.protocol,
      isEnabled: endpoint.isEnabled
    })
  } else {
    isEditing.value = false
    editingId.value = ''
    Object.assign(form, defaultForm())
  }
  showDialog.value = true
}

function resetForm() {
  Object.assign(form, defaultForm())
  isEditing.value = false
  editingId.value = ''
}

async function handleSubmit() {
  if (!form.name.trim()) { ElMessage.warning('请输入名称'); return }
  if (!form.endpointUrl.trim()) { ElMessage.warning('请输入端点 URL'); return }
  saving.value = true
  try {
    if (isEditing.value) {
      await http.put(`/McpEndpoints/${editingId.value}`, { ...form })
      ElMessage.success('更新成功')
    } else {
      await http.post('/McpEndpoints', { name: form.name, endpointUrl: form.endpointUrl, protocol: form.protocol })
      ElMessage.success('添加成功')
    }
    showDialog.value = false
    await fetchAll()
  } catch {
    ElMessage.error(isEditing.value ? '更新失败' : '添加失败')
  } finally {
    saving.value = false
  }
}

async function handleDelete(id: string) {
  try {
    await ElMessageBox.confirm('确认删除该 MCP 端点？关联的 Agent 绑定也将失效。', '提示', { type: 'warning' })
    await http.delete(`/McpEndpoints/${id}`)
    ElMessage.success('删除成功')
    await fetchAll()
  } catch { /* cancelled */ }
}

function copyUrl(url: string) {
  navigator.clipboard.writeText(url).then(() => {
    ElMessage.success('URL 已复制')
  }).catch(() => {
    ElMessage.warning('复制失败，请手动复制')
  })
}

async function handleToggleTool(endpointId: string, toolId: string, isEnabled: boolean) {
  try {
    await http.put(`/McpEndpoints/${endpointId}/tools/${toolId}`, { isEnabled })
    ElMessage.success(isEnabled ? '工具已启用' : '工具已禁用')
  } catch {
    ElMessage.error('操作失败')
    await fetchAll()
  }
}

async function handleDiscover(id: string) {
  discoveringId.value = id
  try {
    const res = await http.post<{ message: string }>(`/McpEndpoints/${id}/discover`)
    ElMessage.success(res.data.message || '工具发现完成')
    await fetchAll()
  } catch {
    ElMessage.error('工具发现失败')
  } finally {
    discoveringId.value = ''
  }
}
</script>
