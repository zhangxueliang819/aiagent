<template>
  <div>
    <el-card>
      <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px">
        <h3 style="margin:0">Agent 列表</h3>
        <el-button type="primary" @click="showDialog = true">创建 Agent</el-button>
      </div>
      <el-table :data="agentStore.agents" v-loading="agentStore.loading" stripe>
        <el-table-column prop="name" label="名称" min-width="150" />
        <el-table-column prop="description" label="描述" min-width="200" show-overflow-tooltip />
        <el-table-column prop="modelEndpointName" label="模型" width="150">
          <template #default="{ row }">{{ row.modelEndpointName || row.modelId || '未配置' }}</template>
        </el-table-column>
        <el-table-column prop="version" label="版本" width="80" />
        <el-table-column prop="status" label="状态" width="100">
          <template #default="{ row }">
            <el-tag :type="row.status === 'Active' ? 'success' : row.status === 'Draft' ? 'info' : 'warning'">
              {{ row.status }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="createdAt" label="创建时间" width="180">
          <template #default="{ row }">{{ new Date(row.createdAt).toLocaleString() }}</template>
        </el-table-column>
        <el-table-column label="操作" width="160" fixed="right">
          <template #default="{ row }">
            <el-button size="small" @click="editAgent(row)">编辑</el-button>
            <el-button size="small" type="danger" @click="handleDelete(row.id)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <!-- Create Dialog -->
    <el-dialog v-model="showDialog" title="创建 Agent" width="600px">
      <el-form :model="form" label-width="110px">
        <el-form-item label="名称" required><el-input v-model="form.name" /></el-form-item>
        <el-form-item label="描述"><el-input v-model="form.description" type="textarea" /></el-form-item>
        <el-form-item label="System Prompt"><el-input v-model="form.systemPrompt" type="textarea" :rows="4" /></el-form-item>
        <el-form-item label="模型">
          <el-select v-model="form.modelEndpointId" placeholder="选择模型端点" clearable filterable style="width:100%">
            <el-option-group v-for="p in modelStore.providers" :key="p.id" :label="p.name">
              <el-option v-for="ep in p.endpoints" :key="ep.id" :label="`${ep.modelName} (${ep.modelId})`" :value="ep.id" />
            </el-option-group>
          </el-select>
        </el-form-item>
        <el-form-item label="创建者"><el-input v-model="form.createdBy" /></el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showDialog = false">取消</el-button>
        <el-button type="primary" @click="handleCreate" :loading="creating">创建</el-button>
      </template>
    </el-dialog>

    <!-- Edit Dialog -->
    <el-dialog v-model="showEditDialog" title="编辑 Agent" width="750px" @open="onEditDialogOpened">
      <el-tabs v-model="editActiveTab" type="border-card">
        <!-- 基本信息 Tab -->
        <el-tab-pane label="基本信息" name="basic">
          <el-form :model="editForm" label-width="110px">
            <el-form-item label="名称"><el-input v-model="editForm.name" /></el-form-item>
            <el-form-item label="描述"><el-input v-model="editForm.description" type="textarea" /></el-form-item>
            <el-form-item label="System Prompt"><el-input v-model="editForm.systemPrompt" type="textarea" :rows="5" /></el-form-item>
            <el-form-item label="模型">
              <el-select v-model="editForm.modelEndpointId" placeholder="选择模型端点" clearable filterable style="width:100%">
                <el-option-group v-for="p in modelStore.providers" :key="p.id" :label="p.name">
                  <el-option v-for="ep in p.endpoints" :key="ep.id" :label="`${ep.modelName} (${ep.modelId})`" :value="ep.id" />
                </el-option-group>
              </el-select>
            </el-form-item>
            <el-form-item label="状态">
              <el-select v-model="editForm.status">
                <el-option label="草稿" value="Draft" />
                <el-option label="活跃" value="Active" />
                <el-option label="停用" value="Inactive" />
                <el-option label="归档" value="Archived" />
              </el-select>
            </el-form-item>
          </el-form>
        </el-tab-pane>

        <!-- 技能绑定 Tab -->
        <el-tab-pane label="技能绑定" name="skills">
          <div style="margin-bottom: 12px; display: flex; gap: 8px">
            <el-select v-model="selectedSkillId" placeholder="选择技能" filterable style="flex:1"
              :disabled="loadingSkills" :loading="loadingSkills">
              <el-option
                v-for="s in availableSkills"
                :key="s.id" :label="`${s.name} (${s.type})`" :value="s.id"
                :disabled="boundSkills.some(b => b.targetId === s.id)" />
            </el-select>
            <el-input-number v-model="skillPriority" :min="0" :max="100" style="width:100px" placeholder="优先级" />
            <el-button type="primary" @click="handleBindSkill" :loading="bindingSkill" :disabled="!selectedSkillId">绑定</el-button>
          </div>
          <el-table :data="boundSkills" size="small" max-height="250">
            <el-table-column prop="targetName" label="技能名称" />
            <el-table-column prop="priority" label="优先级" width="80" />
            <el-table-column prop="isEnabled" label="启用" width="80">
              <template #default="{ row }">
                <el-tag :type="row.isEnabled ? 'success' : 'info'" size="small">{{ row.isEnabled ? '是' : '否' }}</el-tag>
              </template>
            </el-table-column>
            <el-table-column label="操作" width="80">
              <template #default="{ row }">
                <el-button size="small" type="danger" @click="handleUnbindSkill(row.bindingId)">移除</el-button>
              </template>
            </el-table-column>
          </el-table>
          <el-empty v-if="boundSkills.length === 0 && !loadingSkills" description="暂未绑定技能" :image-size="60" />
        </el-tab-pane>

        <!-- MCP 绑定 Tab -->
        <el-tab-pane label="MCP 绑定" name="mcp">
          <div style="margin-bottom: 12px; display: flex; gap: 8px">
            <el-select v-model="selectedMcpId" placeholder="选择 MCP 端点" filterable style="flex:1"
              :disabled="loadingMcp" :loading="loadingMcp">
              <el-option
                v-for="m in availableMcpEndpoints"
                :key="m.id" :label="`${m.name} (${m.protocol})`" :value="m.id"
                :disabled="boundMcps.some(b => b.targetId === m.id)" />
            </el-select>
            <el-input-number v-model="mcpPriority" :min="0" :max="100" style="width:100px" placeholder="优先级" />
            <el-button type="primary" @click="handleBindMcp" :loading="bindingMcp" :disabled="!selectedMcpId">绑定</el-button>
          </div>
          <el-table :data="boundMcps" size="small" max-height="250">
            <el-table-column prop="targetName" label="端点名称" />
            <el-table-column prop="priority" label="优先级" width="80" />
            <el-table-column prop="isEnabled" label="启用" width="80">
              <template #default="{ row }">
                <el-tag :type="row.isEnabled ? 'success' : 'info'" size="small">{{ row.isEnabled ? '是' : '否' }}</el-tag>
              </template>
            </el-table-column>
            <el-table-column label="操作" width="80">
              <template #default="{ row }">
                <el-button size="small" type="danger" @click="handleUnbindMcp(row.bindingId)">移除</el-button>
              </template>
            </el-table-column>
          </el-table>
          <el-empty v-if="boundMcps.length === 0 && !loadingMcp" description="暂未绑定 MCP 端点" :image-size="60" />
        </el-tab-pane>
      </el-tabs>
      <template #footer>
        <el-button @click="showEditDialog = false">取消</el-button>
        <el-button type="primary" @click="handleUpdate" :loading="updating">保存基本信息</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { useAgentStore, type Agent, type AgentSkillBinding, type AgentMcpBinding } from '../stores/agent'
import { useSkillStore, type Skill } from '../stores/skill'
import { useModelStore } from '../stores/model'
import http from '../api/http'
import { ElMessage, ElMessageBox } from 'element-plus'

const agentStore = useAgentStore()
const skillStore = useSkillStore()
const modelStore = useModelStore()
const showDialog = ref(false)
const showEditDialog = ref(false)
const creating = ref(false)
const updating = ref(false)
const editingId = ref('')

const form = reactive({ name: '', description: '', systemPrompt: '', modelId: '', modelEndpointId: null as string | null, createdBy: 'admin' })
const editForm = reactive({ name: '', description: '', systemPrompt: '', modelId: '', modelEndpointId: null as string | null, status: '' })

// Edit dialog tabs
const editActiveTab = ref('basic')

// Skills binding
const boundSkills = ref<AgentSkillBinding[]>([])
const availableSkills = ref<Skill[]>([])
const selectedSkillId = ref('')
const skillPriority = ref(0)
const loadingSkills = ref(false)
const bindingSkill = ref(false)

// MCP binding
const boundMcps = ref<AgentMcpBinding[]>([])
const availableMcpEndpoints = ref<{ id: string; name: string; protocol: string }[]>([])
const selectedMcpId = ref('')
const mcpPriority = ref(0)
const loadingMcp = ref(false)
const bindingMcp = ref(false)

onMounted(() => {
  agentStore.fetchAll()
  skillStore.fetchAll()
  modelStore.fetchAll()
})

async function handleCreate() {
  creating.value = true
  try {
    await agentStore.create({ ...form })
    ElMessage.success('创建成功')
    showDialog.value = false
    Object.assign(form, { name: '', description: '', systemPrompt: '', modelId: '', modelEndpointId: null, createdBy: 'admin' })
  } catch {
    ElMessage.error('创建失败')
  } finally {
    creating.value = false
  }
}

function editAgent(agent: Agent) {
  editingId.value = agent.id
  Object.assign(editForm, { name: agent.name, description: agent.description, systemPrompt: agent.systemPrompt, modelId: agent.modelId, modelEndpointId: agent.modelEndpointId, status: agent.status })
  editActiveTab.value = 'basic'
  showEditDialog.value = true
}

async function onEditDialogOpened() {
  await Promise.all([loadBoundSkills(), loadBoundMcps()])
}

async function loadBoundSkills() {
  loadingSkills.value = true
  try {
    boundSkills.value = await agentStore.fetchSkills(editingId.value)
    availableSkills.value = skillStore.skills
  } catch { /* ignore */ }
  finally { loadingSkills.value = false }
}

async function loadBoundMcps() {
  loadingMcp.value = true
  try {
    boundMcps.value = await agentStore.fetchMcpEndpoints(editingId.value)
    const res = await http.get<{ data: { id: string; name: string; protocol: string }[] }>('/McpEndpoints')
    availableMcpEndpoints.value = res.data.data
  } catch { /* ignore */ }
  finally { loadingMcp.value = false }
}

async function handleBindSkill() {
  if (!selectedSkillId.value) return
  bindingSkill.value = true
  try {
    await agentStore.bindSkill(editingId.value, selectedSkillId.value, skillPriority.value)
    ElMessage.success('技能绑定成功')
    selectedSkillId.value = ''
    skillPriority.value = 0
    await loadBoundSkills()
  } catch {
    ElMessage.error('绑定失败')
  } finally { bindingSkill.value = false }
}

async function handleUnbindSkill(bindingId: string) {
  try {
    await ElMessageBox.confirm('确认移除该技能绑定？', '提示', { type: 'warning' })
    await agentStore.unbindSkill(editingId.value, bindingId)
    ElMessage.success('已移除')
    await loadBoundSkills()
  } catch { /* cancelled */ }
}

async function handleBindMcp() {
  if (!selectedMcpId.value) return
  bindingMcp.value = true
  try {
    await agentStore.bindMcp(editingId.value, selectedMcpId.value, mcpPriority.value)
    ElMessage.success('MCP 绑定成功')
    selectedMcpId.value = ''
    mcpPriority.value = 0
    await loadBoundMcps()
  } catch {
    ElMessage.error('绑定失败')
  } finally { bindingMcp.value = false }
}

async function handleUnbindMcp(bindingId: string) {
  try {
    await ElMessageBox.confirm('确认移除该 MCP 绑定？', '提示', { type: 'warning' })
    await agentStore.unbindMcp(editingId.value, bindingId)
    ElMessage.success('已移除')
    await loadBoundMcps()
  } catch { /* cancelled */ }
}

async function handleUpdate() {
  updating.value = true
  try {
    await agentStore.update(editingId.value, { ...editForm })
    ElMessage.success('更新成功')
    showEditDialog.value = false
  } catch {
    ElMessage.error('更新失败')
  } finally {
    updating.value = false
  }
}

async function handleDelete(id: string) {
  try {
    await ElMessageBox.confirm('确认删除此 Agent？', '提示', { type: 'warning' })
    await agentStore.remove(id)
    ElMessage.success('删除成功')
  } catch { /* cancelled */ }
}
</script>
