import { createRouter, createWebHistory } from 'vue-router'
import ProcessListView from '../views/ProcessListView.vue'
import ProcessCreateView from '../views/ProcessCreateView.vue'
import FlowView from '../views/FlowView.vue'
import TaskInboxView from '../views/TaskInboxView.vue'

const routes = [
  {
    path: '/',
    redirect: '/tasks'
  },
  {
    path: '/processes',
    name: 'ProcessList',
    component: ProcessListView
  },
  {
    path: '/processes/create',
    name: 'ProcessCreate',
    component: ProcessCreateView
  },
  {
    path: '/flow/:instanceId',
    name: 'FlowView',
    component: FlowView,
    props: true
  },
  {
    path: '/tasks',
    name: 'TaskInbox',
    component: TaskInboxView
  }
]

const router = createRouter({
  history: createWebHistory(),
  routes
})

export default router
