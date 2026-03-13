import http from './http.js'

export function listDefinitions(params = {}) {
  return http.get('/process-definitions', { params })
}

export function getDefinition(id) {
  return http.get(`/process-definitions/${id}`)
}

export function createDefinition(data) {
  return http.post('/process-definitions', data)
}

export function activateDefinition(id) {
  return http.post(`/process-definitions/${id}/activate`)
}

export function archiveDefinition(id) {
  return http.post(`/process-definitions/${id}/archive`)
}
