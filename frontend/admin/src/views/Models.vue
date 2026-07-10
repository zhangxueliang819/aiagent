<template>
  <div>
    <el-card>
      <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px">
        <h3 style="margin:0">模型供应商</h3>
        <el-button type="primary" @click="openProviderDialog()">添加供应商</el-button>
      </div>
      <el-table :data="modelStore.providers" v-loading="modelStore.loading" stripe row-key="id">
        <el-table-column type="expand">
          <template #default="{ row: provider }">
            <div style="padding: 0 20px 12px">
              <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 8px">
                <h4 style="margin:0">模型端点</h4>
                <el-button size="small" type="primary" @click="openEndpointDialog(provider.id)">添加端点</el-button>
              </div>
              <el-table :data="provider.endpoints" size="small" v-if="provider.endpoints?.length">
                <el-table-column prop="modelId" label="模型 ID" min-width="150" />
                <el-table-column prop="modelName" label="模型名称" min-width="120" />
                <el-table-column prop="maxTokens" label="Max Tokens" width="120" />
                <el-table-column label="状态" width="80">
                  <template #default="{ row }">
                    <el-tag :type="row.isEnabled ? 'success' : 'danger'" size="small">{{ row.isEnabled ? '启用' : '禁用' }}</el-tag>
                  </template>
                </el-table-column>
                <el-table-column label="操作" width="160">
                  <template #default="{ row: ep }">
                    <el-button size="small" type="primary" link @click.stop="openEndpointEditDialog(provider.id, ep)">编辑</el-button>
                    <el-button size="small" type="danger" link @click.stop="handleDeleteEndpoint(provider.id, ep.id)">删除</el-button>
                  </template>
                </el-table-column>
              </el-table>
              <el-empty v-else description="暂无模型端点，请添加" :image-size="40" />
            </div>
          </template>
        </el-table-column>
        <el-table-column prop="name" label="名称" min-width="150" />
        <el-table-column prop="providerType" label="类型" width="120">
          <template #default="{ row }">
            <el-tag
              :type="row.providerType === 'OpenAI' ? 'primary' : row.providerType === 'Azure' ? 'warning' : 'info'"
              size="small"
            >
              {{ row.providerType }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="apiBaseUrl" label="API 地址" min-width="250" show-overflow-tooltip />
        <el-table-column label="端点数量" width="100">
          <template #default="{ row }">{{ row.endpoints?.length || 0 }}</template>
        </el-table-column>
        <el-table-column label="状态" width="100">
          <template #default="{ row }">
            <el-tag :type="row.isEnabled ? 'success' : 'danger'">{{ row.isEnabled ? '启用' : '禁用' }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="操作" width="260">
          <template #default="{ row }">
            <el-button size="small" type="primary" link @click="handleTestConnection(row)">测试连接</el-button>
            <el-button size="small" type="primary" link @click="openProviderEditDialog(row)">编辑</el-button>
            <el-button size="small" type="danger" link @click="handleDeleteProvider(row.id)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <!-- 添加/编辑供应商对话框 -->
    <el-dialog v-model="showProviderDialog" :title="isEditingProvider ? '编辑模型供应商' : '添加模型供应商'" width="520px" @closed="resetProviderForm">
      <el-form :model="providerForm" label-width="100px">
        <el-form-item label="名称" required><el-input v-model="providerForm.name" /></el-form-item>
        <el-form-item label="类型"><el-input v-model="providerForm.providerType" placeholder="OpenAI" /></el-form-item>
        <el-form-item label="API 地址"><el-input v-model="providerForm.apiBaseUrl" placeholder="https://api.openai.com/v1" /></el-form-item>
        <el-form-item label="API Key"><el-input v-model="providerForm.apiKey" type="password" show-password :placeholder="isEditingProvider ? '留空则不修改' : ''" /></el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showProviderDialog = false">取消</el-button>
        <el-button type="primary" @click="handleSubmitProvider" :loading="saving">{{ isEditingProvider ? '保存' : '添加' }}</el-button>
      </template>
    </el-dialog>

    <!-- 添加/编辑端点对话框 -->
    <el-dialog v-model="showEndpointDialog" :title="isEditingEndpoint ? '编辑模型端点' : '添加模型端点'" width="450px" @closed="resetEndpointForm">
      <el-form :model="endpointForm" label-width="110px">
        <el-form-item label="模型 ID" required><el-input v-model="endpointForm.modelId" placeholder="如 gpt-4o" /></el-form-item>
        <el-form-item label="模型名称" required><el-input v-model="endpointForm.modelName" placeholder="如 GPT-4o" /></el-form-item>
        <el-form-item label="Max Tokens">
          <el-input-number v-model="endpointForm.maxTokens" :min="1" :max="1000000" style="width:100%" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showEndpointDialog = false">取消</el-button>
        <el-button type="primary" @click="handleSubmitEndpoint" :loading="savingEp">{{ isEditingEndpoint ? '保存' : '添加' }}</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { useModelStore, type ModelProvider, type ModelEndpoint } from '../stores/model'
import http from '../api/http'
import { ElMessage, ElMessageBox } from 'element-plus'

const modelStore = useModelStore()

// ─── Provider dialog state ──────────────────────────────────

const showProviderDialog = ref(false)
const isEditingProvider = ref(false)
const editingProviderId = ref('')
const saving = ref(false)
const providerForm = reactive({ name: '', providerType: 'OpenAI', apiBaseUrl: '', apiKey: '' })

// ─── Endpoint dialog state ──────────────────────────────────

const showEndpointDialog = ref(false)
const isEditingEndpoint = ref(false)
const editingEndpointId = ref('')
const savingEp = ref(false)
const currentProviderId = ref('')
const endpointForm = reactive({ modelId: '', modelName: '', maxTokens: 4096 })

onMounted(() => modelStore.fetchAll())

// ─── Provider CRUD ──────────────────────────────────────────

function openProviderDialog(provider?: ModelProvider) {
  if (provider) {
    isEditingProvider.value = true
    editingProviderId.value = provider.id
    Object.assign(providerForm, {
      name: provider.name,
      providerType: provider.providerType,
      apiBaseUrl: provider.apiBaseUrl,
      apiKey: ''
    })
  } else {
    isEditingProvider.value = false
    editingProviderId.value = ''
    Object.assign(providerForm, { name: '', providerType: 'OpenAI', apiBaseUrl: '', apiKey: '' })
  }
  showProviderDialog.value = true
}

function openProviderEditDialog(provider: ModelProvider) {
  openProviderDialog(provider)
}

function resetProviderForm() {
  Object.assign(providerForm, { name: '', providerType: 'OpenAI', apiBaseUrl: '', apiKey: '' })
  isEditingProvider.value = false
  editingProviderId.value = ''
}

async function handleSubmitProvider() {
  saving.value = true
  try {
    if (isEditingProvider.value) {
      const data = { ...providerForm }
      if (!data.apiKey) data.apiKey = '' // 留空不修改
      await modelStore.update(editingProviderId.value, data)
      ElMessage.success('更新成功')
    } else {
      await modelStore.create({ ...providerForm })
      ElMessage.success('添加成功')
    }
    showProviderDialog.value = false
  } catch {
    ElMessage.error(isEditingProvider.value ? '更新失败' : '添加失败')
  } finally {
    saving.value = false
  }
}

async function handleTestConnection(provider: ModelProvider) {
  try {
    ElMessage.info(`正在测试 ${provider.name} 的连接...`)
    await http.post(`/models/${provider.id}/test`)
    ElMessage.success(`${provider.name} 连接测试通过`)
  } catch (e: any) {
    const msg = e?.response?.data?.message || '连接失败'
    ElMessage.error(`${provider.name} 连接测试失败: ${msg}`)
  }
}

async function handleDeleteProvider(id: string) {
  try {
    await ElMessageBox.confirm('确认删除？关联的端点也将被删除。', '提示', { type: 'warning' })
    await modelStore.remove(id)
    ElMessage.success('删除成功')
  } catch { /* cancelled */ }
}

// ─── Endpoint CRUD ──────────────────────────────────────────

function openEndpointDialog(providerId: string) {
  currentProviderId.value = providerId
  isEditingEndpoint.value = false
  editingEndpointId.value = ''
  Object.assign(endpointForm, { modelId: '', modelName: '', maxTokens: 4096 })
  showEndpointDialog.value = true
}

function openEndpointEditDialog(providerId: string, ep: ModelEndpoint) {
  currentProviderId.value = providerId
  isEditingEndpoint.value = true
  editingEndpointId.value = ep.id
  Object.assign(endpointForm, { modelId: ep.modelId, modelName: ep.modelName, maxTokens: ep.maxTokens })
  showEndpointDialog.value = true
}

function resetEndpointForm() {
  Object.assign(endpointForm, { modelId: '', modelName: '', maxTokens: 4096 })
  isEditingEndpoint.value = false
  editingEndpointId.value = ''
}

async function handleSubmitEndpoint() {
  if (!endpointForm.modelId || !endpointForm.modelName) {
    ElMessage.warning('请填写模型 ID 和名称')
    return
  }
  savingEp.value = true
  try {
    if (isEditingEndpoint.value) {
      await modelStore.removeEndpoint(currentProviderId.value, editingEndpointId.value)
      await modelStore.addEndpoint(currentProviderId.value, { ...endpointForm })
      ElMessage.success('端点更新成功')
    } else {
      await modelStore.addEndpoint(currentProviderId.value, { ...endpointForm })
      ElMessage.success('端点添加成功')
    }
    showEndpointDialog.value = false
  } catch {
    ElMessage.error(isEditingEndpoint.value ? '更新失败' : '添加失败')
  } finally {
    savingEp.value = false
  }
}

async function handleDeleteEndpoint(providerId: string, endpointId: string) {
  try {
    await ElMessageBox.confirm('确认删除该端点？', '提示', { type: 'warning' })
    await modelStore.removeEndpoint(providerId, endpointId)
    ElMessage.success('删除成功')
  } catch { /* cancelled */ }
}
</script>
