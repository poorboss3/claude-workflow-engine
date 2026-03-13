<template>
  <div class="flow-view">
    <!-- Not Found State -->
    <div v-if="!instanceData" class="not-found-state">
      <el-empty description="未找到流程实例数据">
        <template #description>
          <p>请从任务收件箱进入，或先提交一个流程实例。</p>
          <p class="instance-id-hint" v-if="instanceId">实例ID：{{ instanceId }}</p>
        </template>
        <router-link to="/tasks">
          <el-button type="primary">前往任务收件箱</el-button>
        </router-link>
        <router-link to="/processes" style="margin-left: 8px">
          <el-button>前往流程列表</el-button>
        </router-link>
      </el-empty>
    </div>

    <template v-else>
      <div class="page-header">
        <div class="page-header-left">
          <el-button :icon="ArrowLeft" @click="$router.back()">返回</el-button>
          <div>
            <h2 class="page-title">流程详情</h2>
            <p class="page-desc">实例ID：{{ instanceId }}</p>
          </div>
        </div>
        <el-tag :type="getInstanceStatusType(instanceData.status)" size="large">
          {{ getInstanceStatusLabel(instanceData.status) }}
        </el-tag>
      </div>

      <div class="flow-layout">
        <!-- Left: Diagram -->
        <div class="flow-left">
          <el-card class="diagram-card">
            <template #header>
              <div class="card-header">
                <el-icon><Share /></el-icon>
                <span>流程图</span>
              </div>
            </template>
            <WorkflowDiagram
              :steps="instanceData.steps"
              :current-step-index="currentPendingStepIndex"
            />
          </el-card>
        </div>

        <!-- Right: Info + Actions -->
        <div class="flow-right">
          <el-card class="info-card">
            <template #header>
              <div class="card-header">
                <el-icon><InfoFilled /></el-icon>
                <span>实例信息</span>
              </div>
            </template>
            <el-descriptions :column="1" border size="small">
              <el-descriptions-item label="流程名称">{{ instanceData.processName }}</el-descriptions-item>
              <el-descriptions-item label="业务Key">
                <el-tag type="info" size="small">{{ instanceData.businessKey }}</el-tag>
              </el-descriptions-item>
              <el-descriptions-item label="提交人">{{ instanceData.submittedBy || '-' }}</el-descriptions-item>
              <el-descriptions-item label="当前状态">
                <el-tag :type="getInstanceStatusType(instanceData.status)" size="small">
                  {{ getInstanceStatusLabel(instanceData.status) }}
                </el-tag>
              </el-descriptions-item>
              <el-descriptions-item label="审批步骤数">{{ instanceData.steps.length }}</el-descriptions-item>
            </el-descriptions>
          </el-card>

          <!-- Approval Actions -->
          <el-card class="action-card" v-if="pendingTask">
            <template #header>
              <div class="card-header">
                <el-icon><EditPen /></el-icon>
                <span>审批操作</span>
                <el-tag type="warning" size="small" style="margin-left: 8px">待处理</el-tag>
              </div>
            </template>
            <div class="action-desc">
              当前步骤需要您的审批（步骤 {{ currentPendingStepIndex }}）
            </div>
            <div class="action-buttons">
              <el-button type="success" :icon="Check" @click="openActionDialog('approve')">
                通过
              </el-button>
              <el-button type="danger" :icon="Close" @click="openActionDialog('reject')">
                拒绝
              </el-button>
              <el-button type="warning" :icon="RefreshLeft" @click="openActionDialog('return')">
                退回
              </el-button>
              <el-button type="info" :icon="Plus" @click="openActionDialog('countersign')">
                加签
              </el-button>
            </div>
          </el-card>
          <el-card class="action-card" v-else>
            <template #header>
              <div class="card-header">
                <el-icon><EditPen /></el-icon>
                <span>审批操作</span>
              </div>
            </template>
            <el-empty description="当前无待处理的审批任务" :image-size="60" />
          </el-card>
        </div>
      </div>

      <!-- Bottom: Approval Log -->
      <el-card class="log-card">
        <template #header>
          <div class="card-header">
            <el-icon><Document /></el-icon>
            <span>审批记录</span>
            <el-button
              size="small"
              :icon="Refresh"
              :loading="loadingTasks"
              @click="loadCompletedTasks"
              style="margin-left: auto"
            >
              刷新
            </el-button>
          </div>
        </template>

        <el-table :data="approvalLog" v-loading="loadingTasks" stripe border>
          <el-table-column label="步骤" width="80" align="center">
            <template #default="{ row }">
              <el-tag size="small" type="info">{{ row.stepIndex || '-' }}</el-tag>
            </template>
          </el-table-column>
          <el-table-column label="审批人" min-width="120">
            <template #default="{ row }">{{ row.assignee || row.OriginalAssigneeId || '-' }}</template>
          </el-table-column>
          <el-table-column label="操作" width="100" align="center">
            <template #default="{ row }">
              <el-tag :type="getActionTagType(row.action || row.Action)" size="small">
                {{ getActionLabel(row.action || row.Action) }}
              </el-tag>
            </template>
          </el-table-column>
          <el-table-column label="备注" min-width="160">
            <template #default="{ row }">{{ row.comment || row.Comment || '-' }}</template>
          </el-table-column>
          <el-table-column label="完成时间" min-width="160">
            <template #default="{ row }">
              {{ formatDate(row.completedAt || row.CompletedAt) }}
            </template>
          </el-table-column>
        </el-table>

        <div v-if="approvalLog.length === 0 && !loadingTasks" class="log-empty">
          <el-empty description="暂无审批记录" :image-size="60" />
        </div>
      </el-card>
    </template>

    <!-- Action Dialog -->
    <el-dialog
      v-model="actionDialogVisible"
      :title="actionDialogTitle"
      width="440px"
      :close-on-click-modal="false"
    >
      <el-form :model="actionForm" ref="actionFormRef" label-width="80px">
        <el-form-item
          label="备注"
          :prop="'Comment'"
          :rules="requiresComment ? [{ required: true, message: '请输入备注原因', trigger: 'blur' }] : []"
        >
          <el-input
            v-model="actionForm.Comment"
            type="textarea"
            :rows="3"
            :placeholder="requiresComment ? '必填：请输入原因' : '可选备注'"
          />
        </el-form-item>

        <template v-if="currentAction === 'return'">
          <el-form-item label="退回步骤">
            <el-input v-model="actionForm.TargetStepId" placeholder="可选：目标步骤ID" />
          </el-form-item>
        </template>

        <template v-if="currentAction === 'countersign'">
          <el-form-item label="加签人员">
            <div v-for="(a, i) in actionForm.Assignees" :key="i" class="assignee-row">
              <el-input v-model="actionForm.Assignees[i]" placeholder="用户ID" size="small" style="flex: 1" />
              <el-button
                type="danger" size="small" :icon="Minus" circle
                :disabled="actionForm.Assignees.length <= 1"
                @click="actionForm.Assignees.splice(i, 1)"
              />
            </div>
            <el-button size="small" :icon="Plus" @click="actionForm.Assignees.push('')" style="margin-top: 6px">
              添加
            </el-button>
          </el-form-item>
        </template>
      </el-form>

      <template #footer>
        <el-button @click="actionDialogVisible = false">取消</el-button>
        <el-button
          :type="getActionButtonType(currentAction)"
          :loading="actionLoading"
          @click="handleAction"
        >
          确认{{ actionDialogTitle }}
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { ElMessage } from 'element-plus'
import {
  ArrowLeft, Check, Close, RefreshLeft, Plus, Minus, Refresh,
  Share, InfoFilled, EditPen, Document
} from '@element-plus/icons-vue'
import WorkflowDiagram from '../components/WorkflowDiagram.vue'
import { useFlowStore } from '../stores/flowStore.js'
import { useUserStore } from '../stores/userStore.js'
import { getCompletedTasks, approveTask, rejectTask, returnTask, countersignTask } from '../api/tasks.js'
import { getPendingTasks } from '../api/tasks.js'

const route = useRoute()
const flowStore = useFlowStore()
const userStore = useUserStore()
const instanceId = computed(() => route.params.instanceId)
const instanceData = computed(() => flowStore.getInstanceData(instanceId.value))

const loadingTasks = ref(false)
const completedTasks = ref([])
const pendingTask = ref(null)

const currentPendingStepIndex = computed(() => {
  if (!pendingTask.value) return null
  return pendingTask.value.stepIndex || pendingTask.value.StepIndex || null
})

const approvalLog = computed(() => {
  const fromStore = (instanceData.value?.steps || [])
    .filter(s => s.action)
    .map(s => ({
      stepIndex: s.stepIndex,
      assignee: (s.assignees || []).map(a => a.UserId || a.userId || a).join(', '),
      action: s.action,
      comment: s.comment,
      completedAt: s.completedAt
    }))

  const fromApi = completedTasks.value.filter(t => {
    const tid = t.instanceId || t.InstanceId
    return String(tid) === String(instanceId.value)
  }).map(t => ({
    stepIndex: t.stepIndex || t.StepIndex || '-',
    assignee: t.OriginalAssigneeId || t.assigneeId || '-',
    action: t.Action || t.action,
    comment: t.Comment || t.comment,
    completedAt: t.CompletedAt || t.completedAt
  }))

  const combined = [...fromStore]
  fromApi.forEach(apiItem => {
    const exists = combined.some(s =>
      s.action === apiItem.action && s.completedAt === apiItem.completedAt
    )
    if (!exists) combined.push(apiItem)
  })
  return combined
})

async function loadCompletedTasks() {
  loadingTasks.value = true
  try {
    const res = await getCompletedTasks({ page: 1, pageSize: 50 })
    const data = res.data
    completedTasks.value = Array.isArray(data) ? data : (data?.items || [])
  } catch (err) {
    // silently fail for log loading
  } finally {
    loadingTasks.value = false
  }
}

async function loadPendingTasks() {
  try {
    const res = await getPendingTasks({ page: 1, pageSize: 50 })
    const data = res.data
    const items = Array.isArray(data) ? data : (data?.items || [])
    const found = items.find(t => {
      const tid = t.instanceId || t.InstanceId
      return String(tid) === String(instanceId.value)
    })
    pendingTask.value = found || null
  } catch (err) {
    // silently fail
  }
}

// Action Dialog
const actionDialogVisible = ref(false)
const actionLoading = ref(false)
const currentAction = ref('')
const actionFormRef = ref(null)
const actionForm = ref({ Comment: '', TargetStepId: '', Assignees: [''] })

const requiresComment = computed(() =>
  currentAction.value === 'reject' || currentAction.value === 'return'
)

const actionDialogTitle = computed(() => {
  const map = { approve: '通过审批', reject: '拒绝审批', return: '退回审批', countersign: '加签' }
  return map[currentAction.value] || '操作'
})

function openActionDialog(action) {
  currentAction.value = action
  actionForm.value = { Comment: '', TargetStepId: '', Assignees: [''] }
  actionDialogVisible.value = true
}

function getActionButtonType(action) {
  const map = { approve: 'success', reject: 'danger', return: 'warning', countersign: 'primary' }
  return map[action] || 'primary'
}

async function handleAction() {
  try {
    await actionFormRef.value.validate()
  } catch {
    return
  }

  if (!pendingTask.value) {
    ElMessage.error('未找到待处理任务')
    return
  }

  const taskId = pendingTask.value.TaskId || pendingTask.value.taskId || pendingTask.value.id
  actionLoading.value = true
  try {
    if (currentAction.value === 'approve') {
      const payload = {}
      if (actionForm.value.Comment) payload.Comment = actionForm.value.Comment
      await approveTask(taskId, payload)
      flowStore.updateStepStatus(instanceId.value, currentPendingStepIndex.value, 'Approved', actionForm.value.Comment)
      ElMessage.success('审批通过')
    } else if (currentAction.value === 'reject') {
      await rejectTask(taskId, { Comment: actionForm.value.Comment })
      flowStore.updateStepStatus(instanceId.value, currentPendingStepIndex.value, 'Rejected', actionForm.value.Comment)
      ElMessage.success('已拒绝')
    } else if (currentAction.value === 'return') {
      const payload = { Comment: actionForm.value.Comment }
      if (actionForm.value.TargetStepId) payload.TargetStepId = actionForm.value.TargetStepId
      await returnTask(taskId, payload)
      flowStore.updateStepStatus(instanceId.value, currentPendingStepIndex.value, 'Returned', actionForm.value.Comment)
      ElMessage.success('已退回')
    } else if (currentAction.value === 'countersign') {
      const validAssignees = actionForm.value.Assignees.filter(a => a && a.trim())
      if (validAssignees.length === 0) {
        ElMessage.error('请至少添加一个加签人')
        return
      }
      const payload = {
        Assignees: validAssignees.map(a => ({ UserId: a.trim() }))
      }
      if (actionForm.value.Comment) payload.Comment = actionForm.value.Comment
      await countersignTask(taskId, payload)
      ElMessage.success('加签成功')
    }

    actionDialogVisible.value = false
    await Promise.all([loadPendingTasks(), loadCompletedTasks()])
  } catch (err) {
    ElMessage.error('操作失败：' + err.message)
  } finally {
    actionLoading.value = false
  }
}

function getInstanceStatusType(status) {
  const map = {
    InProgress: 'warning',
    Completed: 'success',
    Rejected: 'danger',
    Cancelled: 'info',
    Draft: 'info'
  }
  return map[status] || 'info'
}

function getInstanceStatusLabel(status) {
  const map = {
    InProgress: '进行中',
    Completed: '已完成',
    Rejected: '已拒绝',
    Cancelled: '已取消',
    Draft: '草稿'
  }
  return map[status] || status || '未知'
}

function getActionTagType(action) {
  const map = {
    Approved: 'success',
    Rejected: 'danger',
    Returned: 'warning',
    Countersigned: 'info'
  }
  return map[action] || 'info'
}

function getActionLabel(action) {
  const map = {
    Approved: '已通过',
    Rejected: '已拒绝',
    Returned: '已退回',
    Countersigned: '已加签'
  }
  return map[action] || action || '-'
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

onMounted(() => {
  if (instanceData.value) {
    loadPendingTasks()
    loadCompletedTasks()
  }
})
</script>

<style scoped>
.flow-view {
  max-width: 1400px;
  margin: 0 auto;
}

.not-found-state {
  display: flex;
  justify-content: center;
  align-items: center;
  min-height: 400px;
}

.instance-id-hint {
  color: #8c8c8c;
  font-size: 12px;
  margin-top: 4px;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 20px;
}

.page-header-left {
  display: flex;
  align-items: center;
  gap: 12px;
}

.page-title {
  font-size: 22px;
  font-weight: 600;
  color: #262626;
  margin-bottom: 2px;
}

.page-desc {
  font-size: 12px;
  color: #8c8c8c;
}

.flow-layout {
  display: flex;
  gap: 16px;
  margin-bottom: 16px;
}

.flow-left {
  flex: 3;
  min-width: 0;
}

.flow-right {
  flex: 2;
  min-width: 280px;
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.diagram-card,
.info-card,
.action-card,
.log-card {
  height: fit-content;
}

.card-header {
  display: flex;
  align-items: center;
  gap: 6px;
  font-weight: 600;
}

.action-desc {
  font-size: 13px;
  color: #595959;
  margin-bottom: 12px;
  padding: 8px;
  background: #fff7e6;
  border-radius: 4px;
  border-left: 3px solid #fa8c16;
}

.action-buttons {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.log-empty {
  padding: 20px 0;
}

.assignee-row {
  display: flex;
  gap: 8px;
  align-items: center;
  margin-bottom: 6px;
}
</style>
