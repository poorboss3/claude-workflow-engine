import { defineStore } from 'pinia'
import { ref } from 'vue'

export const useFlowStore = defineStore('flow', () => {
  // Map: instanceId -> instance data
  const instances = ref(new Map())

  function setInstanceData(instanceId, data) {
    instances.value.set(String(instanceId), {
      instanceId: String(instanceId),
      processName: data.processName || data.ProcessName || '',
      businessKey: data.businessKey || data.BusinessKey || '',
      submittedBy: data.submittedBy || data.SubmittedBy || '',
      status: data.status || data.Status || 'InProgress',
      steps: (data.steps || data.Steps || []).map((step, idx) => ({
        stepIndex: step.stepIndex ?? step.StepIndex ?? idx + 1,
        type: step.type || step.Type || 'Approval',
        assignees: step.assignees || step.Assignees || [],
        status: step.status || step.Status || 'NotStarted',
        action: step.action || step.Action || null,
        comment: step.comment || step.Comment || '',
        completedAt: step.completedAt || step.CompletedAt || null
      }))
    })
  }

  function getInstanceData(instanceId) {
    return instances.value.get(String(instanceId)) || null
  }

  function updateStepStatus(instanceId, stepIndex, action, comment) {
    const instance = instances.value.get(String(instanceId))
    if (!instance) return
    const step = instance.steps.find(s => s.stepIndex === stepIndex)
    if (step) {
      step.action = action
      step.comment = comment
      step.status = action === 'Approved' ? 'Approved'
        : action === 'Rejected' ? 'Rejected'
        : action === 'Returned' ? 'Returned'
        : step.status
      step.completedAt = new Date().toISOString()
    }
  }

  function allInstances() {
    return Array.from(instances.value.values())
  }

  return { instances, setInstanceData, getInstanceData, updateStepStatus, allInstances }
})
