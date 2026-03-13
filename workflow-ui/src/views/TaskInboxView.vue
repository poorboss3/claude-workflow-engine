<template>
  <div class="task-inbox-view">
    <div class="page-header">
      <div>
        <h2 class="page-title">任务收件箱</h2>
        <p class="page-desc">当前用户：{{ userStore.userId }}</p>
      </div>
      <el-button :icon="Refresh" :loading="loading" @click="loadCurrentTab">刷新</el-button>
    </div>

    <el-card>
      <el-tabs v-model="activeTab" @tab-change="handleTabChange">
        <!-- Pending Tasks Tab -->
        <el-tab-pane name="pending">
          <template #label>
            <div class="tab-label">
              <el-icon><Clock /></el-icon>
              待处理
              <el-badge
                v-if="pendingTotal > 0"
                :value="pendingTotal"
                :max="99"
                class="tab-badge"
              />
            </div>
          </template>

          <el-table
            v-loading="loading"
            :data="pendingTasks"
            stripe
            border
            style="width: 100%"
          >
            <el-table-column label="流程名称" min-width="140">
              <template #default="{ row }">
                <span class="process-name">{{ row.ProcessName || row.processName }}</span>
              </template>
            </el-table-column>
            <el-table-column label="业务Key" min-width="140">
              <template #default="{ row }">
                <el-tag type="info" size="small">{{ row.BusinessKey || row.businessKey }}</el-tag>
              </template>
            </el-table-column>
            <el-table-column label="步骤类型" width="100" align="center">
              <template #default="{ row }">
                {{ getStepTypeLabel(row.StepType || row.stepType) }}
              </template>
            </el-table-column>
            <el-table-column label="待处理时间" min-width="160">
              <template #default="{ row }">
                {{ formatDate(row.PendingSince || row.pendingSince) }}
              </template>
            </el-table-column>
            <el-table-column label="标记" width="100" align="center">
              <template #default="{ row }">
                <el-tag v-if="row.IsUrgent || row.isUrgent" type="danger" size="small" effect="dark">
                  紧急
                </el-tag>
                <el-tag v-if="row.IsDelegated || row.isDelegated" type="warning" size="small">
                  代理
                </el-tag>
              </template>
            </el-table-column>
            <el-table-column label="操作" width="240" align="center" fixed="right">
              <template #default="{ row }">
                <el-button
                  size="small"
                  type="primary"
                  :icon="View"
                  @click="viewFlow(row)"
                >
                  查看流程
                </el-button>
                <el-button
                  size="small"
                  type="success"
                  :icon="Check"
                  @click="openQuickAction(row, 'approve')"
                >
                  审批
                </el-button>
              </template>
            </el-table-column>
          </el-table>

          <div class="pagination-row">
            <el-pagination
              v-model:current-page="pendingPage"
              v-model:page-size="pendingPageSize"
              :total="pendingTotal"
              :page-sizes="[10, 20, 50]"
              layout="total, sizes, prev, pager, next"
              @current-change="loadPendingTasks"
              @size-change="loadPendingTasks"
            />
          </div>
        </el-tab-pane>

        <!-- Completed Tasks Tab -->
        <el-tab-pane name="completed">
          <template #label>
            <div class="tab-label">
              <el-icon><Finished /></el-icon>
              已完成
            </div>
          </template>

          <el-table
            v-loading="loading"
            :data="completedTasks"
            stripe
            border
            style="width: 100%"
          >
            <el-table-column label="流程名称" min-width="140">
              <template #default="{ row }">
                <span class="process-name">{{ row.ProcessName || row.processName }}</span>
              </template>
            </el-table-column>
            <el-table-column label="业务Key" min-width="140">
              <template #default="{ row }">
                <el-tag type="info" size="small">{{ row.BusinessKey || row.businessKey }}</el-tag>
              </template>
            </el-table-column>
            <el-table-column label="操作结果" width="110" align="center">
              <template #default="{ row }">
                <el-tag :type="getActionTagType(row.Action || row.action)" size="small" effect="light">
                  {{ getActionLabel(row.Action || row.action) }}
                </el-tag>
              </template>
            </el-table-column>
            <el-table-column label="流程状态" width="110" align="center">
              <template #default="{ row }">
                <el-tag :type="getProcessStatusType(row.ProcessStatus || row.processStatus)" size="small">
                  {{ getProcessStatusLabel(row.ProcessStatus || row.processStatus) }}
                </el-tag>
              </template>
            </el-table-column>
            <el-table-column label="完成时间" min-width="160">
              <template #default="{ row }">
                {{ formatDate(row.CompletedAt || row.completedAt) }}
              </template>
            </el-table-column>
            <el-table-column label="备注" min-width="160">
              <template #default="{ row }">
                <span class="comment-text">{{ row.Comment || row.comment || '-' }}</span>
              </template>
            </el-table-column>
          </el-table>

          <div class="pagination-row">
            <el-pagination
              v-model:current-page="completedPage"
              v-model:page-size="completedPageSize"
              :total="completedTotal"
              :page-sizes="[10, 20, 50]"
              layout="total, sizes, prev, pager, next"
              @current-change="loadCompletedTasks"
              @size-change="loadCompletedTasks"
            />
          </div>
        </el-tab-pane>
      </el-tabs>
    </el-card>

    <!-- Quick Action Dialog -->
    <el-dialog
      v-model="quickActionVisible"
      :title="quickActionTitle"
      width="480px"
      :close-on-click-modal="false"
    >
      <div v-if="selectedTask" class="quick-action-task-info">
        <el-descriptions :column="2" border size="small">
          <el-descriptions-item label="流程">
            {{ selectedTask.ProcessName || selectedTask.processName }}
          </el-descriptions-item>
          <el-descriptions-item label="业务Key">
            {{ selectedTask.BusinessKey || selectedTask.businessKey }}
          </el-descriptions-item>
        </el-descriptions>
      </div>

      <el-form :model="quickForm" ref="quickFormRef" label-width="80px" style="margin-top: 16px">
        <el-form-item label="操作">
          <el-radio-group v-model="quickAction">
            <el-radio-button value="approve">
              <el-icon><Check /></el-icon> 通过
            </el-radio-button>
            <el-radio-button value="reject">
              <el-icon><Close /></el-icon> 拒绝
            </el-radio-button>
          </el-radio-group>
        </el-form-item>
        <el-form-item
          label="备注"
          :rules="quickAction === 'reject' ? [{ required: true, message: '拒绝时必须填写备注', trigger: 'blur' }] : []"
          prop="Comment"
        >
          <el-input
            v-model="quickForm.Comment"
            type="textarea"
            :rows="3"
            :placeholder="quickAction === 'reject' ? '必填：请输入拒绝原因' : '可选备注'"
          />
        </el-form-item>
      </el-form>

      <template #footer>
        <el-button @click="quickActionVisible = false">取消</el-button>
        <el-button
          :type="quickAction === 'approve' ? 'success' : 'danger'"
          :loading="actionLoading"
          @click="handleQuickAction"
        >
          确认{{ quickAction === 'approve' ? '通过' : '拒绝' }}
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { Refresh, Clock, Finished, View, Check, Close } from '@element-plus/icons-vue'
import { useUserStore } from '../stores/userStore.js'
import { useFlowStore } from '../stores/flowStore.js'
import { getPendingTasks, getCompletedTasks, approveTask, rejectTask } from '../api/tasks.js'

const router = useRouter()
const userStore = useUserStore()
const flowStore = useFlowStore()

const activeTab = ref('pending')
const loading = ref(false)

// Pending
const pendingTasks = ref([])
const pendingPage = ref(1)
const pendingPageSize = ref(10)
const pendingTotal = ref(0)

// Completed
const completedTasks = ref([])
const completedPage = ref(1)
const completedPageSize = ref(10)
const completedTotal = ref(0)

// Quick Action
const quickActionVisible = ref(false)
const actionLoading = ref(false)
const selectedTask = ref(null)
const quickAction = ref('approve')
const quickFormRef = ref(null)
const quickForm = ref({ Comment: '' })

const quickActionTitle = computed(() =>
  quickAction.value === 'approve' ? '快速审批 - 通过/拒绝' : '快速审批'
)

async function loadPendingTasks() {
  loading.value = true
  try {
    const res = await getPendingTasks({ page: pendingPage.value, pageSize: pendingPageSize.value })
    const data = res.data
    if (Array.isArray(data)) {
      pendingTasks.value = data
      pendingTotal.value = data.length
    } else {
      pendingTasks.value = data?.items || []
      pendingTotal.value = data?.total || 0
    }
  } catch (err) {
    ElMessage.error('加载待处理任务失败：' + err.message)
    pendingTasks.value = []
  } finally {
    loading.value = false
  }
}

async function loadCompletedTasks() {
  loading.value = true
  try {
    const res = await getCompletedTasks({ page: completedPage.value, pageSize: completedPageSize.value })
    const data = res.data
    if (Array.isArray(data)) {
      completedTasks.value = data
      completedTotal.value = data.length
    } else {
      completedTasks.value = data?.items || []
      completedTotal.value = data?.total || 0
    }
  } catch (err) {
    ElMessage.error('加载已完成任务失败：' + err.message)
    completedTasks.value = []
  } finally {
    loading.value = false
  }
}

function loadCurrentTab() {
  if (activeTab.value === 'pending') loadPendingTasks()
  else loadCompletedTasks()
}

function handleTabChange(tab) {
  if (tab === 'pending') loadPendingTasks()
  else loadCompletedTasks()
}

function viewFlow(row) {
  const instanceId = row.InstanceId || row.instanceId
  if (!instanceId) {
    ElMessage.warning('无法获取实例ID')
    return
  }
  // Try to populate flowStore if not present
  if (!flowStore.getInstanceData(instanceId)) {
    flowStore.setInstanceData(instanceId, {
      instanceId,
      processName: row.ProcessName || row.processName || '',
      businessKey: row.BusinessKey || row.businessKey || '',
      submittedBy: row.InitiatorId || row.initiatorId || '',
      status: 'InProgress',
      steps: []
    })
  }
  router.push(`/flow/${instanceId}`)
}

function openQuickAction(row, action) {
  selectedTask.value = row
  quickAction.value = action
  quickForm.value = { Comment: '' }
  quickActionVisible.value = true
}

async function handleQuickAction() {
  try {
    await quickFormRef.value.validate()
  } catch {
    return
  }

  const taskId = selectedTask.value?.TaskId || selectedTask.value?.taskId || selectedTask.value?.id
  if (!taskId) {
    ElMessage.error('未找到任务ID')
    return
  }

  actionLoading.value = true
  try {
    if (quickAction.value === 'approve') {
      const payload = {}
      if (quickForm.value.Comment) payload.Comment = quickForm.value.Comment
      await approveTask(taskId, payload)
      ElMessage.success('审批通过')
    } else {
      await rejectTask(taskId, { Comment: quickForm.value.Comment })
      ElMessage.success('已拒绝')
    }
    quickActionVisible.value = false
    loadPendingTasks()
  } catch (err) {
    ElMessage.error('操作失败：' + err.message)
  } finally {
    actionLoading.value = false
  }
}

function getStepTypeLabel(type) {
  const map = { Approval: '审批', Review: '审核', Countersign: '会签' }
  return map[type] || type || '-'
}

function getActionTagType(action) {
  const map = { Approved: 'success', Rejected: 'danger', Returned: 'warning', Countersigned: 'info' }
  return map[action] || 'info'
}

function getActionLabel(action) {
  const map = { Approved: '已通过', Rejected: '已拒绝', Returned: '已退回', Countersigned: '已加签' }
  return map[action] || action || '-'
}

function getProcessStatusType(status) {
  const map = { InProgress: 'warning', Completed: 'success', Rejected: 'danger', Cancelled: 'info' }
  return map[status] || 'info'
}

function getProcessStatusLabel(status) {
  const map = { InProgress: '进行中', Completed: '已完成', Rejected: '已拒绝', Cancelled: '已取消' }
  return map[status] || status || '-'
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

onMounted(loadPendingTasks)
</script>

<style scoped>
.task-inbox-view {
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

.tab-label {
  display: flex;
  align-items: center;
  gap: 4px;
  position: relative;
}

.tab-badge {
  margin-left: 4px;
}

.pagination-row {
  display: flex;
  justify-content: flex-end;
  margin-top: 16px;
}

.process-name {
  font-weight: 500;
  color: #1890ff;
}

.comment-text {
  color: #595959;
  font-size: 13px;
}

.quick-action-task-info {
  margin-bottom: 8px;
}
</style>
