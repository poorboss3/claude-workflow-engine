<template>
  <div class="workflow-diagram">
    <VueFlow
      :nodes="flowNodes"
      :edges="flowEdges"
      :fit-view-on-init="true"
      :nodes-draggable="false"
      :nodes-connectable="false"
      :elements-selectable="false"
      class="vue-flow-wrapper"
    >
      <Background pattern-color="#aaaaaa" :gap="16" />
      <template #node-stepNode="{ data }">
        <div class="step-node" :style="{ borderColor: getStatusColor(data.status), background: getStatusBg(data.status) }">
          <div class="step-node-header" :style="{ background: getStatusColor(data.status) }">
            <span class="step-index">步骤 {{ data.stepIndex }}</span>
            <span class="step-type">{{ data.type === 'Approval' ? '审批' : data.type }}</span>
          </div>
          <div class="step-node-body">
            <div class="step-status-row">
              <el-tag :type="getStatusTagType(data.status)" size="small">
                {{ getStatusLabel(data.status) }}
              </el-tag>
            </div>
            <div class="step-assignees">
              <span v-for="(a, ai) in (data.assignees || [])" :key="ai" class="assignee-tag">
                {{ a.UserId || a.userId || a }}
              </span>
            </div>
          </div>
        </div>
      </template>
    </VueFlow>
  </div>
</template>

<script setup>
import { computed } from 'vue'
import { VueFlow } from '@vue-flow/core'
import { Background } from '@vue-flow/background'

const props = defineProps({
  steps: {
    type: Array,
    default: () => []
  },
  currentStepIndex: {
    type: Number,
    default: null
  }
})

const STATUS_COLORS = {
  Pending: '#fa8c16',
  InProgress: '#fa8c16',
  Approved: '#52c41a',
  Rejected: '#f5222d',
  Returned: '#faad14',
  NotStarted: '#d9d9d9',
  Skipped: '#bfbfbf'
}

const STATUS_BG = {
  Pending: '#fff7e6',
  InProgress: '#fff7e6',
  Approved: '#f6ffed',
  Rejected: '#fff1f0',
  Returned: '#fffbe6',
  NotStarted: '#fafafa',
  Skipped: '#fafafa'
}

function getStatusColor(status) {
  return STATUS_COLORS[status] || STATUS_COLORS.NotStarted
}

function getStatusBg(status) {
  return STATUS_BG[status] || STATUS_BG.NotStarted
}

function getStatusTagType(status) {
  const map = {
    Pending: 'warning',
    InProgress: 'warning',
    Approved: 'success',
    Rejected: 'danger',
    Returned: 'warning',
    NotStarted: 'info',
    Skipped: 'info'
  }
  return map[status] || 'info'
}

function getStatusLabel(status) {
  const map = {
    Pending: '待审批',
    InProgress: '审批中',
    Approved: '已通过',
    Rejected: '已拒绝',
    Returned: '已退回',
    NotStarted: '未开始',
    Skipped: '已跳过'
  }
  return map[status] || status || '未知'
}

const flowNodes = computed(() => {
  const nodes = []

  nodes.push({
    id: 'start',
    type: 'input',
    position: { x: 0, y: 120 },
    label: '开始',
    style: {
      background: '#1890ff',
      color: '#fff',
      border: '2px solid #096dd9',
      borderRadius: '50%',
      width: '60px',
      height: '60px',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      fontSize: '13px',
      fontWeight: 600
    }
  })

  props.steps.forEach((step, i) => {
    nodes.push({
      id: `step-${i}`,
      type: 'stepNode',
      position: { x: 120 + 200 * i, y: 60 },
      data: {
        stepIndex: step.stepIndex ?? i + 1,
        type: step.type || 'Approval',
        assignees: step.assignees || [],
        status: step.status || 'NotStarted',
        action: step.action,
        comment: step.comment
      }
    })
  })

  const endX = 120 + 200 * props.steps.length
  nodes.push({
    id: 'end',
    type: 'output',
    position: { x: endX, y: 120 },
    label: '结束',
    style: {
      background: '#52c41a',
      color: '#fff',
      border: '2px solid #389e0d',
      borderRadius: '50%',
      width: '60px',
      height: '60px',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      fontSize: '13px',
      fontWeight: 600
    }
  })

  return nodes
})

const flowEdges = computed(() => {
  const edges = []

  if (props.steps.length === 0) {
    edges.push({
      id: 'e-start-end',
      source: 'start',
      target: 'end',
      animated: true,
      style: { stroke: '#1890ff' }
    })
    return edges
  }

  edges.push({
    id: 'e-start-step0',
    source: 'start',
    target: 'step-0',
    animated: true,
    style: { stroke: '#1890ff' }
  })

  for (let i = 0; i < props.steps.length - 1; i++) {
    edges.push({
      id: `e-step${i}-step${i + 1}`,
      source: `step-${i}`,
      target: `step-${i + 1}`,
      animated: true,
      style: { stroke: '#1890ff' }
    })
  }

  edges.push({
    id: `e-step${props.steps.length - 1}-end`,
    source: `step-${props.steps.length - 1}`,
    target: 'end',
    animated: true,
    style: { stroke: '#1890ff' }
  })

  return edges
})
</script>

<style scoped>
.workflow-diagram {
  width: 100%;
  height: 300px;
  background: #fafafa;
  border: 1px solid #e8e8e8;
  border-radius: 8px;
  overflow: hidden;
}

.vue-flow-wrapper {
  width: 100%;
  height: 100%;
}

.step-node {
  border: 2px solid #d9d9d9;
  border-radius: 8px;
  min-width: 140px;
  overflow: hidden;
  background: #fafafa;
  box-shadow: 0 2px 8px rgba(0,0,0,0.08);
}

.step-node-header {
  padding: 4px 8px;
  display: flex;
  justify-content: space-between;
  align-items: center;
  color: #fff;
  font-size: 12px;
}

.step-index {
  font-weight: 600;
}

.step-type {
  font-size: 11px;
  opacity: 0.9;
}

.step-node-body {
  padding: 6px 8px;
}

.step-status-row {
  margin-bottom: 4px;
}

.step-assignees {
  display: flex;
  flex-wrap: wrap;
  gap: 3px;
}

.assignee-tag {
  font-size: 11px;
  background: #e6f7ff;
  border: 1px solid #91d5ff;
  border-radius: 3px;
  padding: 1px 5px;
  color: #1890ff;
}
</style>
