<template>
  <div class="process-create-view">
    <div class="page-header">
      <div>
        <h2 class="page-title">新建流程定义</h2>
        <p class="page-desc">创建一个新的工作流程定义</p>
      </div>
      <router-link to="/processes">
        <el-button :icon="ArrowLeft">返回列表</el-button>
      </router-link>
    </div>

    <el-card class="form-card" v-if="!createdDefinition">
      <el-form
        :model="form"
        :rules="rules"
        ref="formRef"
        label-width="160px"
        style="max-width: 640px"
      >
        <el-form-item label="流程名称" prop="Name">
          <el-input
            v-model="form.Name"
            placeholder="请输入流程名称，如：请假申请流程"
            maxlength="100"
            show-word-limit
          />
        </el-form-item>
        <el-form-item label="流程类型" prop="ProcessType">
          <el-input
            v-model="form.ProcessType"
            placeholder="请输入流程类型标识，如：LeaveApproval"
            maxlength="100"
          />
          <div class="form-tip">流程类型是唯一标识符，用于提交实例时指定</div>
        </el-form-item>
        <el-form-item label="节点模板(JSON)" prop="NodeTemplatesJson">
          <el-input
            v-model="form.NodeTemplatesJson"
            type="textarea"
            :rows="5"
            placeholder='可选，节点模板JSON配置，如：[{"name":"HR审核","type":"Approval"}]'
          />
        </el-form-item>
        <el-form-item label="审批人解析URL">
          <el-input
            v-model="form.ApproverResolverUrl"
            placeholder="可选，审批人自动解析服务URL"
          />
        </el-form-item>
        <el-form-item label="权限验证URL">
          <el-input
            v-model="form.PermissionValidatorUrl"
            placeholder="可选，权限验证服务URL"
          />
        </el-form-item>
        <el-form-item>
          <el-button
            type="primary"
            :loading="loading"
            :icon="Check"
            @click="handleCreate"
          >
            创建流程定义
          </el-button>
          <el-button @click="handleReset">重置</el-button>
        </el-form-item>
      </el-form>
    </el-card>

    <!-- Success State -->
    <el-card class="success-card" v-else>
      <el-result
        icon="success"
        :title="`流程定义「${createdDefinition.name || createdDefinition.Name}」创建成功！`"
        :sub-title="`流程ID: ${createdDefinition.id || createdDefinition.Id} | 当前状态: 草稿`"
      >
        <template #extra>
          <div class="success-actions">
            <el-button
              type="success"
              size="large"
              :icon="VideoPlay"
              :loading="activating"
              @click="handleActivate"
            >
              激活此流程
            </el-button>
            <router-link to="/processes">
              <el-button size="large" :icon="List">返回流程列表</el-button>
            </router-link>
            <el-button size="large" :icon="Plus" @click="resetToCreate">继续新建</el-button>
          </div>
          <div v-if="activatedSuccess" class="activate-success-tip">
            <el-alert
              title="流程已激活！现在可以前往流程列表提交测试实例。"
              type="success"
              show-icon
              :closable="false"
              style="margin-top: 16px"
            />
          </div>
        </template>
      </el-result>
    </el-card>
  </div>
</template>

<script setup>
import { ref } from 'vue'
import { ElMessage } from 'element-plus'
import { ArrowLeft, Check, VideoPlay, List, Plus } from '@element-plus/icons-vue'
import { createDefinition, activateDefinition } from '../api/definitions.js'

const formRef = ref(null)
const loading = ref(false)
const activating = ref(false)
const createdDefinition = ref(null)
const activatedSuccess = ref(false)

const form = ref({
  Name: '',
  ProcessType: '',
  NodeTemplatesJson: '',
  ApproverResolverUrl: '',
  PermissionValidatorUrl: ''
})

const rules = {
  Name: [
    { required: true, message: '请输入流程名称', trigger: 'blur' },
    { min: 2, max: 100, message: '名称长度在 2-100 个字符', trigger: 'blur' }
  ],
  ProcessType: [
    { required: true, message: '请输入流程类型标识', trigger: 'blur' },
    { pattern: /^[a-zA-Z0-9_-]+$/, message: '流程类型只能包含字母、数字、下划线和连字符', trigger: 'blur' }
  ],
  NodeTemplatesJson: [
    {
      validator: (rule, value, callback) => {
        if (!value || !value.trim()) return callback()
        try {
          JSON.parse(value)
          callback()
        } catch {
          callback(new Error('请输入合法的JSON格式'))
        }
      },
      trigger: 'blur'
    }
  ]
}

async function handleCreate() {
  try {
    await formRef.value.validate()
  } catch {
    return
  }

  loading.value = true
  try {
    const payload = {
      Name: form.value.Name,
      ProcessType: form.value.ProcessType
    }
    if (form.value.NodeTemplatesJson?.trim()) {
      payload.NodeTemplatesJson = form.value.NodeTemplatesJson.trim()
    }
    if (form.value.ApproverResolverUrl?.trim()) {
      payload.ApproverResolverUrl = form.value.ApproverResolverUrl.trim()
    }
    if (form.value.PermissionValidatorUrl?.trim()) {
      payload.PermissionValidatorUrl = form.value.PermissionValidatorUrl.trim()
    }

    const res = await createDefinition(payload)
    createdDefinition.value = res.data
    ElMessage.success('流程定义创建成功！')
  } catch (err) {
    ElMessage.error('创建失败：' + err.message)
  } finally {
    loading.value = false
  }
}

async function handleActivate() {
  const id = createdDefinition.value?.id || createdDefinition.value?.Id
  if (!id) return

  activating.value = true
  try {
    await activateDefinition(id)
    activatedSuccess.value = true
    ElMessage.success('流程已激活！')
  } catch (err) {
    ElMessage.error('激活失败：' + err.message)
  } finally {
    activating.value = false
  }
}

function handleReset() {
  formRef.value?.resetFields()
}

function resetToCreate() {
  createdDefinition.value = null
  activatedSuccess.value = false
  form.value = {
    Name: '',
    ProcessType: '',
    NodeTemplatesJson: '',
    ApproverResolverUrl: '',
    PermissionValidatorUrl: ''
  }
}
</script>

<style scoped>
.process-create-view {
  max-width: 800px;
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

.form-card {
  padding: 8px;
}

.form-tip {
  font-size: 12px;
  color: #8c8c8c;
  margin-top: 4px;
  line-height: 1.4;
}

.success-card {
  text-align: center;
}

.success-actions {
  display: flex;
  gap: 12px;
  justify-content: center;
  flex-wrap: wrap;
}
</style>
