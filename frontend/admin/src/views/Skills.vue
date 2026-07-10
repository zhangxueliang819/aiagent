<template>
  <div>
    <el-card>
      <!-- 工具栏：搜索 + 筛选 + 操作按钮 -->
      <div style="display: flex; align-items: center; gap: 8px; margin-bottom: 16px">
        <div style="display: flex; align-items: center; gap: 8px; flex: 1">
          <el-input v-model="searchQuery" placeholder="搜索技能名称…" clearable prefix-icon="Search" style="max-width: 220px" />
          <el-select v-model="typeFilter" placeholder="类型筛选" clearable style="width: 130px">
            <el-option label="函数工具" value="FunctionTool" />
            <el-option label="知识技能" value="AgentSkill" />
            <el-option label="MCP 工具" value="McpTool" />
          </el-select>
          <el-select v-model="storageFilter" placeholder="存储筛选" clearable style="width: 100px">
            <el-option label="内联" value="Inline" />
            <el-option label="文件" value="File" />
            <el-option label="目录" value="Directory" />
          </el-select>
        </div>
        <el-button @click="openUploadDialog()" icon="Upload">上传技能包</el-button>
        <el-button type="primary" @click="openDialog()">创建技能</el-button>
      </div>

      <el-table :data="filteredSkills" v-loading="skillStore.loading" stripe>
        <el-table-column prop="name" label="名称" min-width="150" />
        <el-table-column prop="description" label="描述" min-width="200" show-overflow-tooltip />
        <el-table-column label="类型" width="110">
          <template #default="{ row }">
            <el-tag size="small" :type="typeTagType(row.type)">{{ SkillTypeLabels[row.type] ?? row.type }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="存储" width="80">
          <template #default="{ row }">
            <el-tag size="small" :type="storageTagType(row.storageType)" effect="plain">
              {{ StorageTypeLabels[row.storageType] ?? row.storageType }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column label="状态" width="80">
          <template #default="{ row }">
            <el-tag :type="row.isEnabled ? 'success' : 'danger'">{{ row.isEnabled ? '启用' : '禁用' }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="Schema" min-width="160" show-overflow-tooltip>
          <template #default="{ row }">{{ row.inputSchema?.substring(0, 80) }}</template>
        </el-table-column>
        <el-table-column label="操作" width="200">
          <template #default="{ row }">
            <el-button size="small" link type="primary" @click="openDialog(row)">编辑</el-button>
            <el-button v-if="row.storageType === 'File'" size="small" link type="success" @click="viewFiles(row)">
              查看文件
            </el-button>
            <el-button size="small" link type="danger" @click="handleDelete(row.id)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <!-- 创建/编辑技能对话框 -->
    <el-dialog v-model="showDialog" :title="isEditing ? '编辑技能' : '创建技能'" width="640px">
      <el-form :model="form" label-width="100px">
        <el-form-item label="名称" required><el-input v-model="form.name" /></el-form-item>
        <el-form-item label="描述"><el-input v-model="form.description" type="textarea" :rows="2" /></el-form-item>
        <el-form-item label="类型">
          <el-select v-model="form.type" @change="onTypeChange">
            <el-option label="FunctionTool - 函数工具" value="FunctionTool" />
            <el-option label="AgentSkill - 知识技能" value="AgentSkill" />
            <el-option label="McpTool - MCP 工具" value="McpTool" />
          </el-select>
        </el-form-item>
        <el-form-item label="实现">
          <template v-if="form.type === 'AgentSkill'">
            <el-input v-model="form.implementation" type="textarea" :rows="8" :placeholder="implPlaceholder" />
            <div style="font-size:12px;color:#909399;margin-top:4px">{{ implHint }}</div>
          </template>
          <template v-else>
            <el-input v-model="form.implementation" :placeholder="implPlaceholder" />
            <div style="font-size:12px;color:#909399;margin-top:4px">{{ implHint }}</div>
            <div v-if="implTemplates.length > 0" style="margin-top:6px;display:flex;gap:8px;flex-wrap:wrap">
              <el-button v-for="tpl in implTemplates" :key="tpl.label" size="small" link type="primary"
                @click="form.implementation = tpl.value">
                {{ tpl.label }}
              </el-button>
            </div>
          </template>
        </el-form-item>
        <el-form-item v-if="form.type !== 'AgentSkill'" label="Schema">
          <el-input v-model="form.inputSchema" type="textarea" :rows="8"
            placeholder='{"type":"object","properties":{"input":{"type":"string","description":"参数说明"}},"required":["input"]}' />
          <div style="font-size:12px;color:#909399;margin-top:4px">
            <strong>JSON Schema</strong> — LLM 据此判断何时调用技能、传什么参数
          </div>
          <div style="margin-top:6px;display:flex;gap:8px">
            <el-button size="small" link type="primary" @click="applyTemplate('empty')">空参数</el-button>
            <el-button size="small" link type="primary" @click="applyTemplate('simple')">单参数</el-button>
            <el-button size="small" link type="primary" @click="applyTemplate('multi')">多参数</el-button>
          </div>
          <div v-if="schemaError" style="font-size:12px;color:#e6a23c;margin-top:4px">
            ⚠ {{ schemaError }}
          </div>
        </el-form-item>
        <el-form-item v-if="isEditing" label="状态">
          <el-switch v-model="form.isEnabled" active-text="启用" inactive-text="禁用" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showDialog = false">取消</el-button>
        <el-button type="primary" @click="handleSubmit" :loading="saving">{{ isEditing ? '保存' : '创建' }}</el-button>
      </template>
    </el-dialog>

    <!-- 上传技能包对话框 -->
    <el-dialog v-model="showUploadDialog" title="上传技能包" width="560px">
      <el-upload
        ref="uploadRef"
        class="upload-area"
        drag
        :auto-upload="false"
        :limit="1"
        accept=".zip"
        :on-change="onFileChange"
        :on-remove="onFileRemove"
      >
        <el-icon :size="48" color="#409EFF"><UploadFilled /></el-icon>
        <div style="margin-top: 12px">
          <span style="color: #409EFF; font-size: 14px">点击或拖拽 .zip 文件到此区域</span>
          <div style="font-size: 12px; color: #909399; margin-top: 6px">
            仅支持 .zip 格式的技能包，最大 50MB
          </div>
          <div style="font-size: 12px; color: #909399">
            包根目录必须包含 SKILL.md 文件
          </div>
        </div>
      </el-upload>

      <!-- 上传结果预览 -->
      <div v-if="uploadResult" style="margin-top: 16px">
        <el-alert type="success" :closable="false" show-icon>
          <template #title>
            上传成功：<strong>{{ uploadResult.skill.name }}</strong>
          </template>
          类型：{{ SkillTypeLabels[uploadResult.skill.type] }} |
          文件数：{{ uploadResult.files.length }}
        </el-alert>
        <div v-if="uploadResult.files.length > 0" style="margin-top: 12px">
          <div style="font-size: 13px; font-weight: 500; margin-bottom: 6px">📁 包内文件清单</div>
          <div v-for="f in uploadResult.files.slice(0, 15)" :key="f.path"
            style="font-size:12px;color:#606266;padding:2px 0;display:flex;justify-content:space-between">
            <span>{{ f.path }}</span>
            <span style="color:#909399">{{ formatSize(f.size) }}</span>
          </div>
          <div v-if="uploadResult.files.length > 15" style="font-size:11px;color:#909399;margin-top:4px">
            …以及 {{ uploadResult.files.length - 15 }} 个文件
          </div>
        </div>
      </div>

      <template #footer>
        <el-button @click="showUploadDialog = false">关闭</el-button>
        <el-button type="primary" @click="submitUpload" :loading="uploading" :disabled="!pendingFile">
          {{ uploading ? '上传中…' : '确认上传' }}
        </el-button>
      </template>
    </el-dialog>

    <!-- 文件列表对话框 -->
    <el-dialog v-model="showFilesDialog" :title="`文件列表 - ${viewingSkill?.name}`" width="500px">
      <div v-if="fileList.length > 0">
        <div v-for="f in fileList" :key="f.path"
          style="display:flex;justify-content:space-between;align-items:center;padding:6px 0;border-bottom:1px solid #ebeef5">
          <span style="font-size:13px;display:flex;align-items:center;gap:6px">
            <span>{{ fileIcon(f.path) }}</span>
            <span>{{ f.path }}</span>
          </span>
          <span style="font-size:12px;color:#909399">{{ formatSize(f.size) }}</span>
        </div>
      </div>
      <div v-else style="text-align:center;color:#909399;padding:24px">
        暂无文件信息
      </div>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { UploadFilled } from '@element-plus/icons-vue'
import { useSkillStore, type Skill, type SkillUploadResponse, SkillTypeLabels, StorageTypeLabels } from '../stores/skill'
import { ElMessage, ElMessageBox } from 'element-plus'
import type { UploadInstance, UploadFile } from 'element-plus'

const skillStore = useSkillStore()

// 搜索过滤
const searchQuery = ref('')
const typeFilter = ref('')
const storageFilter = ref('')

const filteredSkills = computed(() => {
  let list = skillStore.skills
  if (searchQuery.value) {
    const q = searchQuery.value.toLowerCase()
    list = list.filter(s => s.name.toLowerCase().includes(q))
  }
  if (typeFilter.value) {
    list = list.filter(s => s.type === typeFilter.value)
  }
  if (storageFilter.value) {
    list = list.filter(s => s.storageType === storageFilter.value)
  }
  return list
})

// 类型标签颜色
function typeTagType(type: string) {
  const map: Record<string, string> = { FunctionTool: 'primary', AgentSkill: 'success', McpTool: 'warning' }
  return map[type] ?? 'info'
}

function storageTagType(storage: string) {
  const map: Record<string, string> = { Inline: '', File: 'success', Directory: 'warning' }
  return map[storage] ?? ''
}

function formatSize(bytes: number) {
  if (bytes < 1024) return bytes + ' B'
  if (bytes < 1048576) return (bytes / 1024).toFixed(1) + ' KB'
  return (bytes / 1048576).toFixed(1) + ' MB'
}

function fileIcon(path: string) {
  if (path.endsWith('.md')) return '📝'
  if (path.endsWith('.py')) return '🐍'
  if (path.endsWith('.js')) return '📜'
  if (path.endsWith('.ts')) return '📘'
  if (path.endsWith('.json')) return '📋'
  if (path.endsWith('.yaml') || path.endsWith('.yml')) return '⚙️'
  if (path.endsWith('.sh')) return '💻'
  if (path.includes('/')) return '📁'
  return '📄'
}

// 创建/编辑对话框
const showDialog = ref(false)
const isEditing = ref(false)
const editingId = ref('')
const saving = ref(false)

const defaultForm = {
  name: '', description: '', type: 'FunctionTool',
  implementation: '', inputSchema: '{ "type": "object" }',
  isEnabled: true
}
const form = reactive({ ...defaultForm })

const schemaTemplates: Record<string, string> = {
  empty: JSON.stringify({ type: "object", properties: {} }, null, 2),
  simple: JSON.stringify({
    type: "object", properties: { input: { type: "string", description: "输入内容" } },
    required: ["input"]
  }, null, 2),
  multi: JSON.stringify({
    type: "object",
    properties: {
      query: { type: "string", description: "搜索关键词" },
      limit: { type: "integer", description: "返回数量上限", default: 10 }
    },
    required: ["query"]
  }, null, 2),
}

const typeDescriptions: Record<string, {
  placeholder: string
  hint: string
  templates: { label: string; value: string }[]
}> = {
  FunctionTool: {
    placeholder: '输入 Executor 类名，如 WeatherApiExecutor、SendEmailExecutor',
    hint: 'FunctionTool：LLM 可调用的函数工具，Implementation 为 C# Executor 类名',
    templates: [
      { label: '天气查询', value: 'WeatherApiExecutor' },
      { label: '邮件发送', value: 'SendEmailExecutor' },
      { label: '数据计算', value: 'CalculateExecutor' },
    ]
  },
  AgentSkill: {
    placeholder: '输入 Markdown 指令正文（SKILL.md 的 body 部分）…',
    hint: 'AgentSkill：知识指令包，内容为 Markdown 格式的指令/知识，由 Agent 运行时按需加载',
    templates: []
  },
  McpTool: {
    placeholder: '输入 MCP 工具全限定名，如 mcp://server1/tools/get_weather',
    hint: 'McpTool：来自 MCP Server 的工具标识，运行时由 MAF LocalMcpTools 自动发现',
    templates: [
      { label: 'MCP 工具标识示例', value: 'mcp://filesystem/tools/list_directory' },
    ]
  },
}

// 兼容旧类型名称
function normalizeType(type: string): string {
  const map: Record<string, string> = { Tool: 'FunctionTool', Api: 'FunctionTool', Script: 'AgentSkill', Composite: 'AgentSkill' }
  return map[type] ?? type
}

function onTypeChange(newType: string) {
  form.implementation = ''
  if (newType === 'AgentSkill') {
    form.inputSchema = '{}'
  }
}

const implPlaceholder = computed(() => typeDescriptions[form.type]?.placeholder ?? '')
const implHint = computed(() => typeDescriptions[form.type]?.hint ?? '')
const implTemplates = computed(() => typeDescriptions[form.type]?.templates ?? [])

const schemaError = computed(() => {
  if (form.type === 'AgentSkill') return ''
  const v = form.inputSchema.trim()
  if (!v) return '请输入 Schema'
  try { JSON.parse(v); return '' }
  catch { return 'JSON 格式无效，请检查逗号、引号、括号' }
})

function applyTemplate(name: string) { form.inputSchema = schemaTemplates[name] }

onMounted(() => skillStore.fetchAll())

function openDialog(skill?: Skill) {
  if (skill) {
    isEditing.value = true
    editingId.value = skill.id
    Object.assign(form, {
      name: skill.name, description: skill.description,
      type: normalizeType(skill.type),
      implementation: skill.implementation ?? '',
      inputSchema: skill.inputSchema,
      isEnabled: skill.isEnabled
    })
  } else {
    isEditing.value = false
    editingId.value = ''
    Object.assign(form, { ...defaultForm })
  }
  showDialog.value = true
}

async function handleSubmit() {
  if (!form.name.trim()) { ElMessage.warning('请输入名称'); return }
  if (schemaError.value && form.type !== 'AgentSkill') { ElMessage.warning('请修正 Schema 格式'); return }
  saving.value = true
  try {
    if (isEditing.value) {
      await skillStore.update(editingId.value, {
        name: form.name,
        description: form.description,
        type: form.type,
        implementation: form.implementation,
        inputSchema: form.inputSchema,
        isEnabled: form.isEnabled
      })
      ElMessage.success('更新成功')
    } else {
      await skillStore.create({
        name: form.name,
        description: form.description,
        type: form.type,
        implementation: form.implementation,
        inputSchema: form.inputSchema
      })
      ElMessage.success('创建成功')
    }
    showDialog.value = false
  } catch {
    ElMessage.error(isEditing.value ? '更新失败' : '创建失败')
  } finally { saving.value = false }
}

async function handleDelete(id: string) {
  try {
    await ElMessageBox.confirm('确认删除？此操作不可恢复（文件型技能会同时清理磁盘文件）', '提示', { type: 'warning' })
    await skillStore.remove(id)
    ElMessage.success('删除成功')
  } catch { /* cancelled */ }
}

// 上传相关
const showUploadDialog = ref(false)
const uploadRef = ref<UploadInstance>()
const pendingFile = ref<File | null>(null)
const uploading = ref(false)
const uploadResult = ref<SkillUploadResponse | null>(null)

function openUploadDialog() {
  uploadResult.value = null
  pendingFile.value = null
  showUploadDialog.value = true
}

function onFileChange(file: UploadFile) {
  pendingFile.value = file.raw ?? null
  uploadResult.value = null
}

function onFileRemove() {
  pendingFile.value = null
  uploadResult.value = null
}

async function submitUpload() {
  if (!pendingFile.value) return
  uploading.value = true
  try {
    const result = await skillStore.upload(pendingFile.value)
    uploadResult.value = result
    pendingFile.value = null
    uploadRef.value?.clearFiles()
    ElMessage.success('技能包上传成功')
  } catch {
    ElMessage.error('上传失败，请检查文件格式')
  } finally { uploading.value = false }
}

// 文件列表
const showFilesDialog = ref(false)
const viewingSkill = ref<Skill | null>(null)
const fileList = ref<{ path: string; size: number; lastModified: string }[]>([])

async function viewFiles(skill: Skill) {
  viewingSkill.value = skill
  // 优先使用 fileManifest 字段
  if (skill.fileManifest) {
    try {
      fileList.value = JSON.parse(skill.fileManifest)
    } catch {
      fileList.value = []
    }
  } else {
    try {
      fileList.value = await skillStore.getFiles(skill.id)
    } catch {
      fileList.value = []
    }
  }
  showFilesDialog.value = true
}
</script>

<style scoped>
.upload-area :deep(.el-upload-dragger) {
  padding: 32px;
}
</style>