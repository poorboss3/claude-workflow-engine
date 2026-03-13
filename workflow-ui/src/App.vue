<template>
  <el-container class="app-container">
    <el-header class="app-header">
      <div class="header-left">
        <el-icon class="header-icon"><Setting /></el-icon>
        <span class="app-title">工作流引擎管理系统</span>
      </div>
      <div class="header-nav">
        <router-link to="/processes" class="nav-link" :class="{ active: $route.path.startsWith('/processes') }">
          <el-icon><List /></el-icon>
          流程列表
        </router-link>
        <router-link to="/tasks" class="nav-link" :class="{ active: $route.path === '/tasks' }">
          <el-icon><Bell /></el-icon>
          任务收件箱
        </router-link>
      </div>
      <div class="header-right">
        <span class="user-label">当前用户：</span>
        <el-input
          v-model="inputUserId"
          placeholder="输入用户ID"
          size="small"
          style="width: 120px"
          @keyup.enter="handleUserChange"
        />
        <el-button size="small" type="primary" @click="handleUserChange" style="margin-left: 6px">
          切换
        </el-button>
        <el-tag size="small" type="success" style="margin-left: 8px">
          {{ userStore.userId }}
        </el-tag>
      </div>
    </el-header>
    <el-main class="app-main">
      <RouterView />
    </el-main>
  </el-container>
</template>

<script setup>
import { ref } from 'vue'
import { useUserStore } from './stores/userStore.js'
import { ElMessage } from 'element-plus'

const userStore = useUserStore()
const inputUserId = ref(userStore.userId)

function handleUserChange() {
  const val = inputUserId.value.trim()
  if (!val) {
    ElMessage.warning('用户ID不能为空')
    return
  }
  userStore.setUserId(val)
  ElMessage.success(`已切换到用户：${val}`)
}
</script>

<style>
* {
  box-sizing: border-box;
  margin: 0;
  padding: 0;
}

body {
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
  background: #f0f2f5;
}

.app-container {
  min-height: 100vh;
  display: flex;
  flex-direction: column;
}

.app-header {
  background: #1890ff;
  color: white;
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 24px;
  height: 56px !important;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.15);
  position: sticky;
  top: 0;
  z-index: 1000;
}

.header-left {
  display: flex;
  align-items: center;
  gap: 8px;
}

.header-icon {
  font-size: 22px;
}

.app-title {
  font-size: 18px;
  font-weight: 600;
  letter-spacing: 1px;
}

.header-nav {
  display: flex;
  gap: 4px;
}

.nav-link {
  display: flex;
  align-items: center;
  gap: 4px;
  color: rgba(255, 255, 255, 0.85);
  text-decoration: none;
  padding: 6px 16px;
  border-radius: 4px;
  font-size: 14px;
  transition: all 0.2s;
}

.nav-link:hover,
.nav-link.active {
  color: #fff;
  background: rgba(255, 255, 255, 0.2);
}

.header-right {
  display: flex;
  align-items: center;
  gap: 4px;
}

.user-label {
  font-size: 13px;
  color: rgba(255, 255, 255, 0.9);
  white-space: nowrap;
}

.app-main {
  flex: 1;
  padding: 24px;
  overflow: auto;
}
</style>
