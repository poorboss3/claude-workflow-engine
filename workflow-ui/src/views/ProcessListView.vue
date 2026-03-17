<template>
  <div class="process-list-view">
    <div class="page-header">
      <div>
        <h2 class="page-title">流程定义列表</h2>
        <p class="page-desc">管理所有工作流程定义</p>
      </div>
      <router-link to="/processes/create">
        <el-button type="primary" :icon="Plus">新建流程定义</el-button>
      </router-link>
    </div>

    <el-card class="filter-card">
      <div class="filter-row">
        <el-select v-model="filterStatus" placeholder="状态筛选" clearable style="width: 140px" @change="fetchList">
          <el-option label="草稿" value="Draft" />
          <el-option label="已激活" value="Active" />
          <el-option label="已归档" value="Archived" />
        </el-select>
        <el-button :icon="Refresh" @click="fetchList">刷新</el-button>
      </div>
    </el-card>

    <el-card class="table-card">
      <el-table
        v-loading="loading"
        :data="definitions"
        stripe
        border
        row-key="id"
        style="width: 100%"
      >
        <el-table-column prop="name" label="流程名称" min-width="160">
          <template #default="{ row }">
            <span class="process-name">{{ row.name || row.Name }}</span>
          </template>
        </el-table-column>
        <el-table-column prop="processType" label="流程类型" min-width="140">
          <template #default="{ row }">{{ row.processType || row.ProcessType }}</template>
        </el-table-column>
        <el-table-column prop="version" label="版本" width="80" align="center">
          <template #default="{ row }">
            <el-tag size="small">v{{ row.version || row.Version || 1 }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="状态" width="100" align="center">
          <template #default="{ row }">
            <el-tag :type="getStatusType(row.status || row.Status)" size="small">
              {{ getStatusLabel(row.status || row.Status) }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column label="创建时间" min-width="160">
          <template #default="{ row }">
            {{ formatDate(row.createdAt || row.CreatedAt) }}
          </template>
        </el-table-column>
        <el-table-column label="操作" width="260" align="center" fixed="right">
          <template #default="{ row }">
            <el-button
              v-if="(row.status || row.Status) === 'Draft'"
              type="success"
              size="small"
              :loading="activatingId === (row.id || row.Id)"
              @click="handleActivate(row)"
            >
              激活
            </el-button>
            <el-button
              v-if="(row.status || row.Status) === 'Active'"
              type="danger"
              size="small"
              :loading="archivingId === (row.id || row.Id)"
              @click="handleArchive(row)"
            >
              归档
            </el-button>
            <el-button
              type="primary"
              size="small"
              :icon="VideoPlay"
              @click="openSubmitDialog(row)"
            >
              提交测试流程
            </el-button>
          </template>
        </el-table-column>
      </el-table>

      <div class="pagination-row">
        <el-pagination
          v-model:current-page="page"
          v-model:page-size="pageSize"
          :total="total"
          :page-sizes="[10, 20, 50]"
          layout="total, sizes, prev, pager, next"
          @current-change="fetchList"
          @size-change="fetchList"
        />
      </div>
    </el-card>

    <!-- Submit Instance Dialog -->
    <el-dialog
      v-model="submitDialogVisible"
      title="提交工作流实例"
      width="640px"
      :close-on-click-modal="false"
      @closed="resetSubmitForm"
    >
      <el-form :model="submitForm" :rules="submitRules" ref="submitFormRef" label-width="100px">
        <el-form-item label="流程类型">
          <el-input :value="submitForm.ProcessType" disabled />
        </el-form-item>
        <el-form-item label="业务Key" prop="BusinessKey">
          <el-input v-model="submitForm.BusinessKey" placeholder="请输入业务唯一标识，如 ORDER-001" />
        </el-form-item>
        <el-form-item label="表单数据" prop="FormData">
          <el-input
            v-model="submitForm.FormDataStr"
            type="textarea"
            :rows="4"
            placeholder='请输入JSON格式表单数据，如 {"title":"测试申请","amount":1000}'
          />
        </el-form-item>
        <el-form-item label="代理人">
          <el-input v-model="submitForm.OnBehalfOf" placeholder="可选，代表某人提交" />
        </el-form-item>

        <el-divider content-position="left">审批步骤配置</el-divider>

        <div class="steps-container">
          <div
            v-for="(step, idx) in submitForm.steps"
            :key="idx"
            class="step-item"
          >
            <div class="step-item-header">
              <span class="step-item-title">步骤 {{ idx + 1 }}</span>
              <el-button
                type="danger"
                size="small"
                :icon="Delete"
                circle
                @click="removeStep(idx)"
              />
            </div>
            <div class="step-assignees-list">
              <div v-for="(assignee, ai) in step.assignees" :key="ai" class="assignee-row">
                <el-input
                  v-model="step.assignees[ai]"
                  placeholder="输入审批人用户ID"
                  size="small"
                  style="flex: 1"
                />
                <el-button
                  type="danger"
                  size="small"
                  :icon="Minus"
                  circle
                  :disabled="step.assignees.length <= 1"
                  @click="removeAssignee(idx, ai)"
                />
              </div>
              <el-button
                size="small"
                :icon="Plus"
                @click="addAssignee(idx)"
                style="margin-top: 4px"
              >
                添加审批人
              </el-button>
            </div>
          </div>

          <el-button
            type="dashed"
            :icon="Plus"
            style="width: 100%; margin-top: 8px"
            @click="addStep"
          >
            添加审批步骤
          </el-button>
        </div>
      </el-form>

      <template #footer>
        <el-button @click="submitDialogVisible = false">取消</el-button>
        <el-button
          type="primary"
          :loading="submitting"
          @click="handleSubmitInstance"
        >
          提交流程
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Plus, Refresh, Delete, Minus, VideoPlay } from '@element-plus/icons-vue'
import { listDefinitions, activateDefinition, archiveDefinition } from '../api/definitions.js'
import { prepareSubmit, submitProcess } from '../api/instances.js'
import { useFlowStore } from '../stores/flowStore.js'

const router = useRouter()
const flowStore = useFlowStore()

const loading = ref(false)
const definitions = ref([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(10)
const filterStatus = ref('')
const activatingId = ref(null)
const archivingId = ref(null)

async function fetchList() {
  loading.value = true
  try {
    const params = { page: page.value, pageSize: pageSize.value }
    if (filterStatus.value) params.status = filterStatus.value
    const res = await listDefinitions(params)
    const data = res.data
    if (Array.isArray(data)) {
      definitions.value = data
      total.value = data.length
    } else if (data && Array.isArray(data.items)) {
      definitions.value = data.items
      total.value = data.total || data.items.length
    } else {
      definitions.value = []
      total.value = 0
    }
  } catch (err) {
    ElMessage.error('加载流程定义失败：' + err.message)
    definitions.value = []
  } finally {
    loading.value = false
  }
}

async function handleActivate(row) {
  const id = row.id || row.Id
  try {
    await ElMessageBox.confirm(`确认激活流程"${row.name || row.Name}"？`, '确认激活', { type: 'warning' })
    activatingId.value = id
    await activateDefinition(id)
    ElMessage.success('流程已激活')
    fetchList()
  } catch (err) {
    if (err !== 'cancel') ElMessage.error('激活失败：' + err.message)
  } finally {
    activatingId.value = null
  }
}

async function handleArchive(row) {
  const id = row.id || row.Id
  try {
    await ElMessageBox.confirm(`确认归档流程"${row.name || row.Name}"？归档后无法再提交实例。`, '确认归档', { type: 'warning' })
    archivingId.value = id
    await archiveDefinition(id)
    ElMessage.success('流程已归档')
    fetchList()
  } catch (err) {
    if (err !== 'cancel') ElMessage.error('归档失败：' + err.message)
  } finally {
    archivingId.value = null
  }
}

function getStatusType(status) {
  const map = { Draft: 'info', Active: 'success', Archived: 'danger' }
  return map[status] || 'info'
}

function getStatusLabel(status) {
  const map = { Draft: '草稿', Active: '已激活', Archived: '已归档' }
  return map[status] || status || '未知'
}

function formatDate(dateStr) {
  if (!dateStr) return '-'
  try {
    return new Date(dateStr).toLocaleString('zh-CN', {
      year: 'numeric', month: '2-digit', day: '2-digit',
      hour: '2-digit', minute: '2-digit'
    })
  } catch {
    return dateStr
  }
}

// Submit Dialog
const submitDialogVisible = ref(false)
const submitting = ref(false)
const submitFormRef = ref(null)
const submitForm = ref({
  ProcessType: '',
  BusinessKey: '',
  FormDataStr: '{}',
  OnBehalfOf: '',
  steps: [{ assignees: ['user1'] }]
})
const submitRules = {
  BusinessKey: [{ required: true, message: '请输入业务Key', trigger: 'blur' }]
}

function openSubmitDialog(row) {
  submitForm.value.ProcessType = row.processType || row.ProcessType || ''
  submitDialogVisible.value = true
}

function resetSubmitForm() {
  submitForm.value = {
    ProcessType: '',
    BusinessKey: '',
    FormDataStr: '{}',
    OnBehalfOf: '',
    steps: [{ assignees: ['user1'] }]
  }
}

function addStep() {
  submitForm.value.steps.push({ assignees: ['user1'] })
}

function removeStep(idx) {
  if (submitForm.value.steps.length <= 1) {
    ElMessage.warning('至少需要一个审批步骤')
    return
  }
  submitForm.value.steps.splice(idx, 1)
}

function addAssignee(stepIdx) {
  submitForm.value.steps[stepIdx].assignees.push('')
}

function removeAssignee(stepIdx, ai) {
  submitForm.value.steps[stepIdx].assignees.splice(ai, 1)
}

async function handleSubmitInstance() {
  try {
    await submitFormRef.value.validate()
  } catch {
    return
  }

  let formData = {}
  try {
    formData = JSON.parse(submitForm.value.FormDataStr || '{}')
  } catch {
    ElMessage.error('表单数据必须是合法的JSON格式')
    return
  }

  const validSteps = submitForm.value.steps.every(s =>
    s.assignees.every(a => a && a.trim())
  )
  if (!validSteps) {
    ElMessage.error('所有审批步骤的审批人不能为空')
    return
  }

  submitting.value = true
  try {
    const preparePayload = {
      ProcessType: submitForm.value.ProcessType,
      FormData: formData
    }
    if (submitForm.value.OnBehalfOf) preparePayload.OnBehalfOf = submitForm.value.OnBehalfOf

    await prepareSubmit(preparePayload)

    const confirmedSteps = submitForm.value.steps.map((step, i) => ({
      StepIndex: i + 1,
      Type: 'Approval',
      Assignees: step.assignees.filter(a => a.trim()).map(a => ({ UserId: a.trim() }))
    }))

    const submitPayload = {
      ProcessType: submitForm.value.ProcessType,
      BusinessKey: submitForm.value.BusinessKey,
      FormData: formData,
      ConfirmedSteps: confirmedSteps
    }
    if (submitForm.value.OnBehalfOf) submitPayload.OnBehalfOf = submitForm.value.OnBehalfOf

    const submitRes = await submitProcess(submitPayload)
    const submitResult = submitRes.data

    const instanceId = submitResult?.id || submitResult?.Id
      || `local-${Date.now()}`

    flowStore.setInstanceData(instanceId, {
      instanceId,
      processName: submitForm.value.ProcessType,
      businessKey: submitForm.value.BusinessKey,
      submittedBy: submitForm.value.OnBehalfOf || 'current-user',
      status: 'InProgress',
      steps: confirmedSteps.map((s, i) => ({
        stepIndex: s.StepIndex,
        type: s.Type,
        assignees: s.Assignees,
        status: i === 0 ? 'Pending' : 'NotStarted'
      }))
    })

    ElMessage.success('流程实例提交成功！')
    submitDialogVisible.value = false
    router.push(`/flow/${instanceId}`)
  } catch (err) {
    ElMessage.error('提交失败：' + err.message)
  } finally {
    submitting.value = false
  }
}

onMounted(fetchList)
</script>

<style scoped>
.process-list-view {
  max-width: 1200px;
  margin: 0 auto;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 20px;
}

.page-title {
  font-size: 22px;
  font-weight: 600;
  color: #262626;
  margin-bottom: 4px;
}

.page-desc {
  font-size: 13px;
  color: #8c8c8c;
}

.filter-card {
  margin-bottom: 16px;
}

.filter-row {
  display: flex;
  gap: 12px;
  align-items: center;
}

.table-card {
  margin-bottom: 16px;
}

.process-name {
  font-weight: 500;
  color: #1890ff;
}

.pagination-row {
  display: flex;
  justify-content: flex-end;
  margin-top: 16px;
}

.steps-container {
  padding: 0 0 8px 0;
}

.step-item {
  background: #f5f5f5;
  border: 1px solid #e8e8e8;
  border-radius: 6px;
  padding: 12px;
  margin-bottom: 8px;
}

.step-item-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 8px;
}

.step-item-title {
  font-weight: 600;
  color: #595959;
}

.step-assignees-list {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.assignee-row {
  display: flex;
  gap: 8px;
  align-items: center;
}
</style>
