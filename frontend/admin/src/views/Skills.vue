<template>
  <div>
    <el-card>
      <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px">
        <h3 style="margin:0">技能列表</h3>
        <el-button type="primary" @click="openDialog()">创建技能</el-button>
      </div>
      <el-row :gutter="16" style="margin-bottom: 16px">
        <el-col :span="8">
          <el-input v-model="searchQuery" placeholder="搜索技能名称…" clearable prefix-icon="Search" />
        </el-col>
        <el-col :span="4">
          <el-select v-model="typeFilter" placeholder="类型筛选" clearable style="width:100%">
            <el-option label="Tool" value="Tool" />
            <el-option label="Api" value="Api" />
            <el-option label="Script" value="Script" />
          </el-select>
        </el-col>
      </el-row>
      <el-table :data="filteredSkills" v-loading="skillStore.loading" stripe>
        <el-table-column prop="name" label="名称" min-width="150" />
        <el-table-column prop="description" label="描述" min-width="200" show-overflow-tooltip />
        <el-table-column prop="type" label="类型" width="100" />
        <el-table-column label="状态" width="100">
          <template #default="{ row }">
            <el-tag :type="row.isEnabled ? 'success' : 'danger'">{{ row.isEnabled ? '启用' : '禁用' }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="Schema" min-width="200" show-overflow-tooltip>
          <template #default="{ row }">{{ row.inputSchema?.substring(0, 100) }}</template>
        </el-table-column>
        <el-table-column label="操作" width="150">
          <template #default="{ row }">
            <el-button size="small" link type="primary" @click="openDialog(row)">编辑</el-button>
            <el-button size="small" link type="danger" @click="handleDelete(row.id)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <el-dialog v-model="showDialog" :title="isEditing ? '编辑技能' : '创建技能'" width="600px">
      <el-form :model="form" label-width="100px">
        <el-form-item label="名称" required><el-input v-model="form.name" /></el-form-item>
        <el-form-item label="描述"><el-input v-model="form.description" type="textarea" /></el-form-item>
        <el-form-item label="类型">
          <el-select v-model="form.type">
            <el-option label="Tool" value="Tool" />
            <el-option label="Api" value="Api" />
            <el-option label="Script" value="Script" />
          </el-select>
        </el-form-item>
        <el-form-item label="实现">
          <el-input v-model="form.implementation" :placeholder="implPlaceholder" />
          <div style="font-size:12px;color:#909399;margin-top:4px">{{ implHint }}</div>
          <div v-if="implTemplates.length > 0" style="margin-top:6px;display:flex;gap:8px;flex-wrap:wrap">
            <el-button v-for="tpl in implTemplates" :key="tpl.label" size="small" link type="primary"
              @click="form.implementation = tpl.value">
              {{ tpl.label }}
            </el-button>
          </div>
        </el-form-item>
        <el-form-item label="Schema">
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
      </el-form>
      <template #footer>
        <el-button @click="showDialog = false">取消</el-button>
        <el-button type="primary" @click="handleSubmit" :loading="saving">{{ isEditing ? '保存' : '创建' }}</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { useSkillStore, type Skill } from '../stores/skill'
import { ElMessage, ElMessageBox } from 'element-plus'

const skillStore = useSkillStore()

// 搜索过滤
const searchQuery = ref('')
const typeFilter = ref('')

const filteredSkills = computed(() => {
  let list = skillStore.skills
  if (searchQuery.value) {
    const q = searchQuery.value.toLowerCase()
    list = list.filter(s => s.name.toLowerCase().includes(q))
  }
  if (typeFilter.value) {
    list = list.filter(s => s.type === typeFilter.value)
  }
  return list
})

const showDialog = ref(false)
const isEditing = ref(false)
const editingId = ref('')
const saving = ref(false)

const defaultForm = { name: '', description: '', type: 'Tool', implementation: '', inputSchema: '{ "type": "object" }' }
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
  Tool: {
    placeholder: '输入函数名，如 get_weather、send_email',
    hint: 'Tool 类型：填写函数名，由 FunctionCallHandler 反射调用',
    templates: [
      { label: '天气查询', value: 'get_weather' },
      { label: '邮件发送', value: 'send_email' },
      { label: '数据计算', value: 'calculate' },
    ]
  },
  Api: {
    placeholder: '输入 HTTP URL，如 https://api.example.com/v1/query?city={location}',
    hint: 'Api 类型：填写 HTTP URL，支持 {参数名} 占位符替换',
    templates: [
      { label: 'GET 查询', value: 'https://api.example.com/v1/query?q={q}' },
      { label: 'POST 提交', value: 'https://api.example.com/v1/submit' },
    ]
  },
  Script: {
    placeholder: '输入脚本内容，如 def execute(args):\n    return {"result": "success"}',
    hint: 'Script 类型：填写可执行脚本内容，由脚本引擎解释执行',
    templates: [
      { label: '简单脚本', value: 'def execute(args):\n    return {"result": args.get("input", "")}' },
      { label: '数据处理', value: 'def execute(args):\n    data = args.get("data", [])\n    return {"count": len(data), "items": data}' },
    ]
  }
}

const implPlaceholder = computed(() => typeDescriptions[form.type]?.placeholder ?? '')
const implHint = computed(() => typeDescriptions[form.type]?.hint ?? '')
const implTemplates = computed(() => typeDescriptions[form.type]?.templates ?? [])

const schemaError = computed(() => {
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
      name: skill.name, description: skill.description, type: skill.type,
      implementation: skill.implementation, inputSchema: skill.inputSchema
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
  if (schemaError.value) { ElMessage.warning('请修正 Schema 格式'); return }
  saving.value = true
  try {
    if (isEditing.value) {
      await skillStore.update(editingId.value, { ...form })
      ElMessage.success('更新成功')
    } else {
      await skillStore.create({ ...form })
      ElMessage.success('创建成功')
    }
    showDialog.value = false
  } catch {
    ElMessage.error(isEditing.value ? '更新失败' : '创建失败')
  } finally { saving.value = false }
}

async function handleDelete(id: string) {
  try {
    await ElMessageBox.confirm('确认删除？', '提示', { type: 'warning' })
    await skillStore.remove(id)
    ElMessage.success('删除成功')
  } catch { /* cancelled */ }
}
</script>