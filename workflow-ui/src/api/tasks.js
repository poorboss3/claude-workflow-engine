import http from './http.js'

export function getPendingTasks(params = {}) {
  return http.get('/tasks/pending', { params })
}

export function getCompletedTasks(params = {}) {
  return http.get('/tasks/completed', { params })
}

export function approveTask(id, data = {}) {
  return http.post(`/tasks/${id}/approve`, data)
}

export function rejectTask(id, data) {
  return http.post(`/tasks/${id}/reject`, data)
}

export function returnTask(id, data) {
  return http.post(`/tasks/${id}/return`, data)
}

export function countersignTask(id, data) {
  return http.post(`/tasks/${id}/countersign`, data)
}
